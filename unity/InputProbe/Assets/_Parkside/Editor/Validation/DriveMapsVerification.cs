using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Parkside.Jeju;
using Parkside.UI;
using Parkside.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parkside.Editor
{
    /// <summary>
    /// Play-mode check of the presentation HUD in the four drive scenes, each opened from the menu: the HUD is attached,
    /// the probe-era UI is hidden, the menu's camera choice and the R/N/D gear work, and game-view pictures of the first
    /// person, third person, top view (and the finish dialog) are saved. Then "마치기 → 나가기" returns to the menu.
    /// Results and pictures go to build/ui-preview/drive-hud. Needs a visible editor (game frames and ScreenCapture).
    /// </summary>
    [InitializeOnLoad]
    public static class DriveMapsVerification
    {
        private const string Key = "ParksideDriveHudCheck";
        private static readonly List<Func<bool>> steps = new List<Func<bool>>();
        private static int step, wait, fpsFrames;
        private static float fpsStart;
        private static double deadline, frameCheckAt;
        private static int framesAtCheck;
        private static readonly StringBuilder log = new StringBuilder();
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview/drive-hud"));

        static DriveMapsVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                log.Clear(); steps.Clear(); step = 0; wait = 0; deadline = EditorApplication.timeSinceStartup + 900;
                // keep the game running while another app has focus (play session only; the project setting stays off)
                Application.runInBackground = true;
                Plan();
                frameCheckAt = EditorApplication.timeSinceStartup + 20; framesAtCheck = Time.frameCount;
                EditorApplication.update += Tick;
            };
        }

        public static string StartInEditor()
        {
            if (EditorApplication.isPlaying) return "ERR stop Play first";
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + "Size", GameViewTool.Select(1920, 1080)); // the simulator is 16:9 (2560x1440); the old size is restored at the end
            EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            EditorApplication.isPlaying = true;
            return "started; result in " + Output + "/result.txt";
        }

        public static string Status() => $"step={step}/{steps.Count} frame={Time.frameCount} scene={SceneManager.GetActiveScene().name} hud={(Object.FindObjectOfType<DriveHud>() != null)} built={(Hud != null)} log={log}";

        private static ParksideMenu Menu => SceneManager.GetActiveScene().name == "MainMenu" ? Object.FindObjectOfType<ParksideMenu>() : null;
        private static DriveHud Hud { get { var h = Object.FindObjectOfType<DriveHud>(); return h != null && h.Built ? h : null; } }
        private static void Require(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }
        private static bool Frames(int n) { if (++wait < n) return false; wait = 0; return true; }

        private static void Plan()
        {
            // name, open from the menu, first person chosen in the menu
            var runs = new (string name, Action<ParksideMenu> open, bool first)[]
            {
                ("ParkingLotPractice", m => { m.ShowPractice(); m.ChoosePractice(0); m.SelectFirstPerson(); m.LaunchPractice(); }, true),
                ("BusanLandmarks", m => { m.ShowMaps(); m.ChooseMap(0); m.SelectFirstPerson(); m.LaunchDrive(); }, true),
                ("JejuCoastalDrive", m => { m.ShowMaps(); m.ChooseMap(1); m.SelectThirdPerson(); m.LaunchDrive(); }, false),
                ("ParisEiffelDrive", m => { m.ShowMaps(); m.ChooseMap(2); m.SelectFirstPerson(); m.LaunchDrive(); }, true),
            };
            foreach (var run in runs)
            {
                var r = run;
                steps.Add(() => { var m = Menu; if (m == null || !Frames(5)) return false; log.Append($"[{r.name}] "); r.open(m); return true; });
                steps.Add(() => Hud != null && Frames(20));
                steps.Add(() =>
                {
                    var hud = Hud;
                    var legacy = Object.FindObjectsOfType<RectTransform>().Where(t => t.name == "DebugPanel" || t.name == "BlockReason" || (t.name == "메뉴로" && t.GetComponent<Button>() != null)).Select(t => t.name).ToArray();
                    log.Append($"legacyUi={legacy.Length} ");
                    Require(legacy.Length == 0, r.name + ": probe-era UI still on screen: " + string.Join(",", legacy));
                    var drive = Object.FindObjectOfType<JejuDriveController>();
                    if (drive != null) { log.Append($"jejuOnGui={drive.showHud} externalInput={drive.externalInput} "); Require(!drive.showHud && drive.externalInput, r.name + ": Jeju HUD/input not taken over"); }
                    var expected = r.first ? 0 : 1;
                    log.Append($"view={hud.Target.CurrentView} (expected {expected}) ");
                    Require(hud.Target.CurrentView == expected, r.name + ": menu camera choice not applied");
                    Require(hud.Target.TrySetGear(HudGear.R) && hud.Target.Gear == HudGear.R, r.name + ": gear R at standstill");
                    Require(hud.Target.TrySetGear(HudGear.N) && hud.Target.Gear == HudGear.N, r.name + ": gear N at standstill");
                    Require(hud.Target.TrySetGear(HudGear.D) && hud.Target.Gear == HudGear.D, r.name + ": gear D at standstill");
                    log.Append("gears=RND ");
                    hud.PreviewSteering = 0.1f; // 45° of wheel for the pictures (the hands follow up to 25°)
                    hud.Target.SetView(0);
                    return true;
                });
                foreach (var (view, suffix) in new[] { (0, "1st"), (1, "3rd"), (2, "top") })
                {
                    var v = view; var s = suffix;
                    steps.Add(() => { Hud.Target.SetView(v); return Frames(12); });
                    steps.Add(() => { Capture(r.name + "-" + s); return true; });
                    steps.Add(() => CaptureDone(r.name + "-" + s));
                    if (v < 2) // frame rate with the HUD in this view (editor, so only a rough guide for the device)
                    {
                        steps.Add(() => { fpsStart = Time.realtimeSinceStartup; fpsFrames = Time.frameCount; return true; });
                        steps.Add(() => { if (Time.realtimeSinceStartup - fpsStart < 3f) return false; log.Append($"fps{s}={(Time.frameCount - fpsFrames) / (Time.realtimeSinceStartup - fpsStart):0.0} "); return true; });
                    }
                }
                if (r.name == "ParkingLotPractice")
                {
                    steps.Add(() => { Hud.Target.SetView(0); Hud.OpenDialog(); return Frames(8); });
                    steps.Add(() => { Capture(r.name + "-dialog"); return true; });
                    steps.Add(() => CaptureDone(r.name + "-dialog"));
                }
                // parked-only notice: the Pleos bridge's message (gear not P) and Unity's own check both raise it; other drop-outs only a small tag
                steps.Add(() => { Hud.CloseDialog(); Hud.Target.SetView(0); Hud.PreviewBlock = "AVD를 P단으로 변경하세요"; return true; });
                steps.Add(() => Frames(8));
                steps.Add(() => { Require(Hud.ParkedNoticeShown && !Hud.WaitingShown, r.name + ": parked-only notice for the bridge message"); Capture(r.name + "-parked-only"); return true; });
                steps.Add(() => CaptureDone(r.name + "-parked-only"));
                steps.Add(() => { Hud.PreviewBlock = "Real vehicle must stay in P / 0 speed"; return true; });
                steps.Add(() => Frames(4));
                steps.Add(() => { Require(Hud.ParkedNoticeShown, r.name + ": parked-only notice for Unity's check"); Hud.PreviewBlock = "Vehicle samples are stale"; fpsStart = Time.realtimeSinceStartup; return true; });
                steps.Add(() => Time.realtimeSinceStartup - fpsStart > 2f);
                steps.Add(() => { Require(!Hud.ParkedNoticeShown && Hud.WaitingShown, r.name + ": waiting tag for a drop-out"); Hud.PreviewBlock = null; log.Append("notice=ok "); return true; });
                steps.Add(() => Frames(30));
                steps.Add(() => { Require(!Hud.ParkedNoticeShown && !Hud.WaitingShown, r.name + ": notices cleared (input block: " + (Hud.Target.BlockReason ?? "none") + ")"); return true; });
                steps.Add(() => { var hud = Hud; log.Append($"wheel={hud.WheelAngle:0}° "); hud.PreviewSteering = float.NaN; hud.Target.Exit(); return true; });
                steps.Add(() => { var m = Menu; if (m == null) return false; log.Append($"returned page={m.CurrentPage} | "); return true; });
            }
        }

        private static void Capture(string name)
        {
            Directory.CreateDirectory(Output);
            File.Delete(Path.Combine(Output, name + ".png"));
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, name + ".png"));
        }

        private static bool CaptureDone(string name)
        {
            if (File.Exists(Path.Combine(Output, name + ".png"))) { wait = 0; return true; }
            if (++wait > 200) throw new InvalidOperationException("no game-view picture for " + name + " (is the editor window visible?)");
            return false;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException($"timed out at step {step} scene={SceneManager.GetActiveScene().name}");
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
            try { GameViewTool.Restore(SessionState.GetInt(Key + "Size", 0)); } catch (Exception e) { log.Append("(game view size not restored: " + e.Message + ") "); }
            Directory.CreateDirectory(Output);
            var text = (pass ? "PARKSIDE_DRIVE_HUD_PASS " : "PARKSIDE_DRIVE_HUD_FAIL ") + log;
            File.WriteAllText(Path.Combine(Output, "result.txt"), text);
            Debug.Log(text);
            EditorApplication.isPlaying = false;
        }
    }
}
