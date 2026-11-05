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
        public static int SelectedCameraView { get; private set; } = 2;
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
        private int page;
        private int filter;
        private bool loading;
        private static readonly Color Primary = new Color32(213, 247, 121, 255);
        private static readonly Color Raised = new Color32(35, 44, 52, 255);
        private static readonly Color Ink = new Color32(20, 27, 18, 255);
        private static readonly Color White = new Color32(244, 246, 248, 255);
        public int CurrentPage => page;
        public bool IsConfigured => pages != null && pages.Length == 5 && notice != null && noticeText != null &&
            cameraChoices.Length == 2 && mapCameraChoices.Length == 2 && filters.Length == 4 &&
            mapCards.Length == 4 && emptySearch != null && search != null && launchButton != null && launchLabel != null;

        private void Awake() { ShowHome(); RefreshCameraChoices(); ApplyFilter(); }
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
            var names = new[] { "스위스 인터라켄 알프스 switzerland interlaken", "대한민국 서울 한강 seoul korea", "일본 도쿄 베이 tokyo japan", "미국 샌프란시스코 san francisco usa" };
            var count = 0;
            for (var i = 0; i < mapCards.Length; i++)
            {
                var regionMatches = filter == 0 || filter == 1 && i == 1 || filter == 2 && i != 1;
                var visible = regionMatches && (query.Length == 0 || names[i].Contains(query));
                mapCards[i].SetActive(visible);
                if (visible) count++;
            }
            emptySearch.SetActive(count == 0);
        }
        public void ShowMultiplayerNotice() => Message("멀티플레이는 준비 중이에요.\n지금은 혼자 주차 연습을 시작할 수 있어요.");
        public void ShowRecordsNotice() => Message("아직 저장된 연습 기록이 없어요.\n주차 판정과 기록 기능은 준비 중이에요.");
        public void ShowPracticeNotice() => Message("이 연습 모드는 준비 중이에요.\n현재 기본 연습장에서 차량 조작을 확인할 수 있어요.");
        public void ShowDifficultyNotice() => Message("현재 기본 연습장 미리보기만 제공해요.\n난이도별 코스는 준비 중이에요.");
        public void ShowGuidanceNotice() => Message("주차 가이드와 음성 안내는 준비 중이에요.");
        public void ShowMapNotice() => Message("이 주행 맵은 준비 중이에요.\n현재 코스 화면을 미리 볼 수 있어요.");
        public void DismissNotice() { if (!loading) notice.SetActive(false); }
        private void Message(string value) { if (loading) return; noticeText.text = value; notice.SetActive(true); }
        public void LaunchPractice()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded("ParkingPractice")) { Message("기본 연습장을 찾을 수 없어요."); return; }
            StartCoroutine(LoadPractice());
        }
        private IEnumerator LoadPractice()
        {
            loading = true;
            launchButton.interactable = false;
            launchLabel.text = "연습장 여는 중…";
            yield return SceneManager.LoadSceneAsync("ParkingPractice");
        }
    }
}
