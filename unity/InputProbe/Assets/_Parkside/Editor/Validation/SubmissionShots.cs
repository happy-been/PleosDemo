using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BusanEnvironment;
using Parkside.Cameras;
using Parkside.Jeju;
using Parkside.UI;
using Parkside.UI.Hud;
using Parkside.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Parkside.Editor
{
    /// <summary>
    /// Pictures for the contest submission at the simulator size (2560x1440): every drive scene opened from the menu,
    /// with the presentation HUD in first person, third person and top view while driving (auto drive in Busan, guided
    /// drive in Jeju and Paris, so speed, traffic and signals show), plus wide shots without the HUD (landmarks, drone
    /// views). Files go to build/submission; a script converts them to JPG for the submission folder.
    /// Needs a visible editor window (game frames and ScreenCapture).
    /// </summary>
    [InitializeOnLoad]
    public static class SubmissionShots
    {
        private const string Key = "ParksideSubmissionShots";
        private static readonly List<Func<bool>> steps = new List<Func<bool>>();
        private static readonly StringBuilder log = new StringBuilder();
        private static int step, wait, shots, framesAtCheck;
        private static double deadline, frameCheckAt, until;
        private static string pending;
        private static Camera heldCamera;
        private static Camera savedJejuView;
        private static float savedFar, savedFov;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/submission"));
        private static readonly string[] Views = { "1인칭", "3인칭", "탑뷰" };

        static SubmissionShots()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                log.Clear(); steps.Clear(); step = 0; wait = 0; shots = 0;
                deadline = EditorApplication.timeSinceStartup + 2400;
                frameCheckAt = EditorApplication.timeSinceStartup + 20; framesAtCheck = Time.frameCount;
                // keep the game running while another app has focus (play session only; the project setting stays off)
                Application.runInBackground = true;
                Plan();
                EditorApplication.update += Tick;
            };
        }

        public static string StartInEditor() => Start("lot,busan,jeju,paris", true);
        /// <summary>Retakes of single scenes keep the other pictures.</summary>
        public static string StartLotBusan() => Start("lot,busan", false);
        public static string StartJejuParis() => Start("jeju,paris", false);
        public static string StartRetake() => Start("lot,busan,paris", false);
        /// <summary>The lot picture used by the menu (home practice card, practice setup): third person, no HUD, the shared car.</summary>
        public static string StartMenuArt() => Start("menuart", false);
        private static bool Has(string section) => SessionState.GetString(Key + "Sections", "").Contains(section);

        private static string Start(string sections, bool clear)
        {
            if (EditorApplication.isPlaying) return "ERR stop Play first";
            Directory.CreateDirectory(Output);
            if (clear) foreach (var old in Directory.GetFiles(Output, "*.png")) File.Delete(old);
            SessionState.SetString(Key + "Sections", sections);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + "Size", GameViewTool.Select(2560, 1440));
            EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            EditorApplication.isPlaying = true;
            return "started; pictures in " + Output;
        }

        // ---------------------------------------------------------------- plan

        private static void Plan()
        {
            if (Has("menuart"))
            {
                Open(m => { m.ShowPractice(); m.ChoosePractice(0); m.SelectThirdPerson(); m.LaunchPractice(); });
                Do(() => { Hud.Target.SetView(1); Hud.SetVisible(false); });
                Frames(20);
                Shot(() => "menu-setup-imgParkingLot");
                Do(() => Hud.SetVisible(true));
                Back();
            }
            if (Has("lot")) Lot();
            if (Has("busan")) Busan();
            if (Has("jeju")) RouteScene(1, "3_제주", false);
            if (Has("paris")) RouteScene(2, "4_파리", true);
        }

        private static void Lot()
        {
            Open(m => { m.ShowPractice(); m.ChoosePractice(0); m.SelectFirstPerson(); m.LaunchPractice(); });
            HudViews("1_연습장_01_입구");
            Do(() => Pose(new Vector3(-0.6f, 0f, -3.4f), 60f, HudGear.R)); // turning to back into T2 (the chase camera stays inside the lot)
            HudViews("1_연습장_02_후진주차_T2");
            Do(() => Pose(new Vector3(4.3f, 0f, -5.2f), 0f, HudGear.R)); // lining up for P1
            HudViews("1_연습장_03_평행주차_P1", 1, 2);
            Do(() => Pose(new Vector3(2f, 0f, -17f), 0f, HudGear.D));
            Wide("1_연습장_04_전경_보행자시점", () => { var c = Object.FindObjectsOfType<Camera>(true).FirstOrDefault(x => x.name == "Pilot_PedestrianCamera"); return c != null ? (c.transform.position, c.transform.position + c.transform.forward * 30f, c.fieldOfView) : (new Vector3(9f, 5f, -26f), new Vector3(-2f, 0f, -4f), 55f); });
            Wide("1_연습장_05_전경_위", () => (new Vector3(14f, 22f, -30f), new Vector3(-1f, 0f, -4f), 50f));
            Wide("1_연습장_06_전경_수직", () => (new Vector3(0f, 52f, -5.9f), new Vector3(0f, 0f, -6f), 50f));
            Back();
        }

        private static void Busan()
        {
            // auto drive along the landmark loop
            Open(m => { m.ShowMaps(); m.ChooseMap(0); m.SelectFirstPerson(); m.LaunchDrive(); });
            string[] busan = { "해운대", "광안리", "광안대교", "서면", "터널" };
            for (var i = 0; i < busan.Length; i++)
            {
                var index = i; var name = $"2_부산_{i + 1:00}_{busan[i]}";
                Do(() => { var a = Object.FindObjectOfType<DriveAssist>(); a.JumpTo(a.JumpDistance(index)); if (!a.AutoDriving) a.BeginAutoDrive(); });
                Seconds(7f);
                if (i == busan.Length - 1) { HudViews(name, 0, 1); continue; } // in the tunnel the drone and the top view only see its roof
                HudViews(name);
                Wide(name + "_드론", () => Drone(32f, 55f));
            }
            Wide("2_부산_06_광안대교_전경", () => (new Vector3(6022f, 22f, 2996f), new Vector3(5651f, 45f, 3250f), 60f));
            Wide("2_부산_07_광안대교_전경_높이", () => (new Vector3(6080f, 70f, 2940f), new Vector3(5651f, 30f, 3250f), 55f));
            Wide("2_부산_08_광안대교_전경_망원", () => (new Vector3(6022f, 22f, 2996f), new Vector3(5651f, 45f, 3250f), 30f));
            Wide("2_부산_09_광안대교_전경_망원_높이", () => (new Vector3(6080f, 70f, 2940f), new Vector3(5651f, 30f, 3250f), 28f));
            Back();
        }

        private static void RouteScene(int map, string prefix, bool paris)
        {
            Open(m => { m.ShowMaps(); m.ChooseMap(map); m.SelectFirstPerson(); m.LaunchDrive(); });
            var count = paris ? 9 : 10;
            for (var i = 0; i < count; i++)
            {
                var index = i;
                var name = "";
                Do(() =>
                {
                    var d = Object.FindObjectOfType<JejuDriveController>();
                    var station = d.route.stations[Mathf.Min(index, d.route.stations.Length - 1)];
                    name = $"{prefix}_{index + 1:00}_{Safe(station.name)}";
                    Hud.Target.SetView(1);
                    d.JumpToStation(index);
                    d.guidedDrive = !station.branchSpawn; // branch stations are off the main route: the car waits there
                });
                Seconds(7f);
                var top = index % 2 == 0;
                HudViewsNamed(() => name, top ? new[] { 0, 1, 2 } : new[] { 0, 1 });
                Wide(() => name + "_드론", () => Drone(26f, 46f));
            }
            if (paris)
            {
                // landmark wide shots around the three biggest monuments (near their stations so the city around is loaded)
                Landmark(0, "Tour_Eiffel_330m", "4_파리_20_에펠탑_전경");
                Landmark(5, "Arc_de_Triomphe", "4_파리_21_개선문_전경", 1.9f); // a short monument: step further back
                Landmark(7, "Place_de_la_Concorde", "4_파리_22_콩코르드광장_전경");
            }
            else
            {
                Do(() => { var d = Object.FindObjectOfType<JejuDriveController>(); d.JumpToStation(1); d.guidedDrive = true; });
                Seconds(5f);
                Wide("3_제주_20_한라산_전경", () =>
                {
                    var mountain = GameObject.Find("Real_DEM_Hallasan_Backdrop");
                    var car = Hud.Target.Car;
                    var look = mountain != null ? Bounds(mountain).center : car.position + car.forward * 2000f;
                    var from = car.position + Vector3.up * 45f - (look - car.position).normalized * 70f;
                    return (from, look, 50f);
                });
            }
            Do(() => Object.FindObjectOfType<JejuDriveController>().guidedDrive = false);
            Back();
        }

        private static void Landmark(int station, string objectName, string prefix, float scale = 1f)
        {
            Do(() => { var d = Object.FindObjectOfType<JejuDriveController>(); d.JumpToStation(station); d.guidedDrive = false; });
            Frames(30);
            float[] yaws = { 25f, 115f, 205f, 295f };
            for (var i = 0; i < yaws.Length; i++)
            {
                var yaw = yaws[i];
                Wide($"{prefix}_{i + 1}", () =>
                {
                    var go = GameObject.Find(objectName);
                    if (go == null) throw new InvalidOperationException("no landmark " + objectName);
                    var b = Bounds(go);
                    var height = b.size.y; var width = Mathf.Max(b.size.x, b.size.z);
                    var radius = Mathf.Max(height * 1.3f, width * 0.9f, 60f) * scale;
                    var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    var look = new Vector3(b.center.x, b.min.y + height * 0.35f, b.center.z);
                    var from = new Vector3(b.center.x, b.min.y + Mathf.Max(height * 0.35f, radius * 0.25f), b.center.z) + dir * radius;
                    return (from, look, 55f);
                });
            }
        }

        // ---------------------------------------------------------------- step helpers

        private static DriveHud Hud { get { var h = Object.FindObjectOfType<DriveHud>(); return h != null && h.Built ? h : null; } }
        private static ParksideMenu Menu => SceneManager.GetActiveScene().name == "MainMenu" ? Object.FindObjectOfType<ParksideMenu>() : null;
        private static void Do(Action action) => steps.Add(() => { action(); return true; });
        private static void Frames(int n) => steps.Add(() => { if (++wait < n) return false; wait = 0; return true; });
        private static void Seconds(float s) { Do(() => until = EditorApplication.timeSinceStartup + s); steps.Add(() => EditorApplication.timeSinceStartup >= until); }

        private static void Open(Action<ParksideMenu> open)
        {
            steps.Add(() => { var m = Menu; if (m == null) return false; if (++wait < 5) return false; wait = 0; open(m); return true; });
            steps.Add(() => Hud != null);
            Frames(40);
        }

        private static void Back()
        {
            Do(() => Hud.Target.Exit());
            steps.Add(() => Menu != null);
        }

        private static void HudViews(string name, params int[] views) => HudViewsNamed(() => name, views.Length == 0 ? new[] { 0, 1, 2 } : views);

        private static void HudViewsNamed(Func<string> name, int[] views)
        {
            foreach (var v in views)
            {
                var view = v;
                Do(() => Hud.Target.SetView(view));
                Frames(18);
                Shot(() => name() + "_" + Views[view]);
            }
        }

        private static void Wide(string name, Func<(Vector3 from, Vector3 look, float fov)> pose) => Wide(() => name, pose);

        private static void Wide(Func<string> name, Func<(Vector3 from, Vector3 look, float fov)> pose)
        {
            Do(() => Hud.Target.SetView(1)); // third person: the car body is shown
            Frames(6);
            Do(() =>
            {
                Hud.SetVisible(false);
                var camera = TakeCamera();
                var (from, look, fov) = pose();
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from));
                camera.fieldOfView = fov;
            });
            Frames(6);
            Shot(name);
            Do(() => { ReleaseCamera(); Hud.SetVisible(true); });
        }

        private static (Vector3, Vector3, float) Drone(float up, float back)
        {
            var car = Hud.Target.Car;
            var flat = Vector3.ProjectOnPlane(car.forward, Vector3.up).normalized;
            return (car.position + Vector3.up * up - flat * back, car.position + flat * 70f, 55f);
        }

        private static void Pose(Vector3 position, float yaw, HudGear gear)
        {
            var car = Object.FindObjectOfType<VehicleController>();
            car.ResetToPose(position, Quaternion.Euler(0f, yaw, 0f));
            Hud.Target.TrySetGear(gear);
        }

        private static Camera TakeCamera()
        {
            var camera = Hud.Target.View;
            var vehicleCamera = Object.FindObjectOfType<VehicleCamera>();
            if (vehicleCamera != null) vehicleCamera.enabled = false;
            var drive = Object.FindObjectOfType<JejuDriveController>();
            if (drive != null) { savedJejuView = drive.view; drive.view = null; }
            savedFar = camera.farClipPlane; savedFov = camera.fieldOfView;
            camera.orthographic = false;
            camera.farClipPlane = Mathf.Max(savedFar, 8000f);
            heldCamera = camera;
            return camera;
        }

        private static void ReleaseCamera()
        {
            if (heldCamera == null) return;
            heldCamera.farClipPlane = savedFar; heldCamera.fieldOfView = savedFov;
            var vehicleCamera = Object.FindObjectOfType<VehicleCamera>();
            if (vehicleCamera != null) vehicleCamera.enabled = true;
            var drive = Object.FindObjectOfType<JejuDriveController>();
            if (drive != null && savedJejuView != null) { drive.view = savedJejuView; drive.UpdateCamera(true); }
            heldCamera = null;
        }

        private static void Shot(Func<string> name)
        {
            steps.Add(() =>
            {
                pending = Path.Combine(Output, name() + ".png");
                File.Delete(pending);
                ScreenCapture.CaptureScreenshot(pending);
                return true;
            });
            steps.Add(() =>
            {
                if (File.Exists(pending)) { wait = 0; shots++; return true; }
                if (++wait > 300) throw new InvalidOperationException("no picture for " + pending);
                return false;
            });
        }

        private static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static string Safe(string text)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) text = text.Replace(c, ' ');
            return text.Replace('·', '-').Replace(' ', '_').Replace("__", "_").Trim('_');
        }

        // ---------------------------------------------------------------- loop

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException($"timed out at step {step}/{steps.Count}");
                if (frameCheckAt > 0 && EditorApplication.timeSinceStartup > frameCheckAt)
                {
                    if (Time.frameCount - framesAtCheck < 10) throw new InvalidOperationException("game frames are not running (keep the Unity window visible, not minimised)");
                    frameCheckAt = 0;
                }
                while (step < steps.Count && steps[step]()) step++;
                if (step >= steps.Count) Finish(true);
            }
            catch (Exception error) { log.Append("FAIL " + error.Message); Finish(false); }
        }

        private static void Finish(bool pass)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            try { GameViewTool.Restore(SessionState.GetInt(Key + "Size", 0)); } catch (Exception e) { log.Append(" (game view size not restored: " + e.Message + ")"); }
            var text = (pass ? "PARKSIDE_SUBMISSION_SHOTS_PASS " : "PARKSIDE_SUBMISSION_SHOTS_FAIL ") + $"pictures={shots} " + log;
            File.WriteAllText(Path.Combine(Output, "result.txt"), text);
            Debug.Log(text);
            EditorApplication.isPlaying = false;
        }
    }
}
