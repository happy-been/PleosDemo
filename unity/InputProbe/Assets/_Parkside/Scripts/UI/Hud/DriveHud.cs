using System.Collections;
using Parkside.Cameras;
using Parkside.Core;
using Parkside.Input;
using Parkside.Jeju;
using Parkside.Vehicle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parkside.UI.Hud
{
    /// <summary>
    /// Presentation HUD shared by the four drive scenes (Figma "시연 HUD 시안 v1", 1600x900 reference = 16:9 simulator):
    /// camera views top left, R/N/D gear slider with speed on the left, map with its name top right, "마치기" bottom right,
    /// and in first person a drawn cockpit (roof, A-pillar, dashboard, steering wheel and hands that turn with the
    /// steering) with live rear-view and side mirrors. It attaches itself when one of the scenes loads, so the scene
    /// files stay as they are; the scenes' own debug panel, menu button and OnGUI key hints are switched off.
    /// </summary>
    public sealed class DriveHud : MonoBehaviour
    {
        private struct SceneInfo { public string Scene, Name, Finish; public float MapSize, MapHeight; }
        private static readonly SceneInfo[] Scenes =
        {
            new SceneInfo { Scene = "ParkingLotPractice", Name = "골목 주차장 · 연습", Finish = "연습 마치기", MapSize = 20f, MapHeight = 60f },
            new SceneInfo { Scene = "BusanLandmarks", Name = "부산 · 광안대교 순환", Finish = "여행 마치기", MapSize = 130f, MapHeight = 320f },
            new SceneInfo { Scene = "JejuCoastalDrive", Name = "제주 · 한라산 가는 길", Finish = "여행 마치기" },
            new SceneInfo { Scene = "ParisEiffelDrive", Name = "파리 · 에펠탑과 개선문", Finish = "여행 마치기" },
        };
        public static bool IsDriveScene(string scene) => System.Array.FindIndex(Scenes, s => s.Scene == scene) >= 0;

        private static readonly Color Panel = new Color32(23, 29, 35, 230);
        private static readonly Color Inner = new Color32(35, 44, 52, 255);
        private static readonly Color Lime = new Color32(213, 247, 121, 255);
        private static readonly Color TextColor = new Color32(244, 246, 248, 255);
        private static readonly Color Muted = new Color32(170, 183, 194, 255);
        private static readonly Color OnLime = new Color32(20, 27, 18, 255);
        // gear knob top offsets inside the 132x420 panel (Figma)
        private static readonly float[] KnobTop = { 102f, 190f, 276f };
        private const float HandFollow = 25f;

        private SceneInfo info;
        private HudAssets art;
        private DriveTarget target;
        private RectTransform canvasRect, cockpit, wheel, hands, knob, gearPanel, mapArrow, dialog;
        private Text speedText, knobLabel, hintText, captionText, signalText;
        private Text[] gearLetters, viewLabels;
        private Image[] viewButtons;
        private RawImage mapImage;
        private GameObject signalPanel, parkedNotice, waitingNotice;
        private float blockedSince = -1f;
        private string blockedReason;
        private Camera mapCamera;
        private RenderTexture mapTexture;
        private readonly MirrorView[] mirrors = new MirrorView[3];
        private float wheelAngle, enforceViewUntil, hintUntil;
        private int wantedView = -1;
        private bool dragging, built;

        /// <summary>Checks and screenshots only: a steering value (-1..1) shown instead of the input; NaN = off.</summary>
        public float PreviewSteering = float.NaN;
        /// <summary>Checks and screenshots only: a block reason shown instead of the input's; null = off.</summary>
        public string PreviewBlock;
        public bool ParkedNoticeShown => parkedNotice != null && parkedNotice.activeSelf;
        public bool WaitingShown => waitingNotice != null && waitingNotice.activeSelf;
        public float WheelAngle => wheelAngle;
        public DriveTarget Target => target;
        public bool Built => built;
        public string FinishLabel => info.Finish;
        public bool DialogOpen => dialog != null && dialog.gameObject.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= Attach;
            SceneManager.sceneLoaded += Attach;
        }

        private static void Attach(Scene scene, LoadSceneMode mode)
        {
            if (!IsDriveScene(scene.name) || FindObjectOfType<DriveHud>() != null) return;
            var host = new GameObject("Presentation HUD");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<DriveHud>().info = System.Array.Find(Scenes, s => s.Scene == scene.name);
        }

        private IEnumerator Start()
        {
            yield return null; // the scene's own Start methods (camera choice, input) run first
            art = HudAssets.Load();
            if (art == null) { Debug.LogError("Resources/ParksideHud/HudAssets is missing (PARKSIDE → Update HUD assets)", this); yield break; }
            target = CreateTarget();
            if (target == null) { Debug.LogError("DriveHud: no supported car controller in " + info.Scene, this); yield break; }
            HideSceneUi();
            Build();
            wantedView = ParksideMenu.LaunchedFromMenu ? (ParksideMenu.SelectedCameraView == 2 ? 0 : 1) : target.CurrentView;
            enforceViewUntil = Time.unscaledTime + 1.5f; // Busan's DriveAssist sets its own view once on its first frames
            target.SetView(wantedView);
            built = true;
        }

        private DriveTarget CreateTarget()
        {
            var drive = FindObjectOfType<JejuDriveController>();
            if (drive != null) return new RouteDriveTarget(drive);
            var game = FindObjectOfType<GameManager>();
            var car = FindObjectOfType<VehicleController>();
            var view = FindObjectOfType<VehicleCamera>();
            if (game == null || car == null || view == null) return null;
            var practice = new PracticeTarget(game, car, view, FindObjectOfType<VehicleInputReceiver>(), FindObjectOfType<PracticeMenuLink>(), info.Scene == "BusanLandmarks" ? "광안대교 경로 · 약 4.4 km" : "연습 칸  T1–T3 · P1–P2");
            practice.UseBody(art.playerCar);
            return practice;
        }

        /// <summary>The probe-era UI is not part of the presentation: the whole PracticeCanvas (debug panel, block reason, status marks) and its "메뉴로" button.</summary>
        private static void HideSceneUi()
        {
            foreach (var t in FindObjectsOfType<RectTransform>(true))
                if (t.name == "PracticeCanvas" || t.name == "DebugPanel" || (t.name == "메뉴로" && t.GetComponent<Button>() != null)) t.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!built) return;
            target.Tick();
            if (Time.unscaledTime < enforceViewUntil && target.CurrentView != wantedView) target.SetView(wantedView);
            else wantedView = target.CurrentView; // later changes (Busan's V key) are followed
            var first = target.CurrentView == 0;
            target.ShowBody(!first);
            cockpit.gameObject.SetActive(first);
            for (var i = 0; i < 3; i++)
            {
                viewButtons[i].color = i == target.CurrentView ? Lime : Color.clear;
                viewLabels[i].color = i == target.CurrentView ? OnLime : TextColor;
            }
            speedText.text = Mathf.RoundToInt(target.SpeedKph).ToString();
            var gear = (int)target.Gear;
            if (!dragging) knob.anchoredPosition = new Vector2(34f, -KnobTop[gear]);
            knobLabel.text = target.Gear.ToString();
            for (var i = 0; i < 3; i++) gearLetters[i].enabled = i != gear;
            hintText.text = Time.unscaledTime < hintUntil ? "멈춘 뒤 바꿔요" : "밀어서 변경";
            hintText.color = Time.unscaledTime < hintUntil ? Lime : Muted;
            captionText.text = target.Caption;
            var signal = target.Signal;
            signalPanel.SetActive(!string.IsNullOrEmpty(signal));
            if (signal != null) signalText.text = signal;
            UpdateMap();
            UpdateNotice();
            if (first) UpdateCockpit();
        }

        private void LateUpdate()
        {
            if (!built) return;
            var first = target.CurrentView == 0;
            // one small extra render per frame at most, without shadows (they cost the most and barely show at this size)
            var shadows = QualitySettings.shadowDistance;
            QualitySettings.shadowDistance = 0f;
            var mirror = mirrors[Time.frameCount % mirrors.Length];
            if (first && mirror != null) mirror.Render(target.Car, target.View);
            else if (mapCamera != null && Time.frameCount % 4 == 0)
            {
                var p = target.Car.position;
                mapCamera.transform.SetPositionAndRotation(new Vector3(p.x, p.y + info.MapHeight, p.z), Quaternion.Euler(90f, 0f, 0f));
                mapCamera.Render();
            }
            if (first && mapCamera != null && Time.frameCount % 12 == 0)
            {
                var p = target.Car.position;
                mapCamera.transform.SetPositionAndRotation(new Vector3(p.x, p.y + info.MapHeight, p.z), Quaternion.Euler(90f, 0f, 0f));
                mapCamera.Render();
            }
            QualitySettings.shadowDistance = shadows;
        }

        private void UpdateCockpit()
        {
            // steering input -1..1 to a steering-wheel angle (about 2.5 turns lock to lock); the hands follow only ±25°:
            // with only the top 30 % of the wheel on screen, a hand turned further leaves the bottom edge
            var steering = float.IsNaN(PreviewSteering) ? target.Steering : PreviewSteering;
            wheelAngle = float.IsNaN(PreviewSteering) ? Mathf.Lerp(wheelAngle, steering * 450f, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)) : steering * 450f;
            wheel.localEulerAngles = new Vector3(0f, 0f, -wheelAngle);
            hands.localEulerAngles = new Vector3(0f, 0f, -Mathf.Clamp(wheelAngle, -HandFollow, HandFollow));
        }

        private void UpdateMap()
        {
            var yaw = target.Car.eulerAngles.y;
            mapArrow.localEulerAngles = new Vector3(0f, 0f, -yaw);
            if (!target.HasRouteMap) return;
            // north-up window of the route map around the car
            const float w = 0.26f;
            var h = w * 210f / 288f; // the map window (mask) is 288x210
            var point = target.RouteMapPoint;
            var x = Mathf.Clamp(point.x - w * 0.5f, 0f, 1f - w);
            var y = Mathf.Clamp(point.y - h * 0.5f, 0f, 1f - h);
            mapImage.uvRect = new Rect(x, y, w, h);
            mapArrow.anchoredPosition = new Vector2((point.x - x - w * 0.5f) / w * 288f, (point.y - y - h * 0.5f) / h * 210f);
        }

        // ---------------------------------------------------------------- building

        private void Build()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.GetComponent<EventSystem>().sendNavigationEvents = false; // keys drive the car, not the buttons
                SceneManager.MoveGameObjectToScene(events, gameObject.scene);
            }
            var canvasObject = new GameObject("HUD canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)canvasObject.transform;
            BuildCockpit();
            BuildViews();
            BuildGear();
            BuildMap();
            BuildSignal();
            BuildNotice();
            BuildFinish(); // the finish button and its dialog stay above the notice, so a parked-only stop can still be left
        }

        private void BuildCockpit()
        {
            // a 1600x900 artboard that covers the screen (keeps the drawing's proportions on other aspect ratios)
            cockpit = Box("Cockpit (first person)", canvasRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 900f));
            var fitter = cockpit.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            var board = Box("Artboard", cockpit, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            board.sizeDelta = Vector2.zero;
            var scaleRoot = Box("1600x900", board, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 900f));
            scaleRoot.gameObject.AddComponent<ScaleToParent>();
            Img("Roof, A-pillar, dashboard", scaleRoot, art.cockpit, Color.white, TopLeft(0, 0, 1600, 900)).raycastTarget = false;
            // steering wheel centre (700, 1054.8): only the top 30 % of the 800-wide wheel is on screen
            var wheelCentre = new Vector2(700f - 800f, 450f - 1054.8f);
            wheel = Box("Steering wheel", scaleRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), wheelCentre, new Vector2(800f, 800f));
            Img("Wheel", wheel, art.wheel, Color.white, null).raycastTarget = false;
            hands = Box("Hands", scaleRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), wheelCentre, Vector2.zero);
            Hand("Left hand", art.handLeft, 48f);
            Hand("Right hand", art.handRight, -48f);
            mirrors[0] = Mirror("Rear-view mirror", scaleRoot, art.mirrorRearHousing, art.mirrorRearMask, 905, 150, 340, 100, new Vector3(0f, 1.32f, -0.5f), 180f, 38f, 512, 150);
            mirrors[1] = Mirror("Left mirror", scaleRoot, art.mirrorLeftHousing, art.mirrorLeftMask, 24, 654, 228, 136, new Vector3(-1.02f, 1.08f, 0.85f), 196f, 44f, 320, 190);
            mirrors[2] = Mirror("Right mirror", scaleRoot, art.mirrorRightHousing, art.mirrorRightMask, 1348, 654, 228, 136, new Vector3(1.02f, 1.08f, 0.85f), 164f, 44f, 320, 190);
        }

        private void Hand(string name, Sprite sprite, float rimAngle)
        {
            var arm = Box(name + " holder", hands, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            arm.localEulerAngles = new Vector3(0f, 0f, rimAngle);
            var hand = Box(name, arm, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 60f / 140f * -1f + 1f), new Vector2(0f, 360f), new Vector2(200f, 140f));
            Img("Image", hand, sprite, Color.white, null).raycastTarget = false;
        }

        private MirrorView Mirror(string name, RectTransform parent, Sprite housing, Sprite mask, float x, float y, float w, float h, Vector3 local, float yaw, float fov, int textureWidth, int textureHeight)
        {
            var frame = Img(name, parent, housing, Color.white, TopLeft(x, y, w, h));
            frame.raycastTarget = false;
            var maskImage = Img("Mask", frame.rectTransform, mask, Color.white, null);
            maskImage.raycastTarget = false;
            maskImage.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var picture = new GameObject("Live view", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            picture.rectTransform.SetParent(maskImage.rectTransform, false);
            Stretch(picture.rectTransform);
            picture.raycastTarget = false;
            picture.uvRect = new Rect(1f, 0f, -1f, 1f); // a mirror shows the scene left-right swapped
            var view = new MirrorView(name, local, yaw, fov, textureWidth, textureHeight, transform);
            picture.texture = view.Texture;
            return view;
        }

        private void BuildViews()
        {
            var bar = Img("Camera views", canvasRect, art.round20, Panel, TopLeft(28, 24, 360, 64)).rectTransform;
            bar.GetComponent<Image>().type = Image.Type.Sliced;
            string[] names = { "1인칭", "3인칭", "탑뷰" };
            viewButtons = new Image[3]; viewLabels = new Text[3];
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var button = Img("View " + names[i], bar, art.round14, Color.clear, new Rect(6f + i * 118f, 6f, 112f, 52f), bar);
                button.type = Image.Type.Sliced;
                button.gameObject.AddComponent<Button>().onClick.AddListener(() => { wantedView = index; enforceViewUntil = 0f; target.SetView(index); });
                viewButtons[i] = button;
                viewLabels[i] = Label(names[i], button.rectTransform, art.medium, 21, TextColor, TextAnchor.MiddleCenter);
            }
        }

        private void BuildGear()
        {
            gearPanel = Img("Gear slider", canvasRect, art.round20, Panel, TopLeft(28, 220, 132, 420)).rectTransform;
            gearPanel.GetComponent<Image>().type = Image.Type.Sliced;
            speedText = Label("0", Box("Speed", gearPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -14f), new Vector2(132f, 52f), new Vector2(0f, 1f)), art.semiBold, 44, TextColor, TextAnchor.MiddleCenter);
            Label("km/h", Box("Unit", gearPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -66f), new Vector2(132f, 20f), new Vector2(0f, 1f)), art.regular, 15, Muted, TextAnchor.MiddleCenter);
            var track = Img("Track", gearPanel, art.round20, Inner, new Rect(30f, 100f, 72f, 272f), gearPanel);
            track.type = Image.Type.Sliced;
            gearLetters = new Text[3];
            string[] letters = { "R", "N", "D" };
            float[] letterTop = { 118f, 222f, 326f };
            for (var i = 0; i < 3; i++)
                gearLetters[i] = Label(letters[i], Box("Gear " + letters[i], gearPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -letterTop[i]), new Vector2(72f, 28f), new Vector2(0f, 1f)), art.semiBold, 22, Muted, TextAnchor.MiddleCenter);
            var knobImage = Img("Knob", gearPanel, art.round20, Lime, new Rect(34f, KnobTop[2], 64f, 92f), gearPanel);
            knobImage.type = Image.Type.Sliced;
            knobImage.raycastTarget = false;
            knob = knobImage.rectTransform;
            Img("Car", knob, art.carIcon, Color.white, new Rect(17f, 10f, 30f, 48f), knob).raycastTarget = false;
            knobLabel = Label("D", Box("Letter", knob, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -60f), new Vector2(64f, 26f), new Vector2(0f, 1f)), art.semiBold, 18, OnLime, TextAnchor.MiddleCenter);
            hintText = Label("밀어서 변경", Box("Hint", gearPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -386f), new Vector2(132f, 20f), new Vector2(0f, 1f)), art.regular, 13, Muted, TextAnchor.MiddleCenter);
            var drag = track.gameObject.AddComponent<GearDrag>();
            drag.hud = this;
        }

        /// <summary>Called by the track while the knob is dragged; topOffset is the knob's top inside the panel.</summary>
        public void DragKnob(float topOffset)
        {
            dragging = true;
            knob.anchoredPosition = new Vector2(34f, -Mathf.Clamp(topOffset, KnobTop[0], KnobTop[2]));
        }

        public void ReleaseKnob()
        {
            dragging = false;
            var top = -knob.anchoredPosition.y;
            var nearest = 0;
            for (var i = 1; i < 3; i++) if (Mathf.Abs(KnobTop[i] - top) < Mathf.Abs(KnobTop[nearest] - top)) nearest = i;
            if (!target.TrySetGear((HudGear)nearest)) hintUntil = Time.unscaledTime + 1.6f;
        }

        internal float PanelTop(Vector2 screen, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gearPanel, screen, eventCamera, out var local);
            return -local.y - 46f; // panel pivot is its top-left corner; the knob is 92 high
        }

        public bool SetGear(HudGear gear) => target.TrySetGear(gear);

        private void BuildMap()
        {
            var root = Box("Map", canvasRect, Vector2.one, Vector2.one, new Vector2(-28f, -24f), new Vector2(300f, 262f), Vector2.one);
            var nameTag = Img("Map name", root, art.round14, Panel, null);
            nameTag.type = Image.Type.Sliced;
            var tag = nameTag.rectTransform;
            tag.anchorMin = tag.anchorMax = tag.pivot = Vector2.one;
            tag.anchoredPosition = Vector2.zero;
            var nameText = Label(info.Name, tag, art.medium, 15, TextColor, TextAnchor.MiddleLeft);
            nameText.rectTransform.offsetMin = new Vector2(28f, 0f);
            nameText.rectTransform.offsetMax = new Vector2(-14f, 0f);
            tag.sizeDelta = new Vector2(nameText.preferredWidth + 44f, 28f);
            Img("Dot", tag, art.circle, Lime, new Rect(12f, 10f, 8f, 8f), tag);
            var frame = Img("Map frame", root, art.round20, Panel, new Rect(0f, 40f, 300f, 222f), root);
            frame.type = Image.Type.Sliced;
            var mask = Img("Map mask", frame.rectTransform, art.round20, Color.white, new Rect(6f, 6f, 288f, 210f), frame.rectTransform);
            mask.type = Image.Type.Sliced;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            mapImage = new GameObject("Map picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            mapImage.rectTransform.SetParent(mask.rectTransform, false);
            Stretch(mapImage.rectTransform);
            mapImage.raycastTarget = false;
            if (target.HasRouteMap) mapImage.texture = target.RouteMap;
            else
            {
                // a live top-down view around the car (lot and Busan have no drawn route map)
                mapTexture = new RenderTexture(384, 280, 16) { name = "HUD map" };
                var go = new GameObject("HUD map camera");
                go.transform.SetParent(transform, false);
                mapCamera = go.AddComponent<Camera>();
                mapCamera.enabled = false;
                mapCamera.orthographic = true;
                mapCamera.orthographicSize = info.MapSize;
                mapCamera.nearClipPlane = 1f;
                mapCamera.farClipPlane = info.MapHeight + 80f;
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = new Color32(29, 37, 44, 255);
                mapCamera.allowHDR = false; mapCamera.allowMSAA = false;
                mapCamera.targetTexture = mapTexture;
                mapImage.texture = mapTexture;
            }
            var arrowSize = 22f;
            mapArrow = Img("Car", mask.rectTransform, art.arrow, Lime, null).rectTransform;
            mapArrow.anchorMin = mapArrow.anchorMax = mapArrow.pivot = new Vector2(0.5f, 0.5f);
            mapArrow.sizeDelta = new Vector2(arrowSize, arrowSize);
            mapArrow.anchoredPosition = Vector2.zero;
            var captionBar = Img("Caption", root, art.round20, new Color32(23, 29, 35, 235), new Rect(0f, 222f, 300f, 40f), root);
            captionBar.type = Image.Type.Sliced;
            captionText = Label("", captionBar.rectTransform, art.medium, 15, TextColor, TextAnchor.MiddleLeft);
            captionText.rectTransform.offsetMin = new Vector2(16f, 0f);
        }

        private void BuildFinish()
        {
            var button = Img("Finish", canvasRect, art.round20, Panel, null);
            button.type = Image.Type.Sliced;
            var rect = button.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-28f, 24f);
            rect.sizeDelta = new Vector2(200f, 64f);
            button.gameObject.AddComponent<Button>().onClick.AddListener(OpenDialog);
            Label(info.Finish, rect, art.medium, 22, TextColor, TextAnchor.MiddleCenter);

            // "메뉴로 나갈까요?" so a stray tap while driving does not end the trip
            var shade = Img("Finish dialog", canvasRect, null, new Color(0f, 0f, 0f, 0.45f), null);
            Stretch(shade.rectTransform);
            dialog = shade.rectTransform;
            var box = Img("Box", dialog, art.round20, Panel, null);
            box.type = Image.Type.Sliced;
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.anchoredPosition = Vector2.zero;
            box.rectTransform.sizeDelta = new Vector2(520f, 230f);
            Label("메뉴로 나갈까요?", Box("Title", box.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -36f), new Vector2(0f, 40f), new Vector2(0.5f, 1f)), art.semiBold, 28, TextColor, TextAnchor.MiddleCenter);
            Label(info.Finish == "연습 마치기" ? "지금까지의 연습을 마치고 홈으로 돌아가요." : "여행을 마치고 홈으로 돌아가요.", Box("Body", box.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -86f), new Vector2(0f, 28f), new Vector2(0.5f, 1f)), art.regular, 18, Muted, TextAnchor.MiddleCenter);
            DialogButton(box.rectTransform, "계속하기", new Vector2(-122f, 36f), Inner, TextColor, CloseDialog);
            DialogButton(box.rectTransform, "나가기", new Vector2(122f, 36f), Lime, OnLime, () => target.Exit());
            dialog.gameObject.SetActive(false);
        }

        private void DialogButton(RectTransform parent, string text, Vector2 position, Color fill, Color ink, UnityEngine.Events.UnityAction action)
        {
            var image = Img(text, parent, art.round14, fill, null);
            image.type = Image.Type.Sliced;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(220f, 60f);
            image.gameObject.AddComponent<Button>().onClick.AddListener(action);
            Label(text, rect, art.medium, 21, ink, TextAnchor.MiddleCenter);
        }

        /// <summary>Hides the whole HUD (screenshots of the scenery only).</summary>
        public void SetVisible(bool visible) { if (canvasRect != null) canvasRect.gameObject.SetActive(visible); }

        public void OpenDialog() => dialog.gameObject.SetActive(true);
        public void CloseDialog() => dialog.gameObject.SetActive(false);

        /// <summary>
        /// The vehicle input is blocked: when it is the parked-only rule (real gear not P, or the car moving) a centred
        /// notice says so; any other short drop-out (input starting, stale samples) shows a small "waiting" tag after 1.5 s.
        /// The car itself is already held still by GameManager / the drive target.
        /// </summary>
        private void UpdateNotice()
        {
            var reason = PreviewBlock ?? target.BlockReason;
            if (reason != blockedReason) { blockedReason = reason; blockedSince = Time.unscaledTime; } // a new reason starts its own 1.5 s
            var parked = DriveTarget.IsParkedOnlyBlock(reason);
            parkedNotice.SetActive(parked);
            waitingNotice.SetActive(reason != null && !parked && Time.unscaledTime - blockedSince > 1.5f);
        }

        private void BuildNotice()
        {
            var shade = Img("Parked-only notice", canvasRect, null, new Color(0f, 0f, 0f, 0.38f), null);
            shade.raycastTarget = false;
            parkedNotice = shade.gameObject;
            var box = Img("Box", shade.rectTransform, art.round20, Panel, null);
            box.type = Image.Type.Sliced;
            box.raycastTarget = false;
            var rect = box.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(600f, 190f);
            var badge = Img("P", rect, art.circle, Lime, new Rect(40f, 51f, 88f, 88f), rect);
            badge.raycastTarget = false;
            Label("P", badge.rectTransform, art.semiBold, 46, OnLime, TextAnchor.MiddleCenter);
            var practice = info.Finish == "연습 마치기";
            var title = Label(practice ? "주차 중에만 연습할 수 있어요" : "주차 중에만 여행할 수 있어요", Box("Title", rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(152f, -52f), new Vector2(-180f, 40f), new Vector2(0f, 1f)), art.semiBold, 28, TextColor, TextAnchor.MiddleLeft);
            Label("기어를 P로 두고 차를 세우면 이어서 할 수 있어요.", Box("Body", rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(152f, -104f), new Vector2(-180f, 30f), new Vector2(0f, 1f)), art.regular, 18, Muted, TextAnchor.MiddleLeft);
            parkedNotice.SetActive(false);

            var tag = Img("Waiting for input", canvasRect, art.round14, Panel, null);
            tag.type = Image.Type.Sliced;
            tag.raycastTarget = false;
            var t = tag.rectTransform;
            t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 1f);
            t.anchoredPosition = new Vector2(0f, -88f);
            t.sizeDelta = new Vector2(300f, 46f);
            Label("차량 입력을 기다리는 중…", t, art.medium, 18, TextColor, TextAnchor.MiddleCenter);
            waitingNotice = tag.gameObject;
            waitingNotice.SetActive(false);
        }

        private void BuildSignal()
        {
            var panel = Img("Signal", canvasRect, art.round14, Panel, null);
            panel.type = Image.Type.Sliced;
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -28f);
            rect.sizeDelta = new Vector2(260f, 46f);
            signalText = Label("", rect, art.medium, 18, TextColor, TextAnchor.MiddleCenter);
            signalPanel = panel.gameObject;
            signalPanel.SetActive(false);
            var credit = target.Credit;
            if (!string.IsNullOrEmpty(credit))
            {
                // map data licence (OpenStreetMap ODbL) stays on screen, small, bottom centre
                var c = Label(credit, Box("Map credit", canvasRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(900f, 20f), new Vector2(0.5f, 0f)), art.regular, 12, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
                c.raycastTarget = false;
            }
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A rect in Figma coordinates (top-left origin) inside a parent's top-left corner.</summary>
        private static Rect? TopLeft(float x, float y, float w, float h) => new Rect(x, y, w, h);

        private static RectTransform Box(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Img(string name, RectTransform parent, Sprite sprite, Color color, Rect? topLeft, RectTransform unused = null)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            image.sprite = sprite;
            image.color = color;
            if (topLeft.HasValue)
            {
                var r = topLeft.Value;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(r.x, -r.y);
                rect.sizeDelta = new Vector2(r.width, r.height);
            }
            else Stretch(rect);
            return image;
        }

        private static Text Label(string text, RectTransform parent, Font font, int size, Color color, TextAnchor anchor)
        {
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            Stretch(label.rectTransform);
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            foreach (var m in mirrors) m?.Release();
            if (mapTexture != null) { mapTexture.Release(); Destroy(mapTexture); }
        }

        /// <summary>One live mirror: a disabled camera rendered by hand into a small texture every other frame.</summary>
        private sealed class MirrorView
        {
            private readonly Camera camera;
            private readonly Vector3 local;
            private readonly float yaw;
            public RenderTexture Texture { get; }

            public MirrorView(string name, Vector3 local, float yaw, float fov, int width, int height, Transform parent)
            {
                this.local = local; this.yaw = yaw;
                Texture = new RenderTexture(width, height, 16) { name = name };
                var go = new GameObject(name + " camera");
                go.transform.SetParent(parent, false);
                camera = go.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 500f;
                camera.allowHDR = false; camera.allowMSAA = false;
                camera.targetTexture = Texture;
            }

            public void Render(Transform car, Camera main)
            {
                if (main != null)
                {
                    camera.clearFlags = main.clearFlags;
                    camera.backgroundColor = main.backgroundColor;
                    camera.cullingMask = main.cullingMask;
                }
                camera.transform.SetPositionAndRotation(car.TransformPoint(local), car.rotation * Quaternion.Euler(3f, yaw, 0f));
                camera.Render();
            }

            public void Release() { if (Texture != null) Texture.Release(); }
        }
    }

    /// <summary>Scales a fixed-size artboard so it fills its parent (the cockpit drawing is authored at 1600x900).</summary>
    public sealed class ScaleToParent : MonoBehaviour
    {
        private void LateUpdate()
        {
            var parent = (RectTransform)transform.parent;
            var self = (RectTransform)transform;
            var s = parent.rect.width / self.sizeDelta.x;
            self.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Drag (or tap) on the gear track to move the knob; it snaps to R, N or D on release.</summary>
    public sealed class GearDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public DriveHud hud;
        public void OnPointerDown(PointerEventData e) => hud.DragKnob(hud.PanelTop(e.position, e.pressEventCamera));
        public void OnDrag(PointerEventData e) => hud.DragKnob(hud.PanelTop(e.position, e.pressEventCamera));
        public void OnPointerUp(PointerEventData e) => hud.ReleaseKnob();
    }
}
