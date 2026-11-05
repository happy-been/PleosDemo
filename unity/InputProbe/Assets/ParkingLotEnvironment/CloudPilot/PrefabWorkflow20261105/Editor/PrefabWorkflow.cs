using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    public static class PrefabWorkflow
    {
        private const string Folder = "Assets/ParkingLotEnvironment/CloudPilot/PrefabWorkflow20261105";
        private const string EditableScene = Folder + "/ParkingLot_Editable.unity";
        private const string ReviewScene = "Assets/ParkingLotEnvironment/CloudPilot/WholeMapFinish20261105/WholeMapReview.unity";
        private const string RootName = "ParkingLot_CornerPilot_Root";

        public static void Run()
        {
            RunBatch(output =>
            {
                if (File.Exists(EditableScene))
                    throw new InvalidOperationException("The editable scene already exists.");

                var scene = EditorSceneManager.OpenScene(ReviewScene);
                var root = FindRoot(scene);
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(child.gameObject))
                        PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                int rendererCount = root.GetComponentsInChildren<Renderer>(true).Length;
                var prefabs = new List<string>();
                CreatePrefabFolders();
                foreach (Transform child in root.transform)
                    ConvertHierarchy(child, prefabs);

                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, EditableScene))
                    throw new IOException("Could not save the editable scene.");

                root = FindRoot(EditorSceneManager.OpenScene(EditableScene));
                if (root.GetComponentsInChildren<Renderer>(true).Length != rendererCount)
                    throw new InvalidOperationException("Renderer count changed during conversion.");

                ContextBuilder.Validate(root, output);
                File.WriteAllLines(Path.Combine(output, "prefabs.txt"), prefabs);
                Capture(root.GetComponentInChildren<Camera>(), Path.Combine(output, "editable-preview.png"));
                File.WriteAllText(Path.Combine(output, "complete.txt"), $"Saved and reopened {prefabs.Count} prefabs and {rendererCount} renderers.");
            });
        }

        public static void ExportForMerge()
        {
            RunBatch(output =>
            {
                var root = FindRoot(EditorSceneManager.OpenScene(EditableScene));
                ContextBuilder.Validate(root, output);
                var paths = AssetDatabase.GetDependencies(EditableScene, true)
                    .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                var package = Path.Combine(output, "ParkingLot_Editable.unitypackage");
                AssetDatabase.ExportPackage(paths, package, ExportPackageOptions.Default);
                if (!File.Exists(package) || new FileInfo(package).Length == 0)
                    throw new IOException("Unity did not produce the merge package.");
                File.WriteAllLines(Path.Combine(output, "asset-paths.txt"), paths);
                Debug.Log($"Exported editable parking lot with {paths.Length} assets.");
            });
        }

        private static void RunBatch(Action<string> action)
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-collabOutput");
                if (index < 0 || index + 1 >= args.Length)
                    throw new ArgumentException("Pass -collabOutput followed by an output directory.");

                string output = Path.GetFullPath(args[index + 1]);
                Directory.CreateDirectory(output);
                action(output);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static GameObject FindRoot(Scene scene) =>
            scene.GetRootGameObjects().Single(root => root.name == RootName);

        private static void CreatePrefabFolders()
        {
            foreach (string category in new[] { "Vehicles", "Buildings", "Environment", "Lighting" })
                Directory.CreateDirectory($"{Folder}/Prefabs/{category}");
            AssetDatabase.Refresh();
        }

        private static void ConvertHierarchy(Transform item, List<string> prefabs)
        {
            var obj = item.gameObject;
            bool vehicle = item.name.StartsWith("Parked_", StringComparison.Ordinal);
            bool lighting = obj.GetComponent<Light>() || obj.GetComponent<ReflectionProbe>();
            bool contextBuilding = item.name.Contains("ContextBuilding") ||
                (item.parent.name == "ParkingContext_ApprovedKitExtension" &&
                 item.GetComponentsInChildren<Renderer>(true).Length > 1);

            if (!vehicle && !lighting && !contextBuilding && !obj.GetComponent<Renderer>())
            {
                foreach (Transform child in item)
                    ConvertHierarchy(child, prefabs);
                return;
            }

            string category = "Environment";
            if (vehicle)
                category = "Vehicles";
            else if (item.name.Contains("Building") || item.GetComponentsInChildren<Renderer>(true).Length > 8)
                category = "Buildings";
            else if (lighting)
                category = "Lighting";

            string name = new string(item.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            string path = $"{Folder}/Prefabs/{category}/{prefabs.Count:D4}_{name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(obj, path, InteractionMode.AutomatedAction, out bool saved);
            if (!saved)
                throw new IOException($"Could not save {path}");
            prefabs.Add(path);
        }

        private static void Capture(Camera camera, string path)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                var eye = new Vector3(.7f, 1.65f, -17.5f);
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(Vector3.forward));
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
