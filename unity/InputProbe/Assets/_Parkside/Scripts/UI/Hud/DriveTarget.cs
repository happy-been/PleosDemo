using Parkside.Cameras;
using Parkside.Core;
using Parkside.Input;
using Parkside.Jeju;
using Parkside.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parkside.UI.Hud
{
    public enum HudGear { R, N, D }

    /// <summary>
    /// What the presentation HUD needs from a scene's car: speed, steering, gear, camera view, map and the way back.
    /// The practice scenes (lot and Busan) and the Jeju/Paris drive maps run different controllers, so each gets an adapter.
    /// HUD views: 0 = first person, 1 = third person, 2 = top.
    /// </summary>
    public abstract class DriveTarget
    {
        public abstract Transform Car { get; }
        public abstract Camera View { get; }
        public abstract float SpeedKph { get; }
        /// <summary>Steering input -1..1, positive to the right.</summary>
        public abstract float Steering { get; }
        public abstract HudGear Gear { get; }
        /// <summary>Changes gear only at a standstill; false when the car is still moving.</summary>
        public abstract bool TrySetGear(HudGear gear);
        public abstract int CurrentView { get; }
        public abstract void SetView(int view);
        /// <summary>Renderers of the player car to hide in first person (the camera sits inside the body).</summary>
        public abstract void ShowBody(bool visible);
        public abstract void Tick();
        public abstract void Exit();
        /// <summary>Why the vehicle input is blocked right now (null when it drives).</summary>
        public virtual string BlockReason => null;

        /// <summary>
        /// True when the block is the parked-only rule: the real car must be in P at 0 speed. Unity's own check says
        /// "Real vehicle must stay in P / 0 speed"; the Pleos bridge says "AVD를 P단으로 변경하세요" or "…속도가 0이어야…".
        /// </summary>
        public static bool IsParkedOnlyBlock(string reason) =>
            !string.IsNullOrEmpty(reason) && (reason.Contains("P / 0") || reason.Contains("P단") || reason.Contains("속도가 0"));

        public virtual bool HasRouteMap => false;
        public virtual Texture RouteMap => null;
        /// <summary>The player position on the route map, 0..1 on both axes (map up = world +z).</summary>
        public virtual Vector2 RouteMapPoint => Vector2.zero;
        public virtual string Caption => "";
        public virtual string Signal => null;
        public virtual string Credit => null;
        public const float StandstillKph = 1f;

        public static void LoadMenu() => SceneManager.LoadSceneAsync(ParksideMenu.ScenePath);
    }

    /// <summary>Parking lot practice and Busan: GameManager + VehicleController + VehicleCamera.</summary>
    public sealed class PracticeTarget : DriveTarget
    {
        private readonly GameManager game;
        private readonly VehicleController car;
        private readonly VehicleCamera camera;
        private readonly VehicleInputReceiver input;
        private readonly PracticeMenuLink link;
        private readonly string caption;
        private GameObject body;

        public PracticeTarget(GameManager game, VehicleController car, VehicleCamera camera, VehicleInputReceiver input, PracticeMenuLink link, string caption)
        {
            this.game = game; this.car = car; this.camera = camera; this.input = input; this.link = link; this.caption = caption;
        }

        public override Transform Car => car.transform;
        public override Camera View => camera.GetComponent<Camera>();
        // Shown speed comes from the car's actual movement: Busan's auto drive moves the car itself and leaves
        // VehicleController.SpeedKph at zero. Gear changes still use the controller's own speed.
        public override float SpeedKph => Mathf.Max(car.SpeedKph, measuredKph);
        private float measuredKph;
        private Vector3 lastPosition;
        private bool measured;
        public override float Steering => input != null && input.Controls.IsValid ? Mathf.Clamp(input.Snapshot.steeringNormalized, -1f, 1f) : 0f;
        public override HudGear Gear => car.Neutral ? HudGear.N : car.IsReversing ? HudGear.R : HudGear.D;
        public override string BlockReason => input == null ? "No input receiver" : input.Controls.IsValid ? null : input.Controls.BlockReason;
        public override string Caption => caption;

        public override bool TrySetGear(HudGear gear)
        {
            if (gear == Gear) return true;
            if (car.SpeedKph > StandstillKph) return false;
            car.Neutral = gear == HudGear.N;
            if ((gear == HudGear.R) != car.IsReversing && gear != HudGear.N) car.ToggleDirection();
            return true;
        }

        // VehicleCamera: 0 = chase, 1 = top, 2 = driver
        public override int CurrentView => camera.ViewIndex == 2 ? 0 : camera.ViewIndex == 0 ? 1 : 2;
        public override void SetView(int view) => camera.SetView(view == 0 ? 2 : view == 1 ? 0 : 1);

        /// <summary>Puts the shared Tucson on the car and hides the scene's own body (the lot's box car, Busan's i30).</summary>
        public void UseBody(GameObject prefab)
        {
            if (body != null) return;
            var authored = car.transform.Find("Unified Tucson"); // the lot scene already carries it (ParksideMenuAuthoring)
            if (authored != null) { body = authored.gameObject; return; }
            if (prefab == null) return;
            foreach (Transform child in car.transform)
            {
                if (child.name == "CameraAnchors" || child.GetComponentInChildren<Camera>(true) != null) continue;
                if (child.GetComponentInChildren<Renderer>(true) == null) continue;
                child.gameObject.SetActive(false); // the wheel transforms keep being steered, only unseen
            }
            body = Object.Instantiate(prefab, car.transform, false);
            body.name = "Unified Tucson";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var bounds = Bounds(body);
            var local = car.transform.InverseTransformPoint(bounds.center);
            var bottom = car.transform.InverseTransformPoint(bounds.min).y;
            body.transform.localPosition -= new Vector3(local.x, bottom, local.z);
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = car.gameObject.layer;
        }

        private static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        public override void ShowBody(bool visible) { if (body != null && body.activeSelf != visible) body.SetActive(visible); }

        public override void Tick()
        {
            // No start button on screen any more: practice runs whenever the input is ready.
            if (!game.IsRunning && game.InputReady && Application.isFocused) game.StartPractice();
            var position = car.transform.position;
            var dt = Time.deltaTime;
            if (measured && dt > 0f)
            {
                var step = Vector3.Distance(position, lastPosition);
                var kph = step > 8f ? 0f : step / dt * 3.6f; // a jump (reset, course jump) is not speed
                measuredKph = Mathf.Lerp(measuredKph, kph, 1f - Mathf.Exp(-4f * dt));
                if (measuredKph < 0.5f) measuredKph = 0f;
            }
            lastPosition = position;
            measured = true;
        }

        public override void Exit()
        {
            if (link != null) link.ReturnToMenu();
            else { game.PausePractice(); LoadMenu(); }
        }
    }

    /// <summary>Jeju and Paris: JejuDriveController, driven by the Pleos input instead of the keyboard.</summary>
    public sealed class RouteDriveTarget : DriveTarget
    {
        private readonly JejuDriveController drive;
        private readonly VehicleInputReceiver input;
        private HudGear gear = HudGear.D;

        public RouteDriveTarget(JejuDriveController drive)
        {
            this.drive = drive;
            // Same input path as the practice scenes: keyboard in the editor, Pleos SDK + console pedals on the device.
            var host = new GameObject("Pleos input (HUD)");
            SceneManager.MoveGameObjectToScene(host, drive.gameObject.scene);
            var keyboard = host.AddComponent<KeyboardInputSource>();
            var pleos = host.AddComponent<PleosInputSource>();
            input = host.AddComponent<VehicleInputReceiver>();
            input.UseSources(keyboard, pleos);
            drive.externalInput = true;
            drive.showHud = false; // its OnGUI title, speed, map and key hints are replaced by this HUD
        }

        public override Transform Car => drive.transform;
        public override Camera View => drive.view;
        public override float SpeedKph => drive.SpeedKph;
        public override float Steering => drive.Steering;
        public override HudGear Gear => gear;
        public override string BlockReason => input.Controls.IsValid ? null : input.Controls.BlockReason;

        public override bool TrySetGear(HudGear wanted)
        {
            if (wanted == gear) return true;
            if (drive.SpeedKph > StandstillKph) return false;
            gear = wanted;
            return true;
        }

        // JejuDriveController: 0 = chase, 1 = driver, 2 = top
        public override int CurrentView => drive.cameraMode == 1 ? 0 : drive.cameraMode == 0 ? 1 : 2;
        public override void SetView(int view) { drive.cameraMode = view == 0 ? 1 : view == 1 ? 0 : 2; drive.UpdateCamera(true); }
        public override void ShowBody(bool visible) { } // the controller already hides its car in the driver view

        public override void Tick()
        {
            var controls = input.Controls;
            var pedal = controls.IsValid ? controls.Throttle : 0f;
            drive.externalThrottle = gear == HudGear.D ? pedal : gear == HudGear.R ? -pedal : 0f;
            drive.externalTurn = controls.IsValid ? Mathf.Clamp(input.Snapshot.steeringNormalized, -1f, 1f) : 0f;
            drive.externalBrake = !controls.IsValid || controls.Brake > 0.01f;
        }

        public override void Exit() => LoadMenu();

        public override bool HasRouteMap => drive.route != null && drive.route.map != null;
        public override Texture RouteMap => drive.route.map;
        public override Vector2 RouteMapPoint
        {
            get
            {
                var p = drive.transform.position;
                return new Vector2(Mathf.InverseLerp(drive.route.mapMin.x, drive.route.mapMax.x, p.x), Mathf.InverseLerp(drive.route.mapMin.y, drive.route.mapMax.y, p.z));
            }
        }
        public override string Caption => drive.route == null ? "" : "남은 거리  " + Mathf.Max(0f, (drive.route.Length - drive.Progress) / 1000f).ToString("0.0") + " km";
        public override string Credit => drive.mapCredit;

        public override string Signal
        {
            get
            {
                if (drive.traffic == null) return null;
                var stop = drive.traffic.StopDistance(drive.transform.position, drive.transform.forward, out JejuSignalAspect aspect);
                if (stop >= 85f) return null;
                return (aspect == JejuSignalAspect.Green ? "진행 신호" : aspect == JejuSignalAspect.Amber ? "황색 신호" : "정지 신호") + "   " + Mathf.Max(0f, stop).ToString("0") + " m";
            }
        }
    }
}
