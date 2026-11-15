using System;
using System.IO;
using System.Linq;
using Parkside.Cameras;
using Parkside.Core;
using Parkside.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parkside.Editor
{
    /// <summary>Batch smoke test for the authored menu-to-practice-to-menu flow.</summary>
    [InitializeOnLoad]
    public static class MenuPlayVerification
    {
        private static int stage;
        private static double deadline;
        static MenuPlayVerification()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ParksideMenuSmoke", false)) return;
                stage = 0; deadline = EditorApplication.timeSinceStartup + 45;
                EditorApplication.update += Tick;
            };
        }
        private static void Tick()
        {
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Menu smoke test timed out");
                var menu = Object.FindObjectOfType<ParksideMenu>();
                if (stage == 0)
                {
                    if (menu == null) return;
                    Require(menu.IsConfigured && menu.CurrentPage == 0, "Menu starts at configured Home");
                    Click(menu, "ShowPractice"); Require(menu.CurrentPage == 1, "Practice navigation");
                    // Every practice choice goes straight to the start screen with its own title.
                    for (var i = 0; i < ParksideMenu.Practices.Length; i++)
                    {
                        menu.ShowPractice(); Click(menu, "ChoosePractice", i);
                        Require(menu.CurrentPage == 2 && menu.SelectedPractice == i && menu.SetupTitle == ParksideMenu.Practices[i].Title, "Practice choice " + i);
                    }
                    Click(menu, "SelectThirdPerson"); Require(ParksideMenu.SelectedCameraView == 0, "Third-person selection");
                    Click(menu, "SelectFirstPerson"); Require(ParksideMenu.SelectedCameraView == 2, "First-person selection");
                    menu.ShowMaps();
                    var search = menu.GetComponentInChildren<InputField>();
                    search.text = "광안대교";
                    Require(Visible(menu, "BusanLandmarks") && !Visible(menu, "JejuCoastalDrive") && !Visible(menu, "ParisEiffelDrive"), "Search keeps only the matching map");
                    search.text = ""; menu.FilterKorea();
                    Require(Visible(menu, "BusanLandmarks") && Visible(menu, "JejuCoastalDrive") && !Visible(menu, "ParisEiffelDrive"), "Korea filter");
                    menu.FilterOverseas();
                    Require(!Visible(menu, "BusanLandmarks") && !Visible(menu, "JejuCoastalDrive") && Visible(menu, "ParisEiffelDrive"), "Overseas filter");
                    menu.FilterAll();
                    for (var i = 0; i < ParksideMenu.Maps.Length; i++)
                    {
                        menu.ShowMaps(); Click(menu, "ChooseMap", i);
                        Require(menu.CurrentPage == 4 && menu.SelectedMap == i && menu.DetailTitle == ParksideMenu.Maps[i].Title, "Map choice " + i);
                        Require(Application.CanStreamedLevelBeLoaded(ParksideMenu.Maps[i].Scene), "Map scene is in the build: " + ParksideMenu.Maps[i].Scene);
                    }
                    menu.ShowPractice(); Click(menu, "ChoosePractice", 0); Click(menu, "LaunchPractice"); stage = 1;
                }
                else if (stage == 1)
                {
                    if (SceneManager.GetActiveScene().name != "ParklingLot") return; // the lot becomes active once the rig has loaded it
                    var game = Object.FindObjectOfType<GameManager>(); var camera = Object.FindObjectOfType<VehicleCamera>();
                    var environment = Object.FindObjectOfType<AdditiveEnvironment>();
                    if (game == null || camera == null || environment == null || !environment.Ready || !game.InputReady) return;
                    Require(camera.ViewIndex == 2, "Selected camera applied in practice");
                    game.StartPractice(); Require(game.IsRunning, "Practice input can start");
                    var link = Object.FindObjectOfType<PracticeMenuLink>(); Require(link != null, "Return link is authored");
                    link.ReturnToMenu(); Require(!game.IsRunning, "Returning to menu stops practice before unloading"); stage = 2;
                }
                else if (stage == 2)
                {
                    if (menu == null || SceneManager.GetActiveScene().name != "MainMenu") return;
                    Require(menu.CurrentPage == 0, "Return reaches Home");
                    Finish(0, "PARKSIDE_MENU_SMOKE_PASS practice choices, search, region filters, map details, camera, launch into the large lot, stop, return");
                }
            }
            catch (Exception error) { Finish(1, "PARKSIDE_MENU_SMOKE_FAIL " + error); }
        }
        private static void Click(ParksideMenu menu, string method)
        {
            var button = menu.GetComponentsInChildren<Button>().First(value => Enumerable.Range(0, value.onClick.GetPersistentEventCount())
                .Any(index => value.onClick.GetPersistentMethodName(index) == method));
            button.onClick.Invoke();
        }
        // A button whose saved int argument matches (practice and map choices).
        private static void Click(ParksideMenu menu, string method, int argument)
        {
            var button = menu.GetComponentsInChildren<Button>().First(value => Enumerable.Range(0, value.onClick.GetPersistentEventCount())
                .Any(index => value.onClick.GetPersistentMethodName(index) == method &&
                    new SerializedObject(value).FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(index)
                        .FindPropertyRelative("m_Arguments.m_IntArgument").intValue == argument));
            button.onClick.Invoke();
        }
        private static bool Visible(ParksideMenu menu, string scene) =>
            menu.GetComponentsInChildren<Transform>().Any(x => x.name == "Map · " + scene);
        /// <summary>Runs the smoke test in the open editor (the bridge); in batch mode AuthorAndVerify runs it.</summary>
        public static string StartInEditor()
        {
            if (EditorApplication.isPlaying) return "ERR stop Play first";
            SessionState.SetBool("ParksideMenuSmoke", true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ParksideMenu.ScenePath);
            EditorApplication.isPlaying = true;
            return "started; result in build/ui-preview/play-verification.txt";
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int exitCode, string message)
        {
            SessionState.SetBool("ParksideMenuSmoke", false); EditorApplication.update -= Tick;
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview")));
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview/play-verification.txt")), message);
            if (exitCode == 0) Debug.Log(message); else Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else EditorApplication.isPlaying = false; // started from the open editor: leave it running
        }
    }
}
