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
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
            for (var i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == 0);
            EditorSceneManager.SaveScene(scene, ParksideMenu.ScenePath);
            AddPracticeReturn();
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(ParksideMenu.ScenePath, true),
                new EditorBuildSettingsScene("Assets/_Parkside/Scenes/ParkingPractice.unity", true)
            };
            EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("PARKSIDE_MENU_AUTHORED screens=5");
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
        private static void Intro(Transform parent, string eyebrow, string title, string copy)
        {
            var column = Rect(parent, "Page introduction", 48, 142, 1344, 144);
            Label(column, eyebrow, 0, 0, 1300, 18, Primary, medium);
            Label(column, title, 0, 33.4f, 1300, 44, White, semibold);
            Label(column, copy, 0, 100.6f, 1300, 24, Muted);
        }
        private static void Footer(Transform parent)
        {
            Label(parent, "시뮬레이터 입력 · 실제 차량은 주차 상태", 48, 956, 1150, 18, Muted);
            Label(parent, "PLEOS CONCEPT", 1210, 956, 200, 18, Muted);
        }
        private static void Home(Transform parent)
        {
            Nav(parent, 0); Intro(parent, "DRIVE BEYOND PARKING", "멈춰 있는 차에서, 새로운 길로.", "익숙해질 때까지 연습하고, 낯선 도시를 함께 달려보세요.");
            var photos = new[] { "Parking", "Alps", "Seoul" }; var icons = new[] { "Parking", "Globe", "Users" };
            var badges = new[] { "01  BUILD CONFIDENCE", "02  EXPLORE THE WORLD", "03  DRIVE TOGETHER" };
            var titles = new[] { "연습", "주행", "멀티" }; var copies = new[] { "내 차의 감각을 익히는 시간", "가보지 못한 곳으로 떠나는 여행", "친구와 같은 풍경을 달리기" };
            var actions = new UnityAction[] { menu.ShowPractice, menu.ShowMaps, menu.ShowMultiplayerNotice };
            for (var i = 0; i < 3; i++)
            {
                var card = Photo(parent, "Destination · " + titles[i], "figmaHome-imgWorld" + photos[i], 48 + 456 * i, 330, 432, 502, 75);
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
            Button(parent, "연습 시작하기  →", 782, 858, 400, 64, menu.ShowSetup, false, true);
            Footer(parent);
        }
        private static void Practice(Transform parent)
        {
            Nav(parent, 1); Intro(parent, "PRACTICE", "오늘은 어떤 감각을 익혀볼까요?", "짧은 연습을 반복하며 내 차에 익숙해져요.");
            Button(parent, "주차 연습", 48, 300, 224, 72, menu.ShowPractice, true);
            Button(parent, "도로 연습", 288, 300, 224, 72, menu.ShowPracticeNotice);
            Label(parent, "기초부터 차근차근", 528, 322, 500, 22, Muted);
            var names = new[] { "후진 주차", "평행 주차", "전진 주차", "좁은 공간 탈출" };
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
                if (i == 0) Button(card, "선택  →", 432, 136, 200, 56, menu.ShowSetup, true);
                else
                {
                    var preview = Button(card, "모드 미리보기", 422, 138, 214, 56, menu.ShowPracticeNotice);
                    preview.image.color = Surface;
                    var label = preview.GetComponentInChildren<Text>(); label.fontSize = 18; label.color = Muted;
                }
            }
            Footer(parent);
        }
        private static void Setup(Transform parent)
        {
            Nav(parent, 1); Intro(parent, "PRACTICE / REVERSE PARKING", "후진 주차, 천천히 시작해요.", "안내를 따라 주차선 안에 차를 세워보세요.");
            var preview = Photo(parent, "Practice preview", "setup-imgWorldParking", 48, 304, 732, 538, 280);
            Badge(preview, "서울 · 기본 연습장", 24, 24, 240, White, true);
            Label(preview, "기본 연습장 미리보기", 28, 440, 680, 28, White, semibold);
            Label(preview, "차량 조작과 시점을 먼저 확인해보세요", 28, 485, 680, 22);
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
            Label(options, "현재 기본 연습장 · 난이도별 코스 준비 중", 28, 482, 540, 20, Muted);
            Badge(parent, "입력은 연습장에서 확인", 48, 875, 290, Primary);
            Button(parent, "←  모드 선택", 804, 862, 240, 72, menu.ShowPractice);
            var launch = Button(parent, "연습장 열기  →", 1064, 862, 328, 72, menu.LaunchPractice, true);
            Set(menu, "launchButton", launch); Set(menu, "launchLabel", launch.GetComponentInChildren<Text>());
            Footer(parent);
        }
        private static void Maps(Transform parent)
        {
            Nav(parent, 2); Intro(parent, "EXPLORE", "오늘의 목적지는 어디인가요?", "도시의 밤부터 알프스의 호숫길까지, 새로운 길을 만나보세요.");
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
            var featured = Photo(parent, "Go · mapDetail", "maps-imgWorldAlps", 48, 394, 818, 504, 180);
            Badge(featured, "추천 여행", 24, 24, 160, Primary, true);
            Label(featured, "스위스, 인터라켄", 28, 301, 750, 42, White, semibold);
            Label(featured, "호숫길 · 산악 도로 · 우측통행", 28, 363, 750, 24);
            Button(featured, "코스 보기  →", 28, 422, 762, 60, menu.ShowMapDetail, true);
            var cards = new GameObject[4]; cards[0] = featured.gameObject;
            var titles = new[] { "서울 한강", "도쿄 베이", "샌프란시스코" };
            var countries = new[] { "대한민국", "일본", "미국" };
            var descriptions = new[] { "도심 야경 · 우측통행", "도시 순환 · 좌측통행", "언덕길 · 우측통행" };
            for (var i = 0; i < 3; i++)
            {
                var card = Panel(parent, "Region · " + titles[i], 890, 394 + i * 175, 502, 155, Surface, round24);
                card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (i == 0) Art(card, "maps-imgWorldSeoul", 0, 0, 154, 155, true);
                else
                {
                    Panel(card, "Route thumbnail", 0, 0, 154, 155, Raised);
                    Art(card, "maps-imgSchematicRoad", 0, 0, 154, 155);
                    Art(card, "maps-imgIconGlobe", 61, 61, 32, 32);
                }
                Label(card, countries[i], 178, 19, 300, 18, Muted);
                Label(card, titles[i], 178, 51, 300, 28, White, semibold);
                Label(card, descriptions[i], 178, 102, 300, 18, Muted);
                var click = card.gameObject.AddComponent<Button>(); click.navigation = new Navigation { mode = Navigation.Mode.None };
                card.GetComponent<Image>().raycastTarget = true;
                UnityEventTools.AddPersistentListener(click.onClick, menu.ShowMapNotice);
                cards[i + 1] = card.gameObject;
            }
            Set(menu, "mapCards", cards);
            var empty = Label(parent, "표시할 코스가 없어요. 다른 지역이나 검색어를 선택해보세요.", 70, 536, 1260, 28, Muted);
            empty.gameObject.SetActive(false); Set(menu, "emptySearch", empty.gameObject);
            Footer(parent);
        }
        private static void MapDetail(Transform parent)
        {
            Nav(parent, 2); Intro(parent, "SWITZERLAND / INTERLAKEN", "알프스 호숫길을 따라.", "브리엔츠 호수에서 산 아래 작은 마을까지 달려보세요.");
            var preview = Photo(parent, "Route preview", "mapDetail-imgWorldAlps", 48, 302, 1344, 372, 100);
            Badge(preview, "스위스 · 우측통행", 24, 24, 240, White, true);
            var map = Panel(preview, "Mini map", 1066, 26, 252, 210, Surface, round20);
            map.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Panel(map, "Grid X", 0, 70, 252, 3, Border); Panel(map, "Grid Y", 75, 0, 3, 210, Border);
            Panel(map, "Grid X2", 0, 140, 252, 3, Border); Panel(map, "Grid Y2", 170, 0, 3, 210, Border);
            Art(map, "mapDetail-imgRoute", 0, 0, 252, 210);
            Art(map, "mapDetail-imgCurrentPosition", 83, 128, 18, 18);
            Art(map, "mapDetail-imgDestination", 210, 37, 15, 15);
            Panel(map, "Caption background", 0, 166, 252, 44, Surface);
            Label(map, "코스 미리보기", 16, 175, 220, 18, White, medium);
            Label(preview, "INTERLAKEN LAKESIDE", 28, 267, 900, 36, White, semibold);
            var metrics = new[] { "12.8 km", "18분", "여유롭게" }; var captions = new[] { "코스 길이 · 기획", "예상 시간 · 기획", "주행 난이도" };
            for (var i = 0; i < 3; i++)
            {
                Label(parent, metrics[i], 48 + i * 250, 720, 242, 36, i == 2 ? Primary : White, semibold);
                Label(parent, captions[i], 48 + i * 250, 774, 242, 20, Muted);
            }
            Button(parent, "맑음 · 낮", 820, 723, 274, 72, menu.ShowMapNotice, true);
            Button(parent, "교통량 적음", 1106, 723, 286, 72, menu.ShowMapNotice);
            Set(menu, "mapCameraChoices", new[] {
                Button(parent, "1인칭", 48, 848, 240, 72, menu.SelectFirstPerson, true),
                Button(parent, "3인칭", 304, 848, 240, 72, menu.SelectThirdPerson)
            });
            Button(parent, "← 맵 목록", 560, 848, 240, 72, menu.ShowMaps, false, true);
            Button(parent, "주행 맵 준비 중", 1024, 848, 368, 72, menu.ShowMapNotice, true);
            Footer(parent);
        }
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
            var scene = EditorSceneManager.OpenScene("Assets/_Parkside/Scenes/ParkingPractice.unity");
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
        public static void CapturePages()
        {
            Directory.CreateDirectory(Output);
            menu = Object.FindObjectOfType<ParksideMenu>(); renderCamera = Object.FindObjectOfType<Camera>();
            menu.ShowHome(); Capture("home"); menu.ShowPractice(); Capture("practice"); menu.ShowSetup(); Capture("setup");
            menu.ShowMaps(); Capture("maps"); menu.ShowMapDetail(); Capture("map-detail"); menu.ShowHome();
            Capture("home-widescreen", 1920, 1080);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
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
