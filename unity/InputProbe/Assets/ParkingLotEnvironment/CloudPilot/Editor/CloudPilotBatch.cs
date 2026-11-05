using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Explicit batch entry point only; loading this file never starts a build.
public static class CloudPilotBatch {
    const string ScenePath = "Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Kit = "Assets/ParkingLotEnvironment/CloudPilot";
    static string captureDirectory;
    static double started;

    public static void BuildAndCapture() {
        try {
            if (!Application.isBatchMode) throw new InvalidOperationException("This entry point is for an explicit batch run.");
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("A real graphics device is required; do not pass -nographics.");
            string stop = Environment.GetEnvironmentVariable("PARKSIDE_STOP_FILE");
            if (!string.IsNullOrEmpty(stop) && File.Exists(stop)) throw new OperationCanceledException("Stop file exists.");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject pilot = FindRoot("ParkingLot_CornerPilot_Root");
            if (pilot == null) {
                if (!File.Exists(Kit + "/PilotCorner.prepared.prefab")) CloudPilotPreparation.PreparePrefab();
                CloudPilotInspection.Apply();
                pilot = FindRoot("ParkingLot_CornerPilot_Root");
            }
            if (pilot == null || !pilot.activeInHierarchy) throw new InvalidOperationException("Saved pilot must be active.");
            if (pilot.transform.Find("EXISTING_VEHICLES_UNAPPROVED") == null) {
                CloudPilotInspection.AddComparisonVehicles();
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath))
                    throw new IOException("Pilot scene save failed.");
            }
            CloudPilotInspection.VerifyReopen();
            string output = Path.GetFullPath(Kit + "/Inspection~");
            string[] before = Directory.GetDirectories(output, "unity-*");
            CloudPilotInspection.Capture();
            string[] created = Directory.GetDirectories(output, "unity-*").Except(before).ToArray();
            if (created.Length != 1) throw new InvalidOperationException("Expected one new capture directory.");
            captureDirectory = created[0];
            started = EditorApplication.timeSinceStartup;
            File.WriteAllText(Path.Combine(captureDirectory, "graphics-device.txt"),
                "Unity=" + Application.unityVersion + "\nRenderer=" + SystemInfo.graphicsDeviceName +
                "\nAPI=" + SystemInfo.graphicsDeviceVersion + "\nOfflineCapture=true\nRealtimeFPS=NOT_MEASURED\n");
            EditorApplication.update += Monitor;
        } catch (Exception error) { Fail(error); }
    }

    static GameObject FindRoot(string name) {
        return SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(root => root.name == name);
    }
    static void Monitor() {
        try {
            string stop = Environment.GetEnvironmentVariable("PARKSIDE_STOP_FILE");
            if (!string.IsNullOrEmpty(stop) && File.Exists(stop)) throw new OperationCanceledException("User stop file detected.");
            string failure = Path.Combine(captureDirectory, "CAPTURE_FAILED.txt");
            if (File.Exists(failure)) throw new IOException(File.ReadAllText(failure));
            if (File.Exists(Path.Combine(captureDirectory, "CAPTURE_COMPLETE.txt"))) {
                if (Directory.GetFiles(captureDirectory, "*.png").Length != 122)
                    throw new IOException("Capture marker exists but the image count is incorrect.");
                EditorApplication.update -= Monitor;
                Debug.Log("PARKSIDE_BATCH_COMPLETE " + captureDirectory);
                EditorApplication.Exit(0);
                return;
            }
            if (EditorApplication.timeSinceStartup - started > 1200)
                throw new TimeoutException("Capture exceeded the 20 minute limit.");
        } catch (Exception error) { Fail(error); }
    }
    static void Fail(Exception error) {
        EditorApplication.update -= Monitor;
        CloudPilotInspection.Release();
        Debug.LogException(error);
        EditorApplication.Exit(1);
    }
}
}
