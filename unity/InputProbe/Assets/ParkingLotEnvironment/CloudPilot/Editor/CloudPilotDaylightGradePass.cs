using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    public static class CloudPilotDaylightGradePass
    {
        const string S = "Assets/_Parkside/Scenes/ParklingLot.unity";
        const string W = "Assets/ParkingLotEnvironment/CloudPilot/DaylightGrade";
        const string Expected = "b3ef89591b2b362d18cfe3a950e24255f7843d1b4b8455856fcaffcd563d60b4";
        static string Hash(string p) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant(); }
        public static void Apply()
        {
            string output = null;
            try
            {
                if (File.Exists("/workspaces/parkside-setup/STOP")) throw new Exception("STOP");
                if (Hash(S) != Expected) throw new Exception("Accepted scene changed");
                if (QualitySettings.shadows != ShadowQuality.Disable || QualitySettings.antiAliasing != 0) throw new Exception("Unexpected global quality");
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-parkingGradeOutput");
                if (i < 0 || i + 1 >= args.Length) throw new Exception("Missing output");
                output = Path.GetFullPath(args[i + 1]); Directory.CreateDirectory(output);
                EditorSceneManager.OpenScene(S);
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                var pilot = roots.Single(x => x.name == "ParkingLot_CornerPilot_Root");
                if (roots.Single(x => x.name == "ParkingLot_V35_Preserved").activeSelf) throw new Exception("Preserved root active");
                var camera = pilot.GetComponentInChildren<Camera>();
                if (camera.GetComponent<MapDaylightGrade>() != null) throw new Exception("Single-use grade already attached");
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(W + "/MapDaylightGrade.shader");
                if (shader == null || !shader.isSupported) throw new Exception("Grade shader unavailable");
                var lighting = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                var grade = camera.gameObject.AddComponent<MapDaylightGrade>();
                grade.gradeShader = shader; grade.exposureEV = 1; grade.contrast = 1.08f; grade.saturation = 1.12f;
                EditorUtility.SetDirty(grade);
                if (PrefabUtility.IsPartOfPrefabInstance(grade)) PrefabUtility.RecordPrefabInstancePropertyModifications(grade);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), S); AssetDatabase.SaveAssets();
                if (lighting != AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)) throw new Exception("Lighting data changed");
                var report = new StringBuilder();
                report.AppendLine("Single whole-frame color/contrast candidate,actual saved camera component; no package or global project edits.");
                report.AppendLine("Hill ACES-fitted tone curve; exposureEV1,linear contrast1.08 around.18,saturation1.12. Gamma/linear conversion is internal to this camera shader; project colorSpace unchanged.");
                report.AppendLine("Camera allowHDR=" + camera.allowHDR + " FOV=" + camera.fieldOfView + " position=" + camera.transform.position);
                foreach (var b in camera.GetComponents<MonoBehaviour>()) report.AppendLine("CameraComponent=" + b.GetType().FullName + " enabled=" + b.enabled);
                report.AppendLine("LightingData=" + lighting + "; lights,reflections,geometry,materials,camera pose,FXAA unchanged; known-bad contact AO stays disabled.");
                report.AppendLine("Whole-frame comparison required; grade cannot establish photographic fidelity or repair model quality.");
                report.AppendLine("SHA256=" + Hash(S));
                File.WriteAllText(output + "/daylight-grade-report.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e); if (output != null) File.WriteAllText(output + "/FAILED.txt", e.ToString()); EditorApplication.Exit(1);
            }
        }
    }
}
