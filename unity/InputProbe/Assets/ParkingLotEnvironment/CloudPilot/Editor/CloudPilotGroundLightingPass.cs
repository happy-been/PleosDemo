using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    public static class CloudPilotGroundLightingPass
    {
        const string Kit = "Assets/ParkingLotEnvironment/CloudPilot";
        const string ScenePath = "Assets/_Parkside/Scenes/ParklingLot.unity";
        static GameObject Open()
        {
            if (File.Exists("/workspaces/parkside-setup/STOP")) throw new Exception("STOP");
            EditorSceneManager.OpenScene(ScenePath);
            if (QualitySettings.shadows != ShadowQuality.Disable || QualitySettings.antiAliasing != 0)
                throw new Exception("Native quality changed");
            return SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == "ParkingLot_CornerPilot_Root");
        }
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        static string output;
        static GameObject bakeRoot;
        static double bakeStarted;
        static StringBuilder bakeReport;
        static void Persist(UnityEngine.Object o)
        {
            EditorUtility.SetDirty(o);
            if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }
        public static void RepairAndBake()
        {
            try
            {
                bakeRoot = Open();
                output = File.ReadAllText(Kit + "/Inspection~/full-canopy-current.txt").Trim();
                const string folder = Kit + "/GroundLightingRepair";
                if (Directory.Exists(folder)) throw new Exception("Repair checkpoint already exists; review before repeat");
                Directory.CreateDirectory(folder + "/Meshes"); AssetDatabase.Refresh();
                bakeReport = new StringBuilder("Fix Street substring being mistaken for tree. Existing mapped renderers are untouched.\n");
                var clones = new Dictionary<Mesh, Mesh>();
                int repaired = 0;
                foreach (var r in bakeRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    string path = PathOf(r.transform);
                    // Street contains 'tree'; remove that word before recognizing actual tree ancestors.
                    string withoutStreet = Regex.Replace(path, "street", "", RegexOptions.IgnoreCase);
                    bool realCaster = withoutStreet.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 || path.Contains("Shrub") || path.IndexOf("vehicle", StringComparison.OrdinalIgnoreCase) >= 0 || path.Contains("Truck");
                    if (realCaster || path.IndexOf("street", StringComparison.OrdinalIgnoreCase) < 0 || r.scaleInLightmap != 0 || r.receiveGI != ReceiveGI.Lightmaps || (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) == 0) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) throw new Exception("Missing mesh: " + path);
                    Mesh mesh;
                    if (!clones.TryGetValue(mf.sharedMesh, out mesh))
                    {
                        mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                        mesh.name = "StreetReceiverUV_" + clones.Count;
                        UnwrapParam unwrap; UnwrapParam.SetDefaults(out unwrap); unwrap.packMargin = .04f;
                        if (!Unwrapping.GenerateSecondaryUVSet(mesh, unwrap)) throw new Exception("UV unwrap failed: " + path);
                        AssetDatabase.CreateAsset(mesh, folder + "/Meshes/Receiver_" + clones.Count + ".asset");
                        clones.Add(mf.sharedMesh, mesh);
                    }
                    mf.sharedMesh = mesh;
                    r.scaleInLightmap = path.Contains("RearStreet") || path.Contains("Footway") ? 1.25f : .12f;
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    Persist(mf); Persist(r);
                    bakeReport.AppendLine("REPAIRED " + path + " scale=" + r.scaleInLightmap);
                    repaired++;
                }
                if (repaired == 0) throw new Exception("No expected falsely classified receivers found");
                bakeReport.AppendLine("Total repaired=" + repaired + " cloned meshes=" + clones.Count);
                var settings = UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);
                settings.name = "StreetReceiverCPU";
                settings.autoGenerate = false;
                AssetDatabase.CreateAsset(settings, folder + "/StreetReceiverCPU.lighting");
                Lightmapping.lightingSettings = settings;
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                // Bake in an owned work scene so generated assets cannot land beside a protected scene.
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), folder + "/StreetReceiverBakeWork.unity");
                File.WriteAllText(output + "/ground-lighting-repair.txt", bakeReport.ToString());
                bakeStarted = EditorApplication.timeSinceStartup;
                EditorApplication.update += BakeTick;
                if (!Lightmapping.BakeAsync()) throw new Exception("Bake request rejected");
            }
            catch (Exception ex) { EditorApplication.update -= BakeTick; Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        static void BakeTick()
        {
            try
            {
                if (File.Exists("/workspaces/parkside-setup/STOP")) throw new Exception("STOP");
                double elapsed = EditorApplication.timeSinceStartup - bakeStarted;
                if (elapsed > 480) throw new Exception("Bake exceeded 480 seconds");
                if (elapsed < 5 || Lightmapping.isRunning) return;
                EditorApplication.update -= BakeTick;
                if (LightmapSettings.lightmaps.Length == 0) throw new Exception("No baked lightmaps");
                foreach (var r in bakeRoot.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "RearStreet" || r.name == "RightStreetFootway"))
                {
                    if (r.lightmapIndex < 0 || r.lightmapIndex >= LightmapSettings.lightmaps.Length) throw new Exception("Ground still unmapped: " + r.name);
                    bakeReport.AppendLine("VERIFIED " + r.name + " map=" + r.lightmapIndex + " scaleOffset=" + r.lightmapScaleOffset);
                }
                foreach (var probe in bakeRoot.GetComponentsInChildren<ReflectionProbe>())
                {
                    string reflection = Kit + "/GroundLightingRepair/StreetReceiverReflection.exr";
                    if (!Lightmapping.BakeReflectionProbe(probe, reflection)) throw new Exception("Reflection bake failed");
                    AssetDatabase.ImportAsset(reflection, ImportAssetOptions.ForceSynchronousImport);
                    probe.mode = ReflectionProbeMode.Custom;
                    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(reflection);
                    Persist(probe);
                }
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
                AssetDatabase.SaveAssets();
                bakeReport.AppendLine("Saved map count=" + LightmapSettings.lightmaps.Length + " bake seconds=" + elapsed + " shadows=" + QualitySettings.shadows + " AA=" + QualitySettings.antiAliasing);
                File.WriteAllText(output + "/ground-lighting-repair.txt", bakeReport.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { EditorApplication.update -= BakeTick; Debug.LogException(ex); EditorApplication.Exit(1); }
        }
        public static void Audit()
        {
            try
            {
                var pilot = Open();
                var report = new StringBuilder();
                report.AppendLine("Native shadows=" + QualitySettings.shadows + " AA=" + QualitySettings.antiAliasing + " lightmaps=" + LightmapSettings.lightmaps.Length);
                foreach (var r in pilot.GetComponentsInChildren<MeshRenderer>().Where(r => new[] { "Ground", "Street", "Footway", "Asphalt", "Sidewalk" }.Any(s => r.name.Contains(s)) && r.bounds.size.x * r.bounds.size.z > 10))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    report.AppendLine("RENDERER " + PathOf(r.transform) + " bounds=" + r.bounds + " enabled=" + r.enabled + " GI=" + r.receiveGI + " lightmap=" + r.lightmapIndex + " scaleOffset=" + r.lightmapScaleOffset + " scale=" + r.scaleInLightmap + " probes=" + r.lightProbeUsage + " anchor=" + (r.probeAnchor == null ? "none" : r.probeAnchor.position.ToString()) + " static=" + GameObjectUtility.GetStaticEditorFlags(r.gameObject));
                    report.AppendLine("MESH " + (mf == null ? "none" : AssetDatabase.GetAssetPath(mf.sharedMesh) + " uv2=" + mf.sharedMesh.uv2.Length));
                    foreach (var m in r.sharedMaterials) report.AppendLine("MAT " + AssetDatabase.GetAssetPath(m) + " shader=" + m.shader.name + " color=" + (m.HasProperty("_Color") ? m.color.ToString() : "n/a"));
                    foreach (var p in new[] { r.bounds.center, new Vector3(r.bounds.center.x, .6f, r.bounds.center.z), new Vector3(0, .6f, 0) })
                    {
                        LightProbes.GetInterpolatedProbe(p, r, out var sh);
                        var colors = new Color[1]; sh.Evaluate(new[] { Vector3.up }, colors);
                        report.AppendLine("SH upward position=" + p + " color=" + colors[0]);
                    }
                }
                var output = File.ReadAllText(Kit + "/Inspection~/full-canopy-current.txt").Trim();
                File.WriteAllText(output + "/ground-lighting-audit.txt", report.ToString());
                Debug.Log(report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
    }
}
