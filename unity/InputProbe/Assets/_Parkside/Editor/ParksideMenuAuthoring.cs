using System;
using System.IO;
using System.Linq;
using Parkside.Cameras;
using Parkside.Core;
using Parkside.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parkside.Editor
{
    /// <summary>Explicit editor authoring command, never invoked by builds or at runtime.</summary>
    public static class ParksideMenuAuthoring
    {
        private const string UiRoot = "Assets/_Parkside/UI/";
        private static readonly Color CanvasColor = Hex("0c1014"), Surface = Hex("171d23"), Raised = Hex("232c34"),
            Border = Hex("35414c"), Primary = Hex("d5f779"), Ink = Hex("141b12"), White = Hex("f4f6f8"), Muted = Hex("aab7c2");
        private static Sprite round20, round24, round28, gradient;
        private static Font regular, medium, semibold, bold;
        private static ParksideMenu menu;
        private static Camera renderCamera;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview"));

        [MenuItem("PARKSIDE/Author Figma menu scene")]
        public static void Author()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring scenes");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            LoadAssets();
            AuthorLotPractice();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener));
            renderCamera = cameraObject.GetComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = CanvasColor;
            renderCamera.orthographic = true;
            renderCamera.nearClipPlane = 0.1f;
            renderCamera.farClipPlane = 20;
            renderCamera.transform.position = new Vector3(0, 0, -10);
            var canvasObject = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = renderCamera;
            canvas.planeDistance = 1;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 1000);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var viewport = Rect(canvas.transform, "Figma viewport · 1440 × 1000", 0, 0, 1440, 1000);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = Vector2.zero;
            var bg = viewport.gameObject.AddComponent<Image>();
            bg.sprite = round28; bg.type = Image.Type.Sliced; bg.color = CanvasColor;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            menu = canvasObject.AddComponent<ParksideMenu>();
            var pages = Enumerable.Range(0, 5).Select(i => Rect(viewport, new[] { "Home · 3-2", "Practice · 3-69", "Setup · 3-138", "Maps · 5-56", "MapDetail · 5-126" }[i], 0, 0, 1440, 1000)).ToArray();
            Home(pages[0]); Practice(pages[1]); Setup(pages[2]); Maps(pages[3]); MapDetail(pages[4]);
            Notice(viewport);
            Set(menu, "pages", pages.Select(p => p.gameObject).ToArray());
            Set(menu, "uiFont", regular); Set(menu, "buttonSprite", round20);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
            for (var i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == 0);
            EditorSceneManager.SaveScene(scene, ParksideMenu.ScenePath);
            AddPracticeReturn();
            EditorBuildSettings.scenes = BuildScenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("PARKSIDE_MENU_AUTHORED screens=5");
        }

        // Everything the menu opens. ParklingLot is loaded additively by the practice rig scene.
        public const string LotPracticePath = "Assets/_Parkside/Scenes/ParkingLotPractice.unity";
        private const string SmallPracticePath = "Assets/_Parkside/Scenes/ParkingPractice.unity";
        private const string LotPath = "Assets/_Parkside/Scenes/ParklingLot.unity";
        public static readonly string[] BuildScenes = {
            ParksideMenu.ScenePath, LotPracticePath, LotPath,
            "Assets/BusanEnvironment/Landmarks/BusanLandmarks.unity",
            "Assets/_Parkside/Scenes/JejuCoastalDrive.unity",
            "Assets/_Parkside/Scenes/ParisEiffelDrive.unity"
        };

        /// <summary>
        /// The large-lot practice scene: a copy of the small practice scene's driving rig (systems, camera, HUD, car,
        /// spawn) without its ground and sun. ParklingLot itself is not edited; AdditiveEnvironment loads it at runtime.
        /// </summary>
        public static void AuthorLotPractice()
        {
            var source = EditorSceneManager.OpenScene(SmallPracticePath, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(source, LotPracticePath, true);
            var scene = EditorSceneManager.OpenScene(LotPracticePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Environment" || root.name == "Sun") Object.DestroyImmediate(root);
            var roots = scene.GetRootGameObjects();
            var components = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            // The lot that is switched on in ParklingLot is a walled alley lot (about x -10..10, z -26..15): perpendicular
            // bays on the west side with practice bays T1-T3, parallel bays P1-P2 on the east side, the entrance at the south.
            // The car starts just inside the entrance in the aisle, facing into the lot (north).
            var spawn = roots.Single(root => root.name == "SpawnPoints").transform.Find("PracticeSpawn");
            spawn.SetPositionAndRotation(new Vector3(2f, 0f, -17f), Quaternion.identity);
            var game = components.OfType<GameManager>().Single();
            var gameSettings = new SerializedObject(game);
            gameSettings.FindProperty("practiceBounds").boundsValue = new Bounds(new Vector3(0f, 0f, -6f), new Vector3(22f, 20f, 46f)); // the walled lot and its entrance
            gameSettings.ApplyModifiedPropertiesWithoutUndo();
            var vehicle = components.OfType<Parkside.Vehicle.VehicleController>().Single();
            vehicle.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var vehicleSettings = new SerializedObject(vehicle);
            vehicleSettings.FindProperty("blockOnObstacles").boolValue = true;
            vehicleSettings.FindProperty("obstacleBox").vector3Value = new Vector3(2.18f, 1.0f, 4.64f); // PracticeCar body bounds
            vehicleSettings.ApplyModifiedPropertiesWithoutUndo();
            UseSharedCar(vehicle.transform, scene);
            var camera = components.OfType<VehicleCamera>().Single().GetComponent<Camera>();
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1000f); // as the lot's own cameras (neighbourhood and sky)
            camera.clearFlags = CameraClearFlags.Skybox; // the lot scene's sky (AdditiveEnvironment also copies the lot camera's setting)
            var systems = roots.Single(root => root.name == "Systems");
            var loader = systems.GetComponent<AdditiveEnvironment>() ?? systems.AddComponent<AdditiveEnvironment>();
            Set(loader, "rigCamera", camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// The shared player car (Tucson, as in Jeju and Paris) in place of PracticeCar's box body, saved in the scene.
        /// The box parts are switched off, not deleted: the front wheel transforms are still steered by VehicleController.
        /// The presentation HUD adopts this body ("Unified Tucson") instead of adding its own.
        /// </summary>
        private static void UseSharedCar(Transform car, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ParkingLotEnvironment/RealVehicles/hyundai-tucson/hyundai-tucson.prefab");
            if (prefab == null) { Debug.LogWarning("Tucson prefab not found; the lot keeps the box car"); return; }
            foreach (Transform child in car)
            {
                if (child.name == "CameraAnchors" || child.GetComponentInChildren<Camera>(true) != null) continue;
                if (child.GetComponentInChildren<Renderer>(true) != null) child.gameObject.SetActive(false);
            }
            var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            body.name = "Unified Tucson";
            body.transform.SetParent(car, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var renderers = body.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var centre = car.InverseTransformPoint(bounds.center);
            var bottom = car.InverseTransformPoint(bounds.min).y;
            body.transform.localPosition -= new Vector3(centre.x, bottom, centre.z);
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = car.gameObject.layer; t.gameObject.isStatic = false; }
        }

        private static void LoadAssets()
        {
            Directory.CreateDirectory(UiRoot + "Generated");
            round20 = Rounded(20); round24 = Rounded(24); round28 = Rounded(28);
            var texture = new Texture2D(2, 256, TextureFormat.RGBA32, false);
            for (var y = 0; y < 256; y++) for (var x = 0; x < 2; x++)
                texture.SetPixel(x, y, new Color(CanvasColor.r, CanvasColor.g, CanvasColor.b, 1 - y / 255f));
            texture.Apply();
            gradient = WriteSprite(texture, UiRoot + "Generated/Contrast.png", Vector4.zero);
            regular = Font("Regular"); medium = Font("Medium"); semibold = Font("SemiBold"); bold = Font("Bold");
            foreach (var file in Directory.GetFiles(UiRoot + "Art", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
        private static Font Font(string weight)
        {
            var value = AssetDatabase.LoadAssetAtPath<Font>(UiRoot + "Fonts/AstaSans-" + weight + ".ttf");
            if (value == null) throw new InvalidOperationException("Missing Asta Sans " + weight);
            return value;
        }
        private static Sprite Rounded(int radius)
        {
            var size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var alpha = Mathf.Clamp01(radius + 0.5f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)));
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply();
            return WriteSprite(texture, UiRoot + "Generated/Round" + radius + ".png", Vector4.one * radius);
        }
        private static Sprite WriteSprite(Texture2D texture, string file, Vector4 border)
        {
            File.WriteAllBytes(file, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(file);
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(file);
        }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, Color color, Sprite sprite = null)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple; image.raycastTarget = false;
            return rect;
        }
        private static Text Label(Transform parent, string copy, float x, float y, float width, int size, Color? color = null, Font font = null, float height = 0)
        {
            var label = Rect(parent, copy.Replace('\n', ' '), x, y, width, height > 0 ? height : size * 1.4f).gameObject.AddComponent<Text>();
            label.font = font != null ? font : regular; label.text = copy; label.fontSize = size;
            label.color = color ?? White; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = false;
            return label;
        }
        private static Button Button(Transform parent, string copy, float x, float y, float width, float height, UnityAction click, bool primary = false, bool ghost = false)
        {
            var rect = Panel(parent, copy, x, y, width, height, ghost ? CanvasColor : primary ? Primary : Raised, round20);
            rect.GetComponent<Image>().raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors; colors.highlightedColor = new Color(0.9f, 0.95f, 0.86f); colors.pressedColor = new Color(0.7f, 0.8f, 0.6f);
            button.colors = colors;
            if (click != null) UnityEventTools.AddPersistentListener(button.onClick, click);
            var label = Label(rect, copy, 8, 0, width - 16, 22, primary ? Ink : White, medium, height);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static void Badge(Transform parent, string copy, float x, float y, float width, Color? textColor = null, bool dark = false)
        {
            var rect = Panel(parent, "Badge · " + copy, x, y, width, 40, dark ? Surface : Raised, round20);
            Label(rect, copy, 16, 7, width - 28, 18, textColor ?? White, medium);
        }
        private static RawImage Art(Transform parent, string asset, float x, float y, float width, float height, bool cover = false)
        {
            // These originals have identical SHA-256 hashes; share textures across screens in the APK.
            var source = asset == "setup-imgWorldParking" ? "figmaHome-imgWorldParking" :
                asset == "maps-imgWorldAlps" || asset == "mapDetail-imgWorldAlps" ? "figmaHome-imgWorldAlps" :
                asset == "maps-imgWorldSeoul" ? "figmaHome-imgWorldSeoul" : asset;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(UiRoot + "Art/" + source + ".png");
            if (texture == null || texture.width == 0) throw new InvalidOperationException("Missing Figma asset: " + asset);
            var image = Rect(parent, asset, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            if (cover)
            {
                var imageAspect = texture.width / (float)texture.height;
                var frameAspect = width / height;
                if (imageAspect > frameAspect) { var uvWidth = frameAspect / imageAspect; image.uvRect = new Rect((1 - uvWidth) / 2, 0, uvWidth, 1); }
                else { var uvHeight = imageAspect / frameAspect; image.uvRect = new Rect(0, (1 - uvHeight) / 2, 1, uvHeight); }
            }
            return image;
        }
        private static RectTransform Photo(Transform parent, string name, string asset, float x, float y, float width, float height, float contrastTop)
        {
            var rect = Panel(parent, name, x, y, width, height, Surface, round28);
            rect.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Art(rect, asset, 0, 0, width, height, true);
            var fade = Panel(rect, "Image contrast", 0, contrastTop, width, height - contrastTop, Color.white);
            fade.GetComponent<Image>().sprite = gradient;
            return rect;
        }
        private static void Nav(Transform parent, int selected)
        {
            var row = Rect(parent, "App navigation", 40, 24, 1360, 64);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16; layout.childControlWidth = false; layout.childControlHeight = false;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.MiddleLeft;
            var brand = Rect(row, "Brand", 0, 0, 350, 64);
            Art(brand, "figmaHome-imgIconRoad", 0, 15, 34, 34);
            var home = Button(brand, "PARKSIDE", 46, 0, 230, 64, menu.ShowHome, false, true);
            var brandText = home.GetComponentInChildren<Text>(); brandText.font = bold; brandText.fontSize = 28; brandText.alignment = TextAnchor.MiddleLeft;
            brandText.rectTransform.anchoredPosition = Vector2.zero;
            Button(row, "연습", 0, 0, 140, 64, menu.ShowPractice, selected == 1, selected != 1);
            Button(row, "주행", 0, 0, 140, 64, menu.ShowMaps, selected == 2, selected != 2);
            Button(row, "멀티", 0, 0, 140, 64, menu.ShowMultiplayerNotice, false, true);
            var state = Rect(row, "Session state", 0, 0, 350, 64);
            Badge(state, "입력 확인 전", 0, 12, 132, Primary);
            Button(state, "내 기록", 144, 0, 140, 64, menu.ShowRecordsNotice, false, true);
            Panel(parent, "Divider", 40, 107, 1360, 1, Border);
        }
        private static Text[] Intro(Transform parent, string eyebrow, string title, string copy)
        {
            var column = Rect(parent, "Page introduction", 48, 142, 1344, 144);
            return new[] {
                Label(column, eyebrow, 0, 0, 1300, 18, Primary, medium),
                Label(column, title, 0, 33.4f, 1300, 44, White, semibold),
                Label(column, copy, 0, 100.6f, 1300, 24, Muted)
            };
        }
        private static void Choose(Button button, UnityAction<int> action, int index)
        {
            button.onClick.RemoveAllListeners();
            for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddIntPersistentListener(button.onClick, action, index);
        }
        private static void Footer(Transform parent)
        {
            Label(parent, "시뮬레이터 입력 · 실제 차량은 주차 상태", 48, 956, 1150, 18, Muted);
            Label(parent, "PLEOS CONCEPT", 1210, 956, 200, 18, Muted);
        }
        private static void Home(Transform parent)
        {
            Nav(parent, 0); Intro(parent, "DRIVE BEYOND PARKING", "멈춰 있는 차에서, 새로운 길로.", "익숙해질 때까지 연습하고, 낯선 도시를 함께 달려보세요.");
            // Practice and drive show the real lot and map; multiplayer (not built yet) keeps the Figma photo.
            var photos = new[] { LotArt, "maps-imgDriveBusan", "figmaHome-imgWorldSeoul" }; var icons = new[] { "Parking", "Globe", "Users" };
            var badges = new[] { "01  BUILD CONFIDENCE", "02  EXPLORE THE WORLD", "03  DRIVE TOGETHER" };
            var titles = new[] { "연습", "주행", "멀티" }; var copies = new[] { "내 차의 감각을 익히는 시간", "가보지 못한 곳으로 떠나는 여행", "친구와 같은 풍경을 달리기" };
            var actions = new UnityAction[] { menu.ShowPractice, menu.ShowMaps, menu.ShowMultiplayerNotice };
            for (var i = 0; i < 3; i++)
            {
                var card = Photo(parent, "Destination · " + titles[i], photos[i], 48 + 456 * i, 330, 432, 502, 75);
                Panel(card, "Image dim", 0, 0, 432, 502, new Color(CanvasColor.r, CanvasColor.g, CanvasColor.b, 0.12f));
                var click = card.gameObject.AddComponent<Button>(); click.navigation = new Navigation { mode = Navigation.Mode.None };
                card.GetComponent<Image>().raycastTarget = true; UnityEventTools.AddPersistentListener(click.onClick, actions[i]);
                Badge(card, badges[i], 24, 24, 302, White, true);
                Art(card, "figmaHome-imgIcon" + icons[i], 28, 276, 36, 36);
                Label(card, titles[i], 28, 326, 360, 42, White, semibold);
                Label(card, copies[i], 28, 386, 380, 22);
                Button(card, "시작하기  →", 24, 430, 384, 56, actions[i]);
            }
            Badge(parent, "최근 기록", 48, 870, 126, Muted);
            Label(parent, "아직 연습 기록이 없어요", 198, 875, 560, 22);
            Choose(Button(parent, "연습 시작하기  →", 782, 858, 400, 64, null, false, true), menu.ChoosePractice, 0);
            Footer(parent);
        }
        // Lot preview: a capture of ParklingLot when it exists, otherwise the Figma parking photo.
        private static string LotArt => File.Exists(UiRoot + "Art/setup-imgParkingLot.png") ? "setup-imgParkingLot" : "figmaHome-imgWorldParking";
        private static void Practice(Transform parent)
        {
            Nav(parent, 1); Intro(parent, "PRACTICE", "오늘은 어떤 감각을 익혀볼까요?", "짧은 연습을 반복하며 내 차에 익숙해져요.");
            Button(parent, "주차 연습", 48, 300, 224, 72, menu.ShowPractice, true);
            Choose(Button(parent, "도로 연습", 288, 300, 224, 72, null), menu.ChoosePractice, 4);
            Label(parent, "기초부터 차근차근 · 모든 연습은 같은 야외 주차장에서", 528, 322, 800, 22, Muted);
            var names = ParksideMenu.Practices.Take(4).Select(p => p.Name).ToArray();
            var copy = new[] { "주차선에 맞춰 뒤로 들어가기", "차 사이의 여유 공간 익히기", "회전 반경과 진입 각도 익히기", "앞뒤 간격을 확인하며 빠져나오기" };
            var badges = new[] { "초급 · 5분", "중급 · 7분", "초급 · 4분", "중급 · 6분" };
            var icons = new[] { "Parking", "Road1", "Arrow", "Expand" };
            for (var i = 0; i < 4; i++)
            {
                var card = Panel(parent, "Practice · " + names[i], 48 + i % 2 * 684, 400 + i / 2 * 240, 660, 216, i == 0 ? Primary : Surface, round24);
                if (i == 0) Panel(card, "Selected border inset", 2, 2, 656, 212, Surface, round24);
                Art(card, "practice-imgIcon" + icons[i], 28, 30, 36, 36);
                Label(card, names[i], 88, 28, 500, 30, White, semibold);
                Label(card, copy[i], 88, 77, 500, 22, Muted);
                Badge(card, badges[i], 28, 150, 170);
                // Every mode opens the start screen; the first card keeps Figma's highlighted look.
                Choose(Button(card, "선택  →", 432, 136, 200, 56, null, i == 0), menu.ChoosePractice, i);
            }
            Footer(parent);
        }
        private static void Setup(Transform parent)
        {
            var first = ParksideMenu.Practices[0];
            Nav(parent, 1); var intro = Intro(parent, first.Eyebrow, first.Title, first.Copy);
            Set(menu, "setupEyebrow", intro[0]); Set(menu, "setupTitle", intro[1]); Set(menu, "setupCopy", intro[2]);
            var preview = Photo(parent, "Practice preview", LotArt, 48, 304, 732, 538, 280);
            Badge(preview, "골목 주차장 · 연습 칸 5곳", 24, 24, 280, White, true);
            Label(preview, "주차된 차 사이에서 연습해요", 28, 440, 680, 28, White, semibold);
            Label(preview, "직각 주차 T1–T3 · 평행 주차 P1–P2 · 닿으면 멈춰요", 28, 485, 680, 22);
            var options = Panel(parent, "Practice options", 804, 304, 588, 538, Surface, round24);
            Label(options, "연습 설정", 28, 26, 530, 28, White, semibold);
            Label(options, "난이도", 28, 86, 530, 22, Muted);
            for (var i = 0; i < 3; i++) Button(options, new[] { "초급", "중급", "고급" }[i], 28 + i * 179, 127, 167, 72, menu.ShowDifficultyNotice, i == 0);
            Label(options, "시작 시점", 28, 223, 530, 22, Muted);
            var cameras = new[] {
                Button(options, "1인칭", 28, 264, 259, 64, menu.SelectFirstPerson, true),
                Button(options, "3인칭", 299, 264, 259, 64, menu.SelectThirdPerson)
            };
            Set(menu, "cameraChoices", cameras);
            Label(options, "주차 가이드 · 음성 안내", 28, 368, 360, 22);
            Button(options, "준비 중", 404, 356, 124, 40, menu.ShowGuidanceNotice).GetComponentInChildren<Text>().fontSize = 18;
            Label(options, "입력 방식  ·  PC 키보드 / Pleos", 28, 430, 530, 22, Muted);
            Label(options, "모든 연습은 같은 야외 주차장에서 해요 · 난이도별 코스 준비 중", 28, 482, 540, 20, Muted);
            Badge(parent, "입력은 연습장에서 확인", 48, 875, 290, Primary);
            Button(parent, "←  모드 선택", 804, 862, 240, 72, menu.ShowPractice);
            var launch = Button(parent, "연습 시작  →", 1064, 862, 328, 72, menu.LaunchPractice, true);
            Set(menu, "launchButton", launch); Set(menu, "launchLabel", launch.GetComponentInChildren<Text>());
            Footer(parent);
        }
        private static void Maps(Transform parent)
        {
            Nav(parent, 2); Intro(parent, "EXPLORE", "오늘의 목적지는 어디인가요?", "부산 바닷길부터 제주 산길, 파리 대로까지 새로운 길을 만나보세요.");
            var buttons = new[] {
                Button(parent, "전체", 48, 298, 132, 64, menu.FilterAll, true),
                Button(parent, "대한민국", 192, 298, 180, 64, menu.FilterKorea),
                Button(parent, "해외", 384, 298, 132, 64, menu.FilterOverseas),
                Button(parent, "즐겨찾기", 528, 298, 180, 64, menu.FilterFavorites)
            };
            Set(menu, "filters", buttons);
            var searchBox = Panel(parent, "Map search", 720, 298, 672, 64, Surface, round20);
            searchBox.GetComponent<Image>().raycastTarget = true;
            Art(searchBox, "maps-imgIconSearch", 22, 20, 24, 24);
            var input = searchBox.gameObject.AddComponent<InputField>();
            input.textComponent = Label(searchBox, "", 64, 17, 594, 22, White, regular, 30);
            input.placeholder = Label(searchBox, "도시나 지역을 찾아보세요", 64, 17, 594, 22, Muted, regular, 30);
            input.characterLimit = 80;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(input.onValueChanged, menu.SearchMaps);
            Set(menu, "search", input);
            var maps = ParksideMenu.Maps;
            var featured = Photo(parent, "Map · " + maps[0].Scene, MapArt[0], 48, 394, 818, 504, 180);
            Badge(featured, "추천 여행", 24, 24, 160, Primary, true);
            Label(featured, maps[0].CardTitle, 28, 301, 750, 42, White, semibold);
            Label(featured, maps[0].CardCopy, 28, 363, 750, 24);
            Choose(Button(featured, "코스 보기  →", 28, 422, 762, 60, null, true), menu.ChooseMap, 0);
            var cards = new GameObject[maps.Length]; cards[0] = featured.gameObject;
            for (var i = 1; i < maps.Length; i++)
            {
                var card = Panel(parent, "Map · " + maps[i].Scene, 890, 394 + (i - 1) * 175, 502, 155, Surface, round24);
                card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                Art(card, MapArt[i], 0, 0, 154, 155, true);
                Label(card, maps[i].Country, 178, 19, 300, 18, Muted);
                Label(card, maps[i].CardTitle, 178, 51, 310, 26, White, semibold);
                Label(card, maps[i].CardCopy, 178, 102, 310, 18, Muted);
                var click = card.gameObject.AddComponent<Button>(); click.navigation = new Navigation { mode = Navigation.Mode.None };
                card.GetComponent<Image>().raycastTarget = true;
                Choose(click, menu.ChooseMap, i);
                cards[i] = card.gameObject;
            }
            // The grid's last slot: no map yet, so a quiet note instead of a card.
            var more = Panel(parent, "Map · coming", 890, 394 + (maps.Length - 1) * 175, 502, 155, Surface, round24);
            Label(more, "다음 도시", 28, 19, 440, 18, Muted);
            Label(more, "새로운 도시를 준비하고 있어요", 28, 51, 440, 26, Muted, semibold);
            Label(more, "지금은 부산 · 제주 · 파리를 달릴 수 있어요", 28, 102, 440, 18, Muted);
            Set(menu, "mapCards", cards);
            Set(menu, "mapImages", MapArt.Select(name => (Object)AssetDatabase.LoadAssetAtPath<Texture2D>(UiRoot + "Art/" + name + ".png")).ToArray());
            var empty = Label(parent, "표시할 코스가 없어요. 다른 지역이나 검색어를 선택해보세요.", 70, 536, 1260, 28, Muted);
            empty.gameObject.SetActive(false); Set(menu, "emptySearch", empty.gameObject);
            Footer(parent);
        }
        private static void MapDetail(Transform parent)
        {
            // Filled with map 0 here; ParksideMenu.ChooseMap rewrites every field for the chosen map.
            var info = ParksideMenu.Maps[0];
            Nav(parent, 2); var intro = Intro(parent, info.Eyebrow, info.Title, info.Copy);
            Set(menu, "detailEyebrow", intro[0]); Set(menu, "detailTitle", intro[1]); Set(menu, "detailCopy", intro[2]);
            var preview = Photo(parent, "Route preview", MapArt[0], 48, 302, 1344, 372, 100);
            Set(menu, "detailImage", preview.GetComponentInChildren<RawImage>());
            var badge = Panel(preview, "Badge · map", 24, 24, 300, 40, Surface, round20);
            Set(menu, "detailBadge", Label(badge, info.Badge, 16, 7, 272, 18, White, medium));
            Set(menu, "detailDisplay", Label(preview, info.Display, 28, 267, 1200, 36, White, semibold));
            var metrics = new Object[3]; var captions = new Object[3];
            for (var i = 0; i < 3; i++)
            {
                metrics[i] = Label(parent, info.Metrics[i], 48 + i * 250, 712, 242, 36, i == 2 ? Primary : White, semibold);
                captions[i] = Label(parent, info.Captions[i], 48 + i * 250, 766, 236, 18, Muted);
            }
            Set(menu, "detailMetrics", metrics); Set(menu, "detailCaptions", captions);
            // Facts about the map, not settings: plain panels instead of Figma's weather and traffic buttons.
            var chips = new Object[2];
            for (var i = 0; i < 2; i++)
            {
                var chip = Panel(parent, "Map fact " + i, 820 + i * 292, 723, 280, 72, Raised, round20);
                var label = Label(chip, info.Chips[i], 12, 0, 256, 19, White, medium, 72); label.alignment = TextAnchor.MiddleCenter;
                chips[i] = label;
            }
            Set(menu, "detailChips", chips);
            Set(menu, "mapCameraChoices", new[] {
                Button(parent, "1인칭", 48, 848, 240, 72, menu.SelectFirstPerson, true),
                Button(parent, "3인칭", 304, 848, 240, 72, menu.SelectThirdPerson)
            });
            Button(parent, "← 맵 목록", 560, 848, 240, 72, menu.ShowMaps, false, true);
            var drive = Button(parent, "여행 시작  →", 1024, 848, 368, 72, menu.LaunchDrive, true);
            Set(menu, "driveButton", drive); Set(menu, "driveLabel", drive.GetComponentInChildren<Text>());
            Footer(parent);
        }
        private static readonly string[] MapArt = { "maps-imgDriveBusan", "maps-imgDriveJeju", "maps-imgDriveParis" };
        private static void Notice(Transform parent)
        {
            var overlay = Panel(parent, "Notice overlay", 0, 0, 1440, 1000, new Color(0, 0, 0, 0.65f));
            overlay.GetComponent<Image>().raycastTarget = true;
            var card = Panel(overlay, "Notice", 330, 340, 780, 320, Surface, round28);
            Label(card, "PARKSIDE", 40, 30, 700, 18, Primary, medium);
            var copy = Label(card, "준비 중인 기능이에요.", 40, 82, 700, 28, White, regular, 126);
            copy.lineSpacing = 1.2f;
            Button(card, "확인", 500, 222, 240, 64, menu.DismissNotice, true);
            Set(menu, "notice", overlay.gameObject); Set(menu, "noticeText", copy);
            overlay.gameObject.SetActive(false);
        }
        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target); var array = serialized.FindProperty(field); array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddPracticeReturn()
        {
            var scene = EditorSceneManager.OpenScene(LotPracticePath);
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var hud = components.OfType<PracticeHud>().Single();
            var canvas = hud.GetComponentInParent<Canvas>();
            var link = canvas.GetComponent<PracticeMenuLink>() ?? canvas.gameObject.AddComponent<PracticeMenuLink>();
            Set(link, "gameManager", components.OfType<GameManager>().Single());
            Set(link, "vehicleCamera", components.OfType<VehicleCamera>().Single());
            var old = canvas.transform.Find("메뉴로"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var button = Button(canvas.transform, "메뉴로", 0, 0, 140, 48, link.ReturnToMenu);
            var rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-24, -20);
            EditorSceneManager.SaveScene(scene);
        }

        public static void AuthorAndVerify()
        {
            Author();
            PracticeProjectValidation.RunChecks();
            CapturePages();
            SessionState.SetBool("ParksideMenuSmoke", true);
            EditorApplication.isPlaying = true;
        }
        /// <summary>Saves every menu screen (and each practice/map variant) to build/ui-preview. Run with MainMenu open.</summary>
        public static string CapturePages()
        {
            Directory.CreateDirectory(Output);
            if (SceneManager.GetActiveScene().path != ParksideMenu.ScenePath) EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            menu = Object.FindObjectOfType<ParksideMenu>(); renderCamera = Object.FindObjectOfType<Camera>();
            menu.ShowHome(); Capture("home"); menu.ShowPractice(); Capture("practice");
            for (var i = 0; i < ParksideMenu.Practices.Length; i++) { menu.ChoosePractice(i); Capture("setup-" + i); }
            menu.ShowMaps(); Capture("maps");
            for (var i = 0; i < ParksideMenu.Maps.Length; i++) { menu.ChooseMap(i); Capture("map-detail-" + i); }
            menu.ChoosePractice(0); menu.ChooseMap(0); menu.ShowHome();
            Capture("home-widescreen", 1920, 1080);
            // Captures change texts on the open scene; reopen the saved file instead of saving them.
            EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            return "captured " + Output;
        }
        /// <summary>Author the menu and practice scene, then run the scene checks. For the editor bridge.</summary>
        public static string AuthorAndCheck()
        {
            Author();
            PracticeProjectValidation.RunChecks();
            return CapturePages();
        }
        private static void Capture(string name, int width = 1440, int height = 1000)
        {
            var target = new RenderTexture(width, height, 24); var previous = RenderTexture.active;
            renderCamera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); renderCamera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), texture.EncodeToPNG());
            renderCamera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
        }
    }
}
