using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BusanEnvironment;
using Parkside.Demo;
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
    /// Records the demo video in the editor (contest "시연 영상"): title, menu, parking practice (wheel and hands, gear slider,
    /// reversing into T2, the stop at a parked car, the parked-only notice, finish), Busan, Jeju (a red signal) and Paris.
    /// The car is driven through DemoInput, the same snapshot path the Pleos bridge uses; the game clock is fixed at 30 fps
    /// (Time.captureFramerate) and every frame is saved to build/demo-video/frames, so the video is smooth on any PC.
    /// tools: build/demo-video/make.ps1 is not needed; the frames are joined with ffmpeg afterwards.
    /// </summary>
    [InitializeOnLoad]
    public static class DemoVideo
    {
        private const string Key = "ParksideDemoVideo";
        private static readonly List<Func<bool>> steps = new List<Func<bool>>();
        private static readonly StringBuilder log = new StringBuilder();
        private static int step;
        private static double deadline, frameCheckAt;
        private static int framesAtCheck;
        private static float until, tweenStart, timeoutAt;
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/demo-video"));

        static DemoVideo()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                Application.runInBackground = true;
                Time.captureFramerate = 30;
                log.Clear(); steps.Clear(); step = 0;
                deadline = EditorApplication.timeSinceStartup + 5400;
                frameCheckAt = EditorApplication.timeSinceStartup + 20; framesAtCheck = Time.frameCount;
                var stage = DemoStage.Create();
                stage.FrameFolder = Path.Combine(Output, "frames");
                Plan(SessionState.GetString(Key + "Parts", "all"));
                EditorApplication.update += Tick;
            };
        }

        public static string StartInEditor() => Start("all");
        public static string StartLotOnly() => Start("lot");
        public static string StartMapsOnly() => Start("maps");

        private static string Start(string parts)
        {
            if (EditorApplication.isPlaying) return "ERR stop Play first";
            var frames = Path.Combine(Output, "frames");
            if (Directory.Exists(frames)) Directory.Delete(frames, true);
            Directory.CreateDirectory(frames);
            SessionState.SetBool(Key, true);
            SessionState.SetString(Key + "Parts", parts);
            SessionState.SetInt(Key + "Size", GameViewTool.Select(1920, 1080));
            EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            EditorApplication.isPlaying = true;
            return "started; frames in " + frames;
        }

        // ---------------------------------------------------------------- the video

        private static void Plan(string parts)
        {
            var all = parts == "all";
            if (all) Intro();
            if (all || parts == "lot") Lot();
            if (all || parts == "maps") Maps();
            if (all) Ending();
        }

        private static void Intro()
        {
            steps.Add(() => Menu != null);
            Do(() => { Stage.Title("파킹 트래블", "멈춰 있는 차에서, 새로운 길로", "팀 잠자는 바보들  ·  Pleos Connect 차량용 앱"); Stage.TitleAlpha = 1f; Stage.Recording = true; });
            Hold(3f);
            Tween(0.7f, t => Stage.TitleAlpha = 1f - t);
            Caption("차가 주차 중일 때, 운전 연습과 여행을 해요", 3f);
            Do(() => Menu.ShowPractice());
            Caption("오늘 연습할 주차를 골라요", 2.4f);
            Do(() => { Menu.ChoosePractice(0); Menu.SelectFirstPerson(); });
            Caption("후진 주차 · 큰 골목 주차장 · 1인칭", 2.6f);
            Do(() => { Stage.Caption(null); Stage.Recording = false; });
        }

        private static void Lot()
        {
            Do(() => { var m = Menu; if (m.CurrentPage != 2) { m.ShowPractice(); m.ChoosePractice(0); } m.SelectFirstPerson(); m.LaunchPractice(); });
            WaitHud();
            Do(() =>
            {
                Pose(new Vector3(0.86f, 0f, -9.5f), 0f);
                Hud.Target.TrySetGear(HudGear.D);
                Hud.Target.SetView(0);
                Stage.Input.Set(0f, 0f, 0f);
                Stage.ShowInput = true;
            });
            Hold(1.5f, false);
            Do(() => Stage.Recording = true);
            // the wheel follows the real steering wheel
            Do(() => Stage.Caption("실제 핸들을 돌리면 화면 속 핸들도 함께 돌아요"));
            Tween(3.6f, t => Stage.Input.Set(Mathf.Sin(t * Mathf.PI * 2f) * 0.55f, 0f, 0f));
            // forward past bay T2
            Do(() => Stage.Caption("가속 페달로 천천히 · 연습 칸 T2를 지나쳐요"));
            Do(() => Stage.Input.Set(0f, 0.75f, 0f));
            WaitFor(() => Car.transform.position.z >= 2.3f, 14f, "forward past T2");
            Do(() => Stage.Input.Set(0f, 0f, 1f));
            Hold(0.8f);
            // gear slider D -> R
            Do(() => { Stage.Input.Set(0f, 0f, 0f); Stage.Caption("기어는 슬라이더를 밀어서 R · N · D"); });
            Hold(0.6f);
            Tween(0.8f, t => Hud.DragKnob(Mathf.Lerp(276f, 102f, Smooth(t))));
            Do(() => Hud.ReleaseKnob());
            Hold(1.0f);
            // reverse into T2: full left lock until the car faces east, then straight back
            Do(() => { Stage.Caption("핸들을 끝까지 돌리고 천천히 후진"); Stage.Input.Set(-1f, 0.6f, 0f); });
            Hold(1.6f);
            Do(() => { Hud.Target.SetView(2); Stage.Caption("탑뷰로 칸에 맞춰요"); });
            WaitFor(() => Yaw >= 86f, 14f, "reverse turn");
            Do(() => Stage.Input.Set(0f, 0.5f, 0f));
            WaitFor(() => Car.transform.position.x <= -5.3f || Car.Blocked, 8f, "straight back");
            Do(() => { Stage.Input.Set(0f, 0f, 1f); log.Append($"parked at {Car.transform.position:0.0} yaw {Yaw:0} blocked={Car.Blocked} "); });
            Hold(0.8f);
            Do(() => Stage.Caption("T2 칸 안에 반듯하게 세웠어요")); // stays in the top view: behind the car (east-facing) is a building
            Hold(2.4f);
            // parked car: the body stops at it
            Do(() => Stage.Recording = false);
            Do(() => { Stage.Input.Set(0f, 0f, 0f); Pose(new Vector3(0.5f, 0f, -7.5f), 270f); Hud.Target.TrySetGear(HudGear.D); Hud.Target.SetView(1); });
            Hold(0.8f, false);
            Do(() => { Stage.Recording = true; Stage.Caption("주차된 차에 닿으면 바로 멈춰요"); Stage.Input.Set(0f, 0.8f, 0f); });
            WaitFor(() => Car.Blocked, 7f, "parked car");
            Hold(0.6f);
            Do(() => { Stage.Input.Set(0f, 0f, 1f); log.Append($"blocked={Car.Blocked} "); });
            Hold(1.2f);
            // parked-only rule: the real car leaves P
            Do(() => { Stage.Input.Set(0f, 0f, 0f); Stage.Input.realGear = 8; Stage.Caption("실제 차가 D단이 되면 바로 막아요"); });
            Hold(3f);
            Do(() => { Stage.Input.realGear = 4; Stage.Caption("P단으로 돌아오면 이어서 연습해요"); });
            Hold(2f);
            Do(() => { Stage.Caption("연습 마치기"); Hud.OpenDialog(); });
            Hold(1.8f);
            Do(() => { Stage.Caption(null); Stage.Recording = false; Stage.ShowInput = false; Hud.Target.Exit(); });
            steps.Add(() => Menu != null);
        }

        private static void Maps()
        {
            steps.Add(() => Menu != null);
            Do(() => { Menu.ShowMaps(); Stage.Recording = true; });
            Caption("부산 · 제주 · 파리, 실제 도시를 달려요", 2.6f);
            Do(() => { Menu.ChooseMap(0); Menu.SelectFirstPerson(); });
            Caption("부산, 광안대교를 건너 한 바퀴", 2.4f);
            Do(() => { Stage.Recording = false; Menu.LaunchDrive(); });

            // Busan: lane following through the same input as a driver
            WaitHud();
            Do(() => { var a = Object.FindObjectOfType<DriveAssist>(); JumpBusan(a, 1); Hud.Target.SetView(0); Stage.targetKph = 45f; Stage.pilot = DemoStage.Pilot.BusanLane; Stage.ShowInput = true; });
            Hold(4f, false);
            Do(() => { Stage.Recording = true; Stage.Caption("페달과 핸들 입력 그대로 달려요 · 광안리 해변길"); });
            Hold(6f);
            Do(() => { Hud.Target.SetView(1); Stage.Caption("3인칭으로 바꾸면 내 차를 뒤에서"); });
            Hold(5f);
            Do(() => { Stage.Recording = false; JumpBusan(Object.FindObjectOfType<DriveAssist>(), 2); log.Append($"bridge route={Object.FindObjectOfType<DriveAssist>().ActiveRoute} "); });
            Hold(4f, false);
            Do(() => { Stage.Recording = true; Stage.Caption("광안대교 위를 달려요"); });
            Hold(5f);
            Do(() => { Hud.Target.SetView(0); Stage.Caption("룸미러와 사이드미러도 실시간으로"); });
            Hold(4.5f);
            Leave();

            // Jeju: route following, stopping for red signals
            OpenMap(1, false);
            Do(() => { var d = Object.FindObjectOfType<JejuDriveController>(); d.guidedDrive = false; d.JumpToStation(0); Hud.Target.SetView(1); Stage.targetKph = 50f; Stage.pilot = DemoStage.Pilot.Route; Stage.ShowInput = true; MoveSignal(); });
            Hold(3f, false);
            Do(() => { Stage.Recording = true; Stage.Caption("제주 해안도로를 따라 한라산으로"); });
            Hold(6f);
            Do(() => { Hud.Target.SetView(0); Stage.Caption("1인칭 운전석 시점"); });
            Hold(4.5f);
            // (a red-signal stop at station 4 was dropped: the lane following there crossed the centre line into the kerb)
            Leave();

            // Paris
            OpenMap(2, true);
            Do(() => { var d = Object.FindObjectOfType<JejuDriveController>(); d.guidedDrive = false; d.JumpToStation(0); Hud.Target.SetView(0); Stage.targetKph = 40f; Stage.pilot = DemoStage.Pilot.Route; Stage.ShowInput = true; MoveSignal(); });
            Hold(3f, false);
            Do(() => { Stage.Recording = true; Stage.Caption("파리, 에펠탑 앞에서 출발"); });
            Hold(6f);
            Do(() => { Hud.Target.SetView(1); Stage.Caption("센강을 건너 샹젤리제로"); });
            Hold(5f);
            Leave();
        }

        private static void Ending()
        {
            steps.Add(() => Menu != null);
            Do(() => log.Append($"ending@{Stage.Frames} ")); // the landmark montage is cut in here
            Do(() => { Stage.Title("파킹 트래블", "주차 중인 차가, 연습장이 되고 여행지가 됩니다", "팀 잠자는 바보들  ·  Pleos Connect 차량용 앱"); Stage.Recording = true; });
            Tween(0.7f, t => Stage.TitleAlpha = t);
            Hold(3f);
            Do(() => Stage.Recording = false);
        }

        // ---------------------------------------------------------------- helpers

        private static DemoStage Stage => DemoStage.Instance;
        private static ParksideMenu Menu => SceneManager.GetActiveScene().name == "MainMenu" ? Object.FindObjectOfType<ParksideMenu>() : null;
        private static DriveHud Hud { get { var h = Object.FindObjectOfType<DriveHud>(); return h != null && h.Built ? h : null; } }
        private static VehicleController Car => Object.FindObjectOfType<VehicleController>();
        private static float Yaw { get { var y = Car.transform.eulerAngles.y; return y > 180f ? y - 360f : y; } }
        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static void Do(Action action) => steps.Add(() => { action(); return true; });

        /// <summary>Waits in game time (30 frames a second while recording); the editor clock runs it when not recording.</summary>
        private static void Hold(float seconds, bool gameTime = true)
        {
            Do(() => until = gameTime ? Time.time + seconds : Time.realtimeSinceStartup + seconds);
            steps.Add(() => (gameTime ? Time.time : Time.realtimeSinceStartup) >= until);
        }

        /// <summary>Waits until the condition holds, at most the given game seconds (a timeout is logged, the video goes on).</summary>
        private static void WaitFor(Func<bool> condition, float seconds, string what)
        {
            Do(() => timeoutAt = Time.time + seconds);
            steps.Add(() =>
            {
                if (condition()) return true;
                if (Time.time < timeoutAt) return false;
                log.Append("(timeout: " + what + ") ");
                return true;
            });
        }

        private static void Tween(float seconds, Action<float> apply)
        {
            Do(() => tweenStart = Time.time);
            steps.Add(() => { var t = Mathf.Clamp01((Time.time - tweenStart) / seconds); apply(t); return t >= 1f; });
        }

        private static void Caption(string text, float seconds) { Do(() => Stage.Caption(text)); Hold(seconds); }

        private static void WaitHud()
        {
            steps.Add(() => Hud != null && Stage.HookInput());
            Hold(1.0f, false);
            Do(() => { Stage.Input.Set(0f, 0f, 0f); Stage.Input.realGear = 4; });
        }

        private static void OpenMap(int map, bool firstPerson)
        {
            steps.Add(() => Menu != null);
            Do(() => { Menu.ShowMaps(); Menu.ChooseMap(map); if (firstPerson) Menu.SelectFirstPerson(); else Menu.SelectThirdPerson(); Menu.LaunchDrive(); });
            WaitHud();
        }

        private static void Leave()
        {
            Do(() => { Stage.Caption(null); Stage.Recording = false; Stage.pilot = DemoStage.Pilot.Off; Stage.ShowInput = false; Stage.Input.Set(0f, 0f, 0f); Hud.Target.Exit(); });
            steps.Add(() => Menu != null);
        }

        /// <summary>The input readout sits top centre; the HUD's signal notice moves under it for the recording.</summary>
        private static void MoveSignal()
        {
            var signal = Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(t => t.name == "Signal" && t.GetComponentInParent<DriveHud>() != null);
            if (signal != null) signal.anchoredPosition = new Vector2(0f, -96f);
        }

        /// <summary>Busan has two routes (bridge and coast); a jump point belongs to one of them.</summary>
        private static void JumpBusan(DriveAssist assist, int jump)
        {
            if (assist.RouteCount > 1) assist.JumpTo(assist.JumpRoute(jump), assist.JumpDistance(jump));
            else assist.JumpTo(assist.JumpDistance(jump));
        }

        private static void Pose(Vector3 position, float yaw)
        {
            Car.ResetToPose(position, Quaternion.Euler(0f, yaw, 0f));
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException($"timed out at step {step}/{steps.Count}");
                if (frameCheckAt > 0 && EditorApplication.timeSinceStartup > frameCheckAt)
                {
                    if (Time.frameCount - framesAtCheck < 10) throw new InvalidOperationException("game frames are not running (keep the Unity window visible)");
                    frameCheckAt = 0;
                }
                var guard = 0;
                while (step < steps.Count && guard++ < 50 && steps[step]()) step++;
                if (step >= steps.Count) Finish(true);
            }
            catch (Exception error) { log.Append("FAIL step " + step + ": " + error.Message); Finish(false); }
        }

        private static void Finish(bool pass)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            var frames = DemoStage.Instance != null ? DemoStage.Instance.Frames : 0;
            if (DemoStage.Instance != null) DemoStage.Instance.Recording = false;
            Time.captureFramerate = 0;
            try { GameViewTool.Restore(SessionState.GetInt(Key + "Size", 0)); } catch (Exception e) { log.Append(" (game view size not restored: " + e.Message + ")"); }
            var text = (pass ? "PARKSIDE_DEMO_VIDEO_PASS " : "PARKSIDE_DEMO_VIDEO_FAIL ") + $"frames={frames} seconds={frames / 30f:0.0} " + log;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "result.txt"), text);
            Debug.Log(text);
            EditorApplication.isPlaying = false;
        }
    }
}
