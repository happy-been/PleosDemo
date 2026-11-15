using System.Collections;
using System.IO;
using BusanEnvironment;
using Parkside.Core;
using Parkside.Input;
using Parkside.Jeju;
using Parkside.UI.Hud;
using Parkside.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parkside.Demo
{
    /// <summary>
    /// Demo-video stage, used only by the editor's demo director: survives scene loads and holds
    /// - the input source (DemoInput) handed to each scene's VehicleInputReceiver,
    /// - an auto pilot that drives through that input (lane following on Busan's course, the route in Jeju/Paris, stopping
    ///   for red signals there), so the wheel, pedals and HUD react as they would to a driver,
    /// - an overlay with a title card, captions and a small "Pleos input" readout,
    /// - a recorder that saves every shown frame (HUD and overlay included) as JPG while Recording is set.
    /// </summary>
    public sealed class DemoStage : MonoBehaviour
    {
        public enum Pilot { Off, BusanLane, Route }

        public static DemoStage Instance { get; private set; }
        public DemoInput Input { get; private set; }
        public Pilot pilot;
        public float targetKph = 45f;
        public bool Recording;
        public string FrameFolder;
        public int Frames { get; private set; }
        public float TitleAlpha, CaptionAlpha = 0f;

        private CanvasGroup titleGroup, captionGroup, inputGroup;
        private Text titleText, subtitleText, footText, captionText, steerText, gearText;
        private Image throttleBar, brakeBar;
        private Font regular, medium, semiBold;

        public static DemoStage Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Demo stage");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<DemoStage>();
            Instance.Input = go.AddComponent<DemoInput>();
            Instance.BuildOverlay();
            return Instance;
        }

        /// <summary>Hands the demo input to the scene's receiver (keyboard and Pleos sources are replaced for the recording).</summary>
        public bool HookInput()
        {
            var receiver = FindObjectOfType<VehicleInputReceiver>();
            if (receiver == null) return false;
            if (receiver.SelectedSource != Input) receiver.UseSources(Input, Input);
            return true;
        }

        private void Update()
        {
            // practice runs whenever the input is ready, even without editor focus (GameManager pauses on focus loss)
            var game = FindObjectOfType<GameManager>();
            if (game != null && !game.IsRunning && game.InputReady && Input.realGear == 4) game.StartPractice();
            if (pilot == Pilot.BusanLane) DriveBusan();
            else if (pilot == Pilot.Route) DriveRoute();
            UpdateOverlay();
        }

        // ---------------------------------------------------------------- auto pilot (pure pursuit through DemoInput)

        private void DriveBusan()
        {
            var assist = FindObjectOfType<DriveAssist>();
            var car = FindObjectOfType<VehicleController>();
            if (assist == null || car == null || assist.CourseLength <= 0f) return;
            var p = car.transform.position;
            var v = car.SpeedMps;
            var s = assist.DistanceNearest(p);
            var look = Mathf.Max(9f, v * 1.1f);
            var target = assist.LanePointAt(s + look);
            var ahead = assist.LanePointAt(s + 35f) - assist.LanePointAt(s + 5f);
            var wanted = targetKph / 3.6f;
            var bend = Vector3.Angle(Flat(car.transform.forward), Flat(ahead));
            if (bend > 18f) wanted = Mathf.Min(wanted, 9f);
            var steer = Pursuit(car.transform, target, 2.7f);
            var throttle = Mathf.Clamp01(wanted / 22f + (wanted - v) * 0.08f); // Busan's car: throttle 1 = 22 m/s
            var brake = v > wanted + 2.5f ? 0.6f : 0f;
            Input.Set(steer / 35f, brake > 0f ? 0f : throttle, brake);
        }

        private void DriveRoute()
        {
            var drive = FindObjectOfType<JejuDriveController>();
            if (drive == null || drive.route == null) return;
            var t = drive.transform;
            var v = Mathf.Abs(drive.SignedSpeed);
            var look = Mathf.Max(10f, v * 1.2f);
            var point = drive.route.AtDistance(drive.Progress + look, out var forward, out var width);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var target = point + right * JejuDriveRoute.LaneOffset(width);
            drive.route.AtDistance(drive.Progress + 35f, out var farForward, out _);
            var wanted = targetKph / 3.6f;
            if (Vector3.Angle(Flat(t.forward), Flat(farForward)) > 20f) wanted = Mathf.Min(wanted, 8.5f);
            var brake = 0f;
            if (drive.traffic != null)
            {
                var stop = drive.traffic.StopDistance(t.position, t.forward, out JejuSignalAspect aspect);
                if (aspect != JejuSignalAspect.Green && stop < Mathf.Max(30f, v * v / 6f + 8f))
                {
                    wanted = 0f;
                    if (stop < v * v / 14f + 4f) brake = 1f;
                }
            }
            var steerLimit = Mathf.Lerp(32f, 11f, Mathf.Clamp01(v / 19f)); // the controller's own steering range
            var steer = Pursuit(t, target, 2.7f) / steerLimit;
            Input.Set(steer, brake > 0f ? 0f : Mathf.Clamp01(wanted / (drive.topSpeedKph / 3.6f)), brake, steerLimit);
        }

        /// <summary>Pure pursuit: the road-wheel angle (degrees, + right) that brings the car through the target point.</summary>
        private static float Pursuit(Transform car, Vector3 target, float wheelbase)
        {
            var to = Flat(target - car.position);
            var alpha = Vector3.SignedAngle(Flat(car.forward), to, Vector3.up) * Mathf.Deg2Rad;
            var distance = Mathf.Max(1f, to.magnitude);
            return Mathf.Clamp(Mathf.Atan(2f * wheelbase * Mathf.Sin(alpha) / distance) * Mathf.Rad2Deg, -35f, 35f);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        // ---------------------------------------------------------------- recorder

        private IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (!Recording || string.IsNullOrEmpty(FrameFolder)) continue;
                var texture = ScreenCapture.CaptureScreenshotAsTexture();
                var jpg = texture.EncodeToJPG(92);
                Destroy(texture);
                File.WriteAllBytes(Path.Combine(FrameFolder, Frames.ToString("00000") + ".jpg"), jpg);
                Frames++;
            }
        }

        // ---------------------------------------------------------------- overlay

        public void Title(string title, string subtitle, string foot) { titleText.text = title; subtitleText.text = subtitle; footText.text = foot; }
        public void Caption(string text) { if (!string.IsNullOrEmpty(text)) captionText.text = text; CaptionTarget = string.IsNullOrEmpty(text) ? 0f : 1f; }
        public float CaptionTarget { get; set; }
        public bool ShowInput { get; set; }

        private void UpdateOverlay()
        {
            titleGroup.alpha = TitleAlpha;
            titleGroup.gameObject.SetActive(TitleAlpha > 0.001f);
            CaptionAlpha = Mathf.MoveTowards(CaptionAlpha, CaptionTarget, Time.deltaTime * 4f);
            captionGroup.alpha = CaptionAlpha;
            inputGroup.alpha = Mathf.MoveTowards(inputGroup.alpha, ShowInput ? 1f : 0f, Time.deltaTime * 4f);
            var wheel = Input.steeringNormalized * 450f;
            steerText.text = "핸들 " + (wheel >= 0f ? "+" : "−") + Mathf.Abs(wheel).ToString("0") + "°";
            throttleBar.fillAmount = Input.throttle;
            brakeBar.fillAmount = Input.brake;
            gearText.text = "차량 기어 " + (Input.realGear == 4 ? "P" : "D");
            gearText.color = Input.realGear == 4 ? new Color32(213, 247, 121, 255) : new Color32(255, 140, 110, 255);
        }

        private void BuildOverlay()
        {
            var assets = HudAssets.Load();
            regular = assets.regular; medium = assets.medium; semiBold = assets.semiBold;
            var canvasObject = new GameObject("Demo overlay", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasObject.transform;

            // title card
            var title = Panel("Title card", root, null, new Color32(12, 15, 18, 255));
            Stretch(title.rectTransform);
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleText = Label(title.rectTransform, semiBold, 96, new Color32(213, 247, 121, 255), new Vector2(0f, 70f), new Vector2(1400f, 130f));
            subtitleText = Label(title.rectTransform, medium, 36, new Color32(244, 246, 248, 255), new Vector2(0f, -30f), new Vector2(1400f, 60f));
            footText = Label(title.rectTransform, regular, 22, new Color32(170, 183, 194, 255), new Vector2(0f, -110f), new Vector2(1400f, 40f));

            // caption: lower third, above the dashboard line of the first-person view
            var caption = Panel("Caption", root, assets.round20, new Color32(12, 15, 18, 215));
            caption.type = Image.Type.Sliced;
            var c = caption.rectTransform;
            c.anchorMin = c.anchorMax = c.pivot = new Vector2(0.5f, 0f);
            c.anchoredPosition = new Vector2(0f, 300f);
            c.sizeDelta = new Vector2(760f, 66f);
            captionGroup = caption.gameObject.AddComponent<CanvasGroup>();
            captionGroup.alpha = 0f;
            captionText = Label(c, medium, 28, new Color32(244, 246, 248, 255), Vector2.zero, Vector2.zero);
            Stretch(captionText.rectTransform);

            // input readout (what the Virtual Console would send): top centre
            var input = Panel("Input readout", root, assets.round20, new Color32(23, 29, 35, 235));
            input.type = Image.Type.Sliced;
            var r = input.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -24f);
            r.sizeDelta = new Vector2(640f, 58f);
            inputGroup = input.gameObject.AddComponent<CanvasGroup>();
            inputGroup.alpha = 0f;
            var head = Label(r, medium, 15, new Color32(170, 183, 194, 255), new Vector2(-260f, 0f), new Vector2(110f, 40f));
            head.text = "Pleos 입력";
            steerText = Label(r, semiBold, 18, new Color32(244, 246, 248, 255), new Vector2(-160f, 0f), new Vector2(120f, 40f));
            throttleBar = Bar(r, assets.round14, "가속", new Vector2(-40f, 0f), new Color32(213, 247, 121, 255));
            brakeBar = Bar(r, assets.round14, "브레이크", new Vector2(110f, 0f), new Color32(255, 140, 110, 255));
            gearText = Label(r, semiBold, 16, Color.white, new Vector2(250f, 0f), new Vector2(120f, 40f));
        }

        private Image Bar(RectTransform parent, Sprite sprite, string name, Vector2 position, Color color)
        {
            var label = Label(parent, regular, 14, new Color32(170, 183, 194, 255), position + new Vector2(-34f, 0f), new Vector2(60f, 30f));
            label.text = name;
            var back = Panel(name + " back", parent, sprite, new Color32(35, 44, 52, 255));
            back.type = Image.Type.Sliced;
            back.rectTransform.anchoredPosition = position + new Vector2(30f, 0f);
            back.rectTransform.sizeDelta = new Vector2(70f, 12f);
            var fill = Panel(name, back.rectTransform, sprite, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            Stretch(fill.rectTransform);
            return fill;
        }

        private static Image Panel(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(RectTransform parent, Font font, int size, Color color, Vector2 position, Vector2 box)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = box;
            text.font = font; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
