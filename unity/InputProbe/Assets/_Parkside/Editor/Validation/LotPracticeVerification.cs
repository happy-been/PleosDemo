using System;
using System.IO;
using System.Text;
using Parkside.Cameras;
using Parkside.Core;
using Parkside.Input;
using Parkside.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Parkside.Editor
{
    /// <summary>
    /// Play-mode check of the large-lot practice: the lot loads around the rig, the car drives on its asphalt,
    /// a parked car stops it, and driver/chase pictures are saved. Results go to build/ui-preview/lot-practice.
    /// The car is driven through VehicleController.Simulate with a fixed 50 Hz clock (keyboard input is not used).
    /// </summary>
    [InitializeOnLoad]
    public static class LotPracticeVerification
    {
        private const string Key = "ParksideLotCheck";
        private static int stage, frame;
        private static double deadline;
        private static readonly StringBuilder log = new StringBuilder();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview/lot-practice"));

        static LotPracticeVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                stage = 0; frame = 0; log.Clear(); deadline = EditorApplication.timeSinceStartup + 120;
                EditorApplication.update += Tick;
            };
        }

        public static string StartInEditor()
        {
            if (EditorApplication.isPlaying) return "ERR stop Play first";
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(ParksideMenuAuthoring.LotPracticePath);
            EditorApplication.isPlaying = true;
            return "started; result in " + Output + "/result.txt";
        }

        /// <summary>Diagnostics: the colliders a car body would touch at a few poses in the lot (edit mode).</summary>
        public static string ProbeLot()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Parkside/Scenes/ParklingLot.unity", OpenSceneMode.Single);
            Physics.SyncTransforms();
            var report = new StringBuilder();
            void Probe(string label, Vector3 at, float yaw)
            {
                var rotation = Quaternion.Euler(0, yaw, 0);
                var hits = Physics.OverlapBox(at + Vector3.up * 0.85f, new Vector3(1.09f, 0.5f, 2.32f), rotation, ~0, QueryTriggerInteraction.Collide);
                report.Append(label).Append(": ");
                foreach (var hit in hits) report.Append(hit.name).Append(hit.isTrigger ? "[trigger]" : "").Append(hit.enabled ? "" : "[off]").Append(' ').Append(hit.bounds.size.ToString("0.0")).Append("; ");
                report.Append(" | ");
            }
            Probe("aisle-15.6", new Vector3(-15.6f, 0, -10), 90);
            Probe("aisle-10", new Vector3(-10f, 0, -10), 90);
            Probe("bay(-12.6,-3)", new Vector3(-12.6f, 0, -3), 0);
            Probe("bay(-7,-3)", new Vector3(-7f, 0, -3), 0);
            var vehicles = 0; var vehicleColliders = 0; var triggers = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("_bay_") && t.parent != null && t.parent.name.StartsWith("04_"))
                    {
                        vehicles++;
                        var c = t.GetComponent<Collider>();
                        if (c != null) { vehicleColliders++; if (c.isTrigger) triggers++; if (vehicles <= 2) report.Append($"{t.name} collider {c.GetType().Name} enabled={c.enabled} active={t.gameObject.activeInHierarchy} bounds={c.bounds.center.ToString("0.0")}/{c.bounds.size.ToString("0.0")} | "); }
                    }
            report.Append($"bayVehicles={vehicles} withCollider={vehicleColliders} triggers={triggers}");
            return report.ToString();
        }

        /// <summary>Diagnostics: a top-down picture of ParklingLot and its active top-level groups with collider counts.</summary>
        public static string TopView()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Parkside/Scenes/ParklingLot.unity", OpenSceneMode.Single);
            var report = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
                foreach (Transform group in root.transform)
                {
                    if (!group.gameObject.activeInHierarchy) { report.Append(group.name).Append("[off]; "); continue; }
                    var colliders = group.GetComponentsInChildren<Collider>(false);
                    var bounds = new Bounds(); var first = true;
                    foreach (var c in colliders) { if (first) { bounds = c.bounds; first = false; } else bounds.Encapsulate(c.bounds); }
                    report.Append($"{group.name} colliders={colliders.Length}" + (first ? "" : $" box={bounds.min.ToString("0")}..{bounds.max.ToString("0")}") + "; ");
                }
            Directory.CreateDirectory(Output);
            var go = new GameObject("TopViewCamera"); var camera = go.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 30; camera.aspect = 1;
            go.transform.SetPositionAndRotation(new Vector3(0, 80, 0), Quaternion.Euler(90, 0, 0));
            var target = new RenderTexture(1200, 1200, 24); camera.targetTexture = target; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(1200, 1200, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Output, "lot-top.png"), texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(go);
            EditorSceneManager.OpenScene("Assets/_Parkside/Scenes/ParklingLot.unity", OpenSceneMode.Single); // discard the temporary camera
            return report.ToString();
        }

        private static void Drive(VehicleController car, float throttle, float steer, float seconds)
        {
            var input = new VehicleControlInput(steer, throttle, throttle > 0f ? 0f : 1f);
            for (var t = 0f; t < seconds; t += 0.02f) car.Simulate(input, true, 0.02f);
        }

        private static void Tick()
        {
            try
            {
                var environment = Object.FindObjectOfType<AdditiveEnvironment>();
                var car = Object.FindObjectOfType<VehicleController>();
                var view = Object.FindObjectOfType<VehicleCamera>();
                var game = Object.FindObjectOfType<GameManager>();
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException($"timed out at stage {stage} frameCount={Time.frameCount} waitFrame={frame} environment={(environment == null ? "none" : environment.Ready.ToString())} car={car != null} view={view != null} scenes={SceneManager.sceneCount}");
                if (environment == null || car == null || view == null || !environment.Ready) return;
                if (++frame < 3) return; // editor ticks, not game frames: a hidden or minimised editor stops rendering frames
                frame = 0;
                switch (stage)
                {
                    case 0:
                        if (game != null) game.enabled = false; // its own update would drive with the keyboard
                        var receiver = Object.FindObjectOfType<VehicleInputReceiver>();
                        if (receiver != null) receiver.enabled = false;
                        log.Append($"activeScene={SceneManager.GetActiveScene().name} scenes={SceneManager.sceneCount} ");
                        log.Append($"cameras={Object.FindObjectsOfType<Camera>().Length} listeners={Object.FindObjectsOfType<AudioListener>().Length} ");
                        log.Append($"spawn={car.transform.position.ToString("0.0")} ");
                        log.Append($"groundUnderSpawn={Physics.Raycast(car.transform.position + Vector3.up * 2f, Vector3.down, 4f)} ");
                        view.SetView(2); stage = 1; break;
                    case 1: Shot(view, "spawn-driver"); view.SetView(0); stage = 2; break;
                    case 2:
                        Shot(view, "spawn-chase");
                        var start = car.transform.position;
                        Drive(car, 1f, 0f, 6f);
                        log.Append($"straight6s={Vector3.Distance(start, car.transform.position):0.0}m y={car.transform.position.y:0.00} blocked={car.Blocked} ");
                        Drive(car, 1f, -12f, 3f); // a gentle left turn towards the T bays
                        log.Append($"curve={car.transform.position.ToString("0.0")} yaw={car.transform.eulerAngles.y:0} blocked={car.Blocked} ");
                        stage = 3; break;
                    case 3: Shot(view, "driving-chase"); view.SetView(2); stage = 4; break;
                    case 4:
                        Shot(view, "driving-driver");
                        // From the aisle, head west into the parked blue sedan in the west row (bay centre about x -5.6, z -7.5).
                        car.ResetToPose(new Vector3(0.5f, 0f, -7.5f), Quaternion.Euler(0f, 270f, 0f));
                        Drive(car, 1f, 0f, 6f);
                        var stopX = car.transform.position.x;
                        log.Append($"towardParkedCar stopX={stopX:0.00} blocked={car.Blocked} ");
                        Drive(car, 0f, 0f, 0.5f);
                        car.ToggleDirection(); // reverse back out
                        Drive(car, 1f, 0f, 2f);
                        log.Append($"reversedOut={(car.transform.position.x - stopX):0.0}m ");
                        car.ToggleDirection();
                        car.ResetToPose(new Vector3(0.5f, 0f, -7.5f), Quaternion.Euler(0f, 270f, 0f));
                        Drive(car, 1f, 0f, 6f);
                        view.SetView(0); stage = 5; break;
                    case 5:
                        Shot(view, "stopped-at-parked-car");
                        // The lot's own pedestrian-eye camera pose, rendered through the rig camera (menu preview picture).
                        var hero = Object.FindObjectsOfType<Camera>(true);
                        foreach (var c in hero)
                            if (c.name == "Pilot_PedestrianCamera")
                            {
                                var rig = view.GetComponent<Camera>(); var t = rig.transform;
                                var keep = (t.position, t.rotation, rig.fieldOfView);
                                t.SetPositionAndRotation(c.transform.position, c.transform.rotation); rig.fieldOfView = c.fieldOfView;
                                Shot(view, "lot-hero");
                                t.SetPositionAndRotation(keep.position, keep.rotation); rig.fieldOfView = keep.fieldOfView;
                            }
                        Finish(true); break;
                }
            }
            catch (Exception error) { log.Append("FAIL " + error.Message); Finish(false); }
        }

        private static void Shot(VehicleCamera view, string name)
        {
            Directory.CreateDirectory(Output);
            var camera = view.GetComponent<Camera>();
            // Place the camera for the current view now (VehicleCamera does this in LateUpdate, which needs a rendered frame).
            for (var i = 0; i < 60; i++)
                typeof(VehicleCamera).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(view, null);
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
        }

        private static void Finish(bool pass)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory(Output);
            var text = (pass ? "PARKSIDE_LOT_CHECK_PASS " : "PARKSIDE_LOT_CHECK_FAIL ") + log;
            File.WriteAllText(Path.Combine(Output, "result.txt"), text);
            Debug.Log(text);
            EditorApplication.isPlaying = false;
        }
    }
}
