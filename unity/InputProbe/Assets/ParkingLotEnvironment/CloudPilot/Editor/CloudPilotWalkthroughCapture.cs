using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    // Offline inspection only: never saves the scene or changes global quality settings.
    public static class CloudPilotWalkthroughCapture
    {
        const string ScenePath = "Assets/_Parkside/Scenes/ParklingLot.unity";
        const string Expected = "b3ef89591b2b362d18cfe3a950e24255f7843d1b4b8455856fcaffcd563d60b4";
        const int Width = 1280, Height = 720, FrameCount = 144, Fps = 24;
        static Camera eye;
        static RenderTexture target;
        static Texture2D pixels;
        static RenderTexture oldTarget;
        static Vector3 startPosition;
        static Quaternion startRotation;
        static bool oldEnabled;
        static string output, originalHash;
        static int frame = -3;
        static double started;
        static readonly StringBuilder report = new StringBuilder();
        static readonly StringBuilder poses = new StringBuilder("frame,time_seconds,x,y,z,qx,qy,qz,qw\n");
        static string Hash(string p)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant();
        }
        static void Check()
        {
            if (File.Exists("/workspaces/parkside-setup/STOP")) throw new Exception("STOP");
            if (QualitySettings.shadows != ShadowQuality.Disable || QualitySettings.antiAliasing != 0 || QualitySettings.pixelLightCount != 4) throw new Exception("Unexpected global quality");
            if (EditorApplication.timeSinceStartup - started > 1700) throw new Exception("Walkthrough timeout");
        }
        public static void Capture()
        {
            try
            {
                started = EditorApplication.timeSinceStartup;
                Check(); originalHash = Hash(ScenePath);
                if (originalHash != Expected) throw new Exception("Accepted scene changed");
                var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-parkingWalkthroughOutput");
                if (i < 0 || i + 1 >= args.Length) throw new Exception("Missing output path");
                output = Path.GetFullPath(args[i + 1]); Directory.CreateDirectory(output);
                string frames = Path.Combine(output, "frames");
                if (Directory.Exists(frames)) throw new Exception("Frame output already exists");
                Directory.CreateDirectory(frames);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                var pilot = roots.Single(x => x.name == "ParkingLot_CornerPilot_Root");
                if (roots.Single(x => x.name == "ParkingLot_V35_Preserved").activeSelf || !pilot.activeInHierarchy) throw new Exception("Unexpected scene roots");
                eye = pilot.GetComponentInChildren<Camera>();
                if (eye == null) throw new Exception("No saved camera");
                startPosition = eye.transform.position; startRotation = eye.transform.rotation;
                oldEnabled = eye.enabled; oldTarget = eye.targetTexture; eye.enabled = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                target.Create(); pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                eye.targetTexture = target;
                report.AppendLine("Actual saved-scene Unity camera walkthrough,offline rendered frames; not AI video,real-time FPS or mobile performance evidence.");
                report.AppendLine("Scene=" + ScenePath + " SHA256=" + originalHash);
                report.AppendLine("Unity=" + Application.unityVersion + " GPU=" + SystemInfo.graphicsDeviceName + " API=" + SystemInfo.graphicsDeviceType);
                report.AppendLine("FrameCount=144 Fps=24 DurationSeconds=6 Width=1280 Height=720; no interpolation,upscaling or duplicated frames.");
                report.AppendLine("Camera start=" + startPosition + " FOV=" + eye.fieldOfView + "; path forward3.6m,right1m,yaw-4degrees with smoothstep easing; original eye height retained,no head bob.");
                report.AppendLine("Saved camera,effects,lighting,materials and geometry unchanged; no RenderProbe,no quality override,no scene save.");
                foreach (var b in eye.GetComponents<MonoBehaviour>()) report.AppendLine("CameraComponent=" + (b == null ? "MISSING" : b.GetType().FullName + " enabled=" + b.enabled));
                File.WriteAllText(Path.Combine(output, "walkthrough-report.txt"), report.ToString());
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Finish(e); }
        }
        static void Tick()
        {
            try
            {
                Check();
                if (frame >= FrameCount) { Finish(null); return; }
                float t = Mathf.Max(0, frame) / (float)(FrameCount - 1);
                float eased = t * t * (3 - 2 * t);
                var p = startPosition + new Vector3(eased, 0, 3.6f * eased);
                var q = Quaternion.AngleAxis(-4 * eased, Vector3.up) * startRotation;
                eye.transform.SetPositionAndRotation(p, q);
                eye.Render();
                if (frame >= 0)
                {
                    var previous = RenderTexture.active;
                    try
                    {
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output, "frames", "frame-" + frame.ToString("D4") + ".png"), pixels.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; }
                    poses.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F6},{2:F6},{3:F6},{4:F6},{5:F8},{6:F8},{7:F8},{8:F8}", frame, frame / (double)Fps, p.x, p.y, p.z, q.x, q.y, q.z, q.w));
                    if (frame % 6 == 0 || frame == FrameCount - 1)
                        File.WriteAllText(Path.Combine(output, "progress.json"), "{\"completed_frames\":" + (frame + 1) + ",\"total_frames\":144,\"elapsed_seconds\":" + (EditorApplication.timeSinceStartup - started).ToString("F1", CultureInfo.InvariantCulture) + "}");
                }
                frame++;
            }
            catch (Exception e) { Finish(e); }
        }
        static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (eye != null) { eye.targetTexture = oldTarget; eye.enabled = oldEnabled; eye.transform.SetPositionAndRotation(startPosition, startRotation); }
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (error == null && originalHash != Hash(ScenePath)) error = new Exception("Scene changed during read-only capture");
            if (error == null && Directory.GetFiles(Path.Combine(output, "frames"), "frame-*.png").Length != FrameCount) error = new Exception("Missing frames");
            if (error != null) Debug.LogException(error);
            report.AppendLine("RenderWallSeconds=" + (EditorApplication.timeSinceStartup - started).ToString("F2", CultureInfo.InvariantCulture));
            report.AppendLine("SavedSceneUnchanged=" + (originalHash == Hash(ScenePath)) + " Result=" + (error == null ? "CAPTURE_COMPLETE" : error.ToString()));
            if (output != null)
            {
                File.WriteAllText(Path.Combine(output, "walkthrough-report.txt"), report.ToString());
                File.WriteAllText(Path.Combine(output, "camera-poses.csv"), poses.ToString());
            }
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
