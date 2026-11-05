using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    public static class CloudPilotVehicleSunPass
    {
        const string ScenePath = "Assets/_Parkside/Scenes/ParklingLot.unity";
        const string Kit = "Assets/ParkingLotEnvironment/CloudPilot";
        const string Owned = Kit + "/VehicleSunLighting";
        const string Expected = "f5d1deb3a93f0521246b40c0e05f22cd312ed0f60912c5ff3bcb1b9de5abbbb2";
        static string output;
        static GameObject pilot;
        static double started;
        static readonly StringBuilder report = new StringBuilder();

        static string Hash(string path)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        static void Check()
        {
            if (File.Exists("/workspaces/parkside-setup/STOP")) throw new Exception("STOP");
            if (QualitySettings.shadows != ShadowQuality.Disable || QualitySettings.antiAliasing != 0) throw new Exception("Unexpected global quality");
        }
        static void Persist(UnityEngine.Object o)
        {
            EditorUtility.SetDirty(o);
            if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
        }
        public static void ApplyAndBake()
        {
            try
            {
                Check();
                if (Hash(ScenePath) != Expected) throw new Exception("Accepted scene changed");
                if (Directory.Exists(Owned)) throw new Exception("Single-use lighting candidate already exists");
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-parkingVehicleSunOutput");
                if (i < 0 || i + 1 >= args.Length) throw new Exception("Missing output");
                output = Path.GetFullPath(args[i + 1]); Directory.CreateDirectory(output);
                EditorSceneManager.OpenScene(ScenePath);
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                pilot = roots.Single(x => x.name == "ParkingLot_CornerPilot_Root");
                if (roots.Single(x => x.name == "ParkingLot_V35_Preserved").activeSelf) throw new Exception("Preserved root active");
                var source = Lightmapping.lightingSettings;
                report.AppendLine("BEFORE LightingSettings=" + AssetDatabase.GetAssetPath(source) + " mode=" + source.mixedBakeMode);
                report.AppendLine("Global shadows=" + QualitySettings.shadows + " shadowmask=" + QualitySettings.shadowmaskMode + " pixelLights=" + QualitySettings.pixelLightCount + "; unchanged throughout.");
                Directory.CreateDirectory(Owned); AssetDatabase.Refresh();
                var settings = UnityEngine.Object.Instantiate(source);
                settings.name = "ParkingLot_MixedShadowmask";
                settings.autoGenerate = false;
                settings.mixedBakeMode = MixedLightingMode.Shadowmask;
                AssetDatabase.CreateAsset(settings, Owned + "/ParkingLot_MixedShadowmask.lighting");
                Lightmapping.lightingSettings = settings;
                var sun = pilot.GetComponentsInChildren<Light>().Single(l => l.type == LightType.Directional);
                report.AppendLine("BEFORE Sun mode=" + sun.lightmapBakeType + " intensity=" + sun.intensity + " renderMode=" + sun.renderMode + " angle=" + sun.transform.eulerAngles);
                sun.lightmapBakeType = LightmapBakeType.Mixed;
                sun.renderMode = LightRenderMode.ForcePixel;
                Persist(sun);
                report.AppendLine("AFTER Sun mode=Mixed,renderMode=ForcePixel; unchanged intensity,color,angle,ambient,camera,reflection,geometry,materials,layers,static flags.");
                report.AppendLine("Hypothesis: live direct lighting restores vehicle form/specular response while shadowmask preserves environment shadows. Must evaluate actual whole-frame result; disabled global shadows may limit behavior.");
                report.AppendLine("Sources: https://docs.unity3d.com/2022.3/Documentation/Manual/LightMode-Mixed-Shadowmask.html ; https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Light-renderMode.html");
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Owned + "/VehicleSunBakeWork.unity");
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
                if (!Lightmapping.BakeAsync()) throw new Exception("Bake rejected");
            }
            catch (Exception e) { Fail(e); }
        }
        public static void RebalanceAndBake()
        {
            try
            {
                Check();
                if (Hash(ScenePath) != "594ba149a6028340be8a5c005fc2a00bf12d5bb878676b9b40df4f6395dc77f9") throw new Exception("Initial candidate changed");
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-parkingVehicleSunOutput");
                if (i < 0 || i + 1 >= args.Length) throw new Exception("Missing output");
                output = Path.GetFullPath(args[i + 1]);
                report.Append(File.ReadAllText(output + "/vehicle-sun-report.txt"));
                EditorSceneManager.OpenScene(ScenePath);
                pilot = SceneManager.GetActiveScene().GetRootGameObjects().Single(x => x.name == "ParkingLot_CornerPilot_Root");
                if (AssetDatabase.GetAssetPath(Lightmapping.lightingSettings) != Owned + "/ParkingLot_MixedShadowmask.lighting") throw new Exception("Unexpected owned lighting");
                var sun = pilot.GetComponentsInChildren<Light>().Single(l => l.type == LightType.Directional);
                if (sun.lightmapBakeType != LightmapBakeType.Mixed || Mathf.Abs(sun.intensity - 2.4f) > .001f) throw new Exception("Unexpected candidate sun");
                sun.intensity = .8f; Persist(sun);
                report.AppendLine("Whole-frame balance correction: initial Mixed2.4 was heavily overexposed. Reduce sun to.8 and rebake indirect lighting consistently; same atmosphere,materials,camera and geometry. No further per-material tuning.");
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), Owned + "/VehicleSunBakeWork.unity");
                started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
                if (!Lightmapping.BakeAsync()) throw new Exception("Balance bake rejected");
            }
            catch (Exception e) { Fail(e); }
        }
        static void Tick()
        {
            try
            {
                Check(); double elapsed = EditorApplication.timeSinceStartup - started;
                if (elapsed > 480) throw new Exception("Bake timeout");
                if (elapsed < 5 || Lightmapping.isRunning) return;
                EditorApplication.update -= Tick;
                if (LightmapSettings.lightmaps.Length == 0 || !LightmapSettings.lightmaps.Any(m => m.shadowMask != null)) throw new Exception("No baked shadowmask");
                foreach (var m in LightmapSettings.lightmaps) report.AppendLine("Lightmap=" + AssetDatabase.GetAssetPath(m.lightmapColor) + " shadowmask=" + AssetDatabase.GetAssetPath(m.shadowMask));
                foreach (var p in pilot.GetComponentsInChildren<ReflectionProbe>())
                    if (p.mode != ReflectionProbeMode.Custom || AssetDatabase.GetAssetPath(p.customBakedTexture) != Kit + "/SunContrastLighting/DirectionalReflection.exr") throw new Exception("Accepted reflection changed");
                var sun = pilot.GetComponentsInChildren<Light>().Single(l => l.type == LightType.Directional);
                report.AppendLine("BakingOutput mode=" + sun.bakingOutput.lightmapBakeType + " mixed=" + sun.bakingOutput.mixedLightingMode + " occlusionChannel=" + sun.bakingOutput.occlusionMaskChannel);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
                AssetDatabase.SaveAssets();
                report.AppendLine("Bake seconds=" + elapsed + " SHA256=" + Hash(ScenePath));
                File.WriteAllText(output + "/vehicle-sun-report.txt", report.ToString());
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
