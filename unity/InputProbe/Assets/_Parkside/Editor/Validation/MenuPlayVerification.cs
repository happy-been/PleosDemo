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
                    Click(menu, "ShowSetup"); Require(menu.CurrentPage == 2, "Setup navigation");
                    Click(menu, "SelectThirdPerson"); Require(ParksideMenu.SelectedCameraView == 0, "Third-person selection");
                    Click(menu, "SelectFirstPerson"); Require(ParksideMenu.SelectedCameraView == 2, "First-person selection");
                    menu.ShowMaps();
                    var search = menu.GetComponentInChildren<InputField>();
                    search.text = "인터라켄";
                    Require(menu.GetComponentsInChildren<RawImage>().Count(x => x.name.Contains("WorldAlps")) == 1, "Search retains matching map");
                    Require(!menu.GetComponentsInChildren<RawImage>().Any(x => x.name.Contains("WorldSeoul")), "Search hides nonmatching map");
                    search.text = ""; menu.FilterKorea();
                    Require(menu.GetComponentsInChildren<RawImage>().Any(x => x.name.Contains("WorldSeoul")) &&
                        !menu.GetComponentsInChildren<RawImage>().Any(x => x.name.Contains("WorldAlps")), "Korea filter");
                    menu.FilterAll(); menu.ShowMapDetail(); Require(menu.CurrentPage == 4, "Map detail navigation");
                    menu.ShowSetup(); Click(menu, "LaunchPractice"); stage = 1;
                }
                else if (stage == 1)
                {
                    if (SceneManager.GetActiveScene().name != "ParkingPractice") return;
                    var game = Object.FindObjectOfType<GameManager>(); var camera = Object.FindObjectOfType<VehicleCamera>();
                    if (game == null || camera == null || !game.InputReady) return;
                    Require(camera.ViewIndex == 2, "Selected camera applied in practice");
                    game.StartPractice(); Require(game.IsRunning, "Practice input can start");
                    var link = Object.FindObjectOfType<PracticeMenuLink>(); Require(link != null, "Return link is authored");
                    link.ReturnToMenu(); Require(!game.IsRunning, "Returning to menu stops practice before unloading"); stage = 2;
                }
                else if (stage == 2)
                {
                    if (menu == null || SceneManager.GetActiveScene().name != "MainMenu") return;
                    Require(menu.CurrentPage == 0, "Return reaches Home");
                    Finish(0, "PARKSIDE_MENU_SMOKE_PASS navigation, search, region filters, camera, launch, stop, return");
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
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int exitCode, string message)
        {
            SessionState.SetBool("ParksideMenuSmoke", false); EditorApplication.update -= Tick;
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview")));
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/ui-preview/play-verification.txt")), message);
            if (exitCode == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(exitCode);
        }
    }
}
