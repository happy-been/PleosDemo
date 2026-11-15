using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parkside.UI
{
    /// <summary>Navigation and state only. All visuals are authored and saved in MainMenu.unity.</summary>
    public sealed class ParksideMenu : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Parkside/Scenes/MainMenu.unity";
        public const string PracticeScene = "ParkingLotPractice";
        public static int SelectedCameraView { get; private set; } = 2;
        /// <summary>True after the menu opened a practice or drive scene, so that scene applies the menu's choices.</summary>
        public static bool LaunchedFromMenu { get; private set; }
        /// <summary>Menu font and rounded button sprite, for UI that drive scenes add at runtime.</summary>
        public static Font UiFont { get; private set; }
        public static Sprite ButtonSprite { get; private set; }

        public struct PracticeInfo
        {
            public string Name, Eyebrow, Title, Copy;
        }

        /// <summary>Practice modes. They all start in the same outdoor lot (ParklingLot): bays T1-T3 and P1-P2.</summary>
        public static readonly PracticeInfo[] Practices =
        {
            new PracticeInfo { Name = "후진 주차", Eyebrow = "PRACTICE / REVERSE PARKING", Title = "후진 주차, 천천히 시작해요.", Copy = "T1–T3 빈 칸 중 하나를 골라 주차선 안에 뒤로 세워보세요." },
            new PracticeInfo { Name = "평행 주차", Eyebrow = "PRACTICE / PARALLEL PARKING", Title = "평행 주차, 여유 공간을 익혀요.", Copy = "P1·P2 칸에 앞뒤 차와 간격을 두고 나란히 세워보세요." },
            new PracticeInfo { Name = "전진 주차", Eyebrow = "PRACTICE / FORWARD PARKING", Title = "전진 주차, 진입 각도를 익혀요.", Copy = "통로에서 크게 돌아 T1–T3 빈 칸에 앞으로 들어가보세요." },
            new PracticeInfo { Name = "좁은 공간 탈출", Eyebrow = "PRACTICE / TIGHT EXIT", Title = "좁은 공간 탈출, 앞뒤를 살펴요.", Copy = "주차된 차 사이에서 부딪히지 않게 빠져나와보세요." },
            new PracticeInfo { Name = "도로 연습", Eyebrow = "PRACTICE / ROAD", Title = "도로 위에서도, 차분하게.", Copy = "주차장 통로를 따라 차선 유지와 부드러운 가속·제동을 익혀보세요." },
        };

        public struct MapInfo
        {
            public string Scene, Country, CardTitle, CardCopy, Search, Eyebrow, Title, Copy, Badge, Display;
            public bool Korea;
            public string[] Metrics, Captions, Chips;
        }

        /// <summary>Drive maps in card order (0 is the large featured card). Figures come from docs/*-drive.md.</summary>
        public static readonly MapInfo[] Maps =
        {
            new MapInfo
            {
                Scene = "BusanLandmarks", Korea = true, Country = "대한민국", CardTitle = "부산, 명소 순환", CardCopy = "광안대교 · 해안 도로 · 우측통행",
                Search = "부산 busan 광안대교 광안리 해운대 서면 대한민국 korea",
                Eyebrow = "KOREA / BUSAN", Title = "광안대교를 건너 한 바퀴.", Copy = "해운대에서 출발해 광안대교나 광안리 해안을 지나 다시 해운대로 돌아와요.",
                Badge = "대한민국 · 우측통행", Display = "BUSAN LANDMARK LOOP",
                Metrics = new[] { "약 4.4 km", "92대", "순환" }, Captions = new[] { "광안대교 경로 길이", "신호를 지키는 주변 차량", "출발한 곳으로 돌아오는 코스" },
                Chips = new[] { "광안대교 경로 · 약 4.4 km", "광안리 해안 경로 · 약 3.6 km" },
            },
            new MapInfo
            {
                Scene = "JejuCoastalDrive", Korea = true, Country = "대한민국", CardTitle = "제주, 한라산 가는 길", CardCopy = "해안 도로 · 산길 · 우측통행",
                Search = "제주 jeju 한라산 중문 예래 1100 서귀포 대한민국 korea",
                Eyebrow = "KOREA / JEJU", Title = "바다에서 한라산까지.", Copy = "예래 해안에서 중문을 지나 1100도로를 따라 고지대까지 올라가요.",
                Badge = "대한민국 · 우측통행", Display = "JEJU COAST TO 1100",
                Metrics = new[] { "약 23.1 km", "최대 24대", "편도" }, Captions = new[] { "주 경로 길이", "목적지가 다른 주변 차량", "고지대가 종점인 코스" },
                Chips = new[] { "실제 고도 지형", "1–5 키로 구간 이동" },
            },
            new MapInfo
            {
                Scene = "ParisEiffelDrive", Korea = false, Country = "프랑스", CardTitle = "파리, 에펠탑과 개선문", CardCopy = "도심 대로 · 신호 · 우측통행",
                Search = "파리 paris 에펠탑 eiffel 개선문 샹젤리제 콩코르드 프랑스 france 해외",
                Eyebrow = "FRANCE / PARIS", Title = "센강을 건너 샹젤리제로.", Copy = "에펠탑에서 앵발리드와 콩코르드를 지나 개선문과 트로카데로까지 달려요.",
                Badge = "프랑스 · 우측통행", Display = "PARIS · TOUR EIFFEL",
                Metrics = new[] { "약 11.4 km", "9곳", "도심" }, Captions = new[] { "안내 코스 길이", "1–9 키로 고르는 출발 지점", "신호와 주변 차량이 있는 대로" },
                Chips = new[] { "에펠탑 · 개선문 · 콩코르드", "신호 · 주변 차량" },
            },
        };

        [SerializeField] private GameObject[] pages;
        [SerializeField] private GameObject notice;
        [SerializeField] private Text noticeText;
        [SerializeField] private Button[] cameraChoices;
        [SerializeField] private Button[] mapCameraChoices;
        [SerializeField] private Button[] filters;
        [SerializeField] private GameObject[] mapCards;
        [SerializeField] private GameObject emptySearch;
        [SerializeField] private InputField search;
        [SerializeField] private Button launchButton;
        [SerializeField] private Text launchLabel;
        [SerializeField] private Text setupEyebrow, setupTitle, setupCopy;
        [SerializeField] private Text detailEyebrow, detailTitle, detailCopy, detailBadge, detailDisplay;
        [SerializeField] private Text[] detailMetrics, detailCaptions, detailChips;
        [SerializeField] private RawImage detailImage;
        [SerializeField] private Texture[] mapImages;
        [SerializeField] private Button driveButton;
        [SerializeField] private Text driveLabel;
        [SerializeField] private Font uiFont;
        [SerializeField] private Sprite buttonSprite;
        private int page;
        private int filter;
        private int practice;
        private int map;
        private bool loading;
        private static readonly Color Primary = new Color32(213, 247, 121, 255);
        private static readonly Color Raised = new Color32(35, 44, 52, 255);
        private static readonly Color Ink = new Color32(20, 27, 18, 255);
        private static readonly Color White = new Color32(244, 246, 248, 255);
        public int CurrentPage => page;
        public int SelectedPractice => practice;
        public int SelectedMap => map;
        public string SetupTitle => setupTitle != null ? setupTitle.text : "";
        public string DetailTitle => detailTitle != null ? detailTitle.text : "";
        public bool IsConfigured => pages != null && pages.Length == 5 && notice != null && noticeText != null &&
            cameraChoices.Length == 2 && mapCameraChoices.Length == 2 && filters.Length == 4 &&
            mapCards.Length == Maps.Length && emptySearch != null && search != null && launchButton != null && launchLabel != null &&
            setupEyebrow != null && setupTitle != null && setupCopy != null &&
            detailEyebrow != null && detailTitle != null && detailCopy != null && detailBadge != null && detailDisplay != null &&
            detailMetrics.Length == 3 && detailCaptions.Length == 3 && detailChips.Length == 2 && detailImage != null &&
            mapImages.Length == Maps.Length && driveButton != null && driveLabel != null && uiFont != null && buttonSprite != null;

        private void Awake()
        {
            UiFont = uiFont; ButtonSprite = buttonSprite;
            ShowHome(); RefreshCameraChoices(); ApplyFilter();
        }
        private void Start() => Debug.Log("PARKSIDE_MENU_READY");
        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && !loading)
            {
                if (notice.activeSelf) DismissNotice();
                else if (page == 2) ShowPractice();
                else if (page == 4) ShowMaps();
                else ShowHome();
            }
        }
        public void ShowHome() => ShowPage(0);
        public void ShowPractice() => ShowPage(1);
        public void ShowSetup() => ShowPage(2);
        public void ShowMaps() => ShowPage(3);
        public void ShowMapDetail() => ShowPage(4);
        private void ShowPage(int index)
        {
            if (loading) return;
            page = index;
            for (var i = 0; i < pages.Length; i++) pages[i].SetActive(i == index);
            notice.SetActive(false);
        }

        /// <summary>Picks a practice mode and goes straight to the screen with the start button.</summary>
        public void ChoosePractice(int index)
        {
            if (loading) return;
            practice = Mathf.Clamp(index, 0, Practices.Length - 1);
            var info = Practices[practice];
            setupEyebrow.text = info.Eyebrow; setupTitle.text = info.Title; setupCopy.text = info.Copy;
            ShowSetup();
        }

        /// <summary>Fills the course screen with the chosen map and shows it.</summary>
        public void ChooseMap(int index)
        {
            if (loading) return;
            map = Mathf.Clamp(index, 0, Maps.Length - 1);
            var info = Maps[map];
            detailEyebrow.text = info.Eyebrow; detailTitle.text = info.Title; detailCopy.text = info.Copy;
            detailBadge.text = info.Badge; detailDisplay.text = info.Display;
            for (var i = 0; i < 3; i++) { detailMetrics[i].text = info.Metrics[i]; detailCaptions[i].text = info.Captions[i]; }
            for (var i = 0; i < 2; i++) detailChips[i].text = info.Chips[i];
            Cover(detailImage, mapImages[map]);
            ShowMapDetail();
        }

        /// <summary>Shows a texture filling the frame without stretching (the frame crops the longer side).</summary>
        public static void Cover(RawImage image, Texture texture)
        {
            image.texture = texture;
            var frame = image.rectTransform.rect;
            if (texture == null || frame.width <= 0 || frame.height <= 0) { image.uvRect = new Rect(0, 0, 1, 1); return; }
            var imageAspect = texture.width / (float)texture.height;
            var frameAspect = frame.width / frame.height;
            if (imageAspect > frameAspect) { var w = frameAspect / imageAspect; image.uvRect = new Rect((1 - w) / 2, 0, w, 1); }
            else { var h = imageAspect / frameAspect; image.uvRect = new Rect(0, (1 - h) / 2, 1, h); }
        }

        public void SelectFirstPerson() { SelectedCameraView = 2; RefreshCameraChoices(); }
        public void SelectThirdPerson() { SelectedCameraView = 0; RefreshCameraChoices(); }
        private void RefreshCameraChoices()
        {
            Select(cameraChoices, SelectedCameraView == 2 ? 0 : 1);
            Select(mapCameraChoices, SelectedCameraView == 2 ? 0 : 1);
        }
        private static void Select(Button[] buttons, int index)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].image.color = i == index ? Primary : Raised;
                buttons[i].GetComponentInChildren<Text>().color = i == index ? Ink : White;
            }
        }
        public void FilterAll() { filter = 0; ApplyFilter(); }
        public void FilterKorea() { filter = 1; ApplyFilter(); }
        public void FilterOverseas() { filter = 2; ApplyFilter(); }
        public void FilterFavorites() { filter = 3; ApplyFilter(); }
        public void SearchMaps(string _) => ApplyFilter();
        private void ApplyFilter()
        {
            Select(filters, filter);
            var query = search.text.Trim().ToLowerInvariant();
            var count = 0;
            for (var i = 0; i < mapCards.Length; i++)
            {
                var regionMatches = filter == 0 || filter == 1 && Maps[i].Korea || filter == 2 && !Maps[i].Korea;
                var visible = regionMatches && (query.Length == 0 || Maps[i].Search.Contains(query));
                mapCards[i].SetActive(visible);
                if (visible) count++;
            }
            emptySearch.SetActive(count == 0);
        }
        public void ShowMultiplayerNotice() => Message("멀티플레이는 준비 중이에요.\n지금은 혼자 연습하거나 주행 맵을 달릴 수 있어요.");
        public void ShowRecordsNotice() => Message("아직 저장된 연습 기록이 없어요.\n주차 판정과 기록 기능은 준비 중이에요.");
        public void ShowDifficultyNotice() => Message("난이도별 코스는 준비 중이에요.\n지금은 모든 연습을 같은 큰 주차장에서 해요.");
        public void ShowGuidanceNotice() => Message("주차 가이드와 음성 안내는 준비 중이에요.");
        public void DismissNotice() { if (!loading) notice.SetActive(false); }
        private void Message(string value) { if (loading) return; noticeText.text = value; notice.SetActive(true); }

        public void LaunchPractice() => Launch(PracticeScene, launchButton, launchLabel, "주차장 여는 중…");
        public void LaunchDrive() => Launch(Maps[map].Scene, driveButton, driveLabel, "맵 여는 중…");
        private void Launch(string scene, Button button, Text label, string wait)
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Message("이 화면을 찾을 수 없어요.\n빌드 목록에 " + scene + " 씬이 있는지 확인해 주세요."); return; }
            StartCoroutine(Load(scene, button, label, wait));
        }
        private IEnumerator Load(string scene, Button button, Text label, string wait)
        {
            loading = true;
            LaunchedFromMenu = true;
            button.interactable = false;
            label.text = wait;
            yield return SceneManager.LoadSceneAsync(scene);
        }
    }
}
