using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    // Whole-frame daylight experiment: stronger direct sun and reduced environment fill; no global quality edits.
    // This edits only a new scene-specific LightingSettings asset and the authorized parking scene.
    public static class CloudPilotSunContrastPass
    {
        const string Kit = "Assets/ParkingLotEnvironment/CloudPilot";
        const string ScenePath = "Assets/_Parkside/Scenes/ParklingLot.unity";
        const string Owned = Kit + "/SunContrastLighting";
        static string output;
        static double started;
        static GameObject pilot;
        static readonly StringBuilder report = new StringBuilder();
        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            if (i < 0 || i + 1 >= args.Length) throw new Exception("Required argument: " + name);
            return args[i + 1];
        }
        static string Hash(string path)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        static void Check()
        {
            if (File.Exists("/workspaces/parkside-setup/STOP")) throw new Exception("STOP");
            if (QualitySettings.shadows != ShadowQuality.Disable || QualitySettings.antiAliasing != 0) throw new Exception("Unexpected project quality");
        }
        static void Persist(UnityEngine.Object o)
        {
            EditorUtility.SetDirty(o);
            if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }
        public static void Bake()
        {
            try
            {
                Check();
                if (Hash(ScenePath) != Arg("-parkingExpectedSceneHash")) throw new Exception("Scene changed since reviewed checkpoint");
                if (Directory.Exists(Owned)) throw new Exception("Directional experiment already exists; review before repeat");
                output = Path.GetFullPath(Arg("-parkingSunContrastOutput")); Directory.CreateDirectory(output);
                EditorSceneManager.OpenScene(ScenePath);
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                pilot = roots.Single(g => g.name == "ParkingLot_CornerPilot_Root");
                if (roots.Single(g => g.name == "ParkingLot_V35_Preserved").activeSelf) throw new Exception("Preserved root unexpectedly active");
                var source = Lightmapping.lightingSettings;
                report.AppendLine("BEFORE LightingSettings=" + AssetDatabase.GetAssetPath(source) + " directionality=" + source.directionalityMode);
                foreach (var l in pilot.GetComponentsInChildren<Light>()) report.AppendLine("Light=" + l.name + " type=" + l.type + " mode=" + l.lightmapBakeType + " shadows=" + l.shadows + " intensity=" + l.intensity + " rotation=" + l.transform.eulerAngles);
                // Preserve the reviewed bake mode and geometry; compare direct/environment balance only.
                Directory.CreateDirectory(Owned); AssetDatabase.Refresh();
                var settings = UnityEngine.Object.Instantiate(source);
                settings.name = "ParkingLot_DirectionalCPU";
                settings.autoGenerate = false;
                settings.directionalityMode = source.directionalityMode;
                AssetDatabase.CreateAsset(settings, Owned + "/ParkingLot_DirectionalCPU.lighting");
                Lightmapping.lightingSettings = settings;
                report.AppendLine("Before ambientIntensity=" + RenderSettings.ambientIntensity);
                RenderSettings.ambientIntensity = .45f;
                foreach (var light in pilot.GetComponentsInChildren<Light>())
                {
                    if (light.type != LightType.Directional) continue;
                    light.intensity = 2.4f;
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    Persist(light.transform); Persist(light);
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Owned + "/DirectionalBakeWork.unity");
                report.AppendLine("Whole-frame trial: sun intensity2.4, environment intensity.45. Sun orientation, color, bake mode, geometry, materials and camera preserved. Resolution=" + settings.lightmapResolution + " maxSize=" + settings.lightmapMaxSize + " samples=" + settings.directSampleCount + "/" + settings.indirectSampleCount + "/" + settings.environmentSampleCount);
                File.WriteAllText(output + "/sun-contrast-report.txt", report.ToString());
                started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
                if (!Lightmapping.BakeAsync()) throw new Exception("Directional bake rejected");
            }
            catch (Exception e) { Fail(e); }
        }
        static void Tick()
        {
            try
            {
                Check(); double elapsed = EditorApplication.timeSinceStartup - started;
                if (elapsed > 480) throw new Exception("Directional bake exceeded 480 seconds");
                if (elapsed < 5 || Lightmapping.isRunning) return;
                EditorApplication.update -= Tick;
                if (LightmapSettings.lightmaps.Length == 0 || LightmapSettings.lightmaps.Any(m => m.lightmapColor == null)) throw new Exception("Missing directional lightmap data");
                foreach (var map in LightmapSettings.lightmaps) report.AppendLine("MAP color=" + AssetDatabase.GetAssetPath(map.lightmapColor) + " direction=" + (map.lightmapDir==null?"none":AssetDatabase.GetAssetPath(map.lightmapDir)) + " size=" + map.lightmapColor.width + "x" + map.lightmapColor.height);
                foreach (var p in pilot.GetComponentsInChildren<ReflectionProbe>())
                {
                    string reflection = Owned + "/DirectionalReflection.exr";
                    if (!Lightmapping.BakeReflectionProbe(p, reflection)) throw new Exception("Reflection bake failed");
                    AssetDatabase.ImportAsset(reflection, ImportAssetOptions.ForceSynchronousImport);
                    p.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                    p.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(reflection); Persist(p);
                }
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
                AssetDatabase.SaveAssets();
                report.AppendLine("Saved target SHA256=" + Hash(ScenePath) + " bakeSeconds=" + elapsed + " nativeShadows=" + QualitySettings.shadows + " nativeAA=" + QualitySettings.antiAliasing);
                report.AppendLine("Actual whole-frame A/B and cold reopen still required; not a photoreal completion claim.");
                File.WriteAllText(output + "/sun-contrast-report.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail(e); }
        }
        static void Fail(Exception e)
        {
            EditorApplication.update -= Tick; Debug.LogException(e);
            if (output != null) File.WriteAllText(output + "/FAILED.txt", e.ToString());
            EditorApplication.Exit(1);
        }
    }
}
