using System.Linq;
using Parkside.Cameras;
using Parkside.Core;
using Parkside.Diagnostics;
using Parkside.Input;
using Parkside.UI;
using Parkside.Vehicle;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parkside.Editor
{
    /// <summary>Checks saved scene wiring. Menu scenes do not need a practice vehicle or debug HUD.</summary>
    public static class PracticeSceneValidator
    {
        public static int ValidateBuildScenes()
        {
            var entries = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (entries.Length == 0) throw new BuildFailedException("No enabled build scenes");
            var checks = 1;
            foreach (var entry in entries)
            {
                var scene = SceneManager.GetSceneByPath(entry.path);
                var alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                try { checks += ValidateScene(scene); }
                finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
            }
            return checks;
        }

        public static int ValidateScene(Scene scene)
        {
            var checks = 0;
            void Require(bool condition, string message)
            {
                if (!condition) throw new BuildFailedException(scene.path + ": " + message);
                checks++;
            }

            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Require(!components.Any(component => component == null), "Missing script reference");
            var menus = components.OfType<ParksideMenu>().ToArray();
            if (menus.Length > 0)
            {
                Require(menus.Length == 1 && menus[0].IsConfigured, "Menu pages and control references are missing");
                Require(components.OfType<EventSystem>().Count() == 1, "Exactly one menu EventSystem is required");
                var menuCanvas = menus[0].GetComponent<Canvas>();
                Require(menuCanvas != null && menuCanvas.worldCamera != null && menuCanvas.GetComponent<CanvasScaler>() != null, "Menu scaling or render camera missing");
                foreach (var image in menus[0].GetComponentsInChildren<RawImage>(true))
                {
                    Require(image.texture != null && image.texture.width > 0 && image.texture.height > 0, "Empty Figma asset: " + image.name);
                    var slot = image.rectTransform.rect;
                    var aspect = image.texture.width * image.uvRect.width / (image.texture.height * image.uvRect.height);
                    Require(Mathf.Abs(aspect / (slot.width / slot.height) - 1) < 0.035f, "Figma asset proportions: " + image.name);
                }
                foreach (var button in menus[0].GetComponentsInChildren<Button>(true))
                    Require(button.onClick.GetPersistentEventCount() > 0 && button.onClick.GetPersistentTarget(0) != null, "Menu button has no action: " + button.name);
            }
            var practiceScene = components.Any(component => component is GameManager || component is VehicleController ||
                component is VehicleInputReceiver || component is VehicleCamera || component is PracticeHud);
            if (!practiceScene) return checks;

            var managers = components.OfType<GameManager>().ToArray();
            Require(managers.Length == 1 && managers[0].IsConfigured, "Exactly one configured GameManager is required");
            var receivers = components.OfType<VehicleInputReceiver>().ToArray();
            Require(receivers.Length == 1 && receivers[0].HasPlatformSources, "Input receiver platform adapters are missing");
            Require(components.OfType<VehicleController>().Count() == 1 && components.OfType<VehicleController>().All(value => value.IsConfigured), "Vehicle wheel references are missing");
            Require(components.OfType<VehicleCamera>().Count() == 1 && components.OfType<VehicleCamera>().All(value => value.IsConfigured), "Camera anchors are missing");
            var huds = components.OfType<PracticeHud>().ToArray();
            Require(huds.Length == 1 && huds[0].IsConfigured, "HUD references are missing");
            Require(components.OfType<InputDiagnostics>().Count() == 1 && components.OfType<InputDiagnostics>().All(value => value.IsConfigured), "Diagnostics references are missing");

            var canvas = huds[0].GetComponentInParent<Canvas>();
            Require(canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null), "HUD Canvas or its camera is missing");
            Require(canvas.GetComponent<CanvasScaler>() != null && canvas.GetComponent<GraphicRaycaster>() != null, "Canvas scaling or pointer input is missing");
            var eventSystems = components.OfType<EventSystem>().ToArray();
            Require(eventSystems.Length == 1 && eventSystems[0].GetComponent<StandaloneInputModule>() != null, "Scene EventSystem is missing");
            Require(!eventSystems[0].sendNavigationEvents, "Pedal and steering keys must not trigger UI navigation/submit");
            var buttons = huds[0].ControlButtons;
            var methods = new[] { "TogglePractice", "ResetPractice", "ToggleDirection", "CycleView", "ReconnectInput" };
            for (var i = 0; i < buttons.Length; i++)
            {
                var click = buttons[i].onClick;
                var expected = methods[i];
                Require(Enumerable.Range(0, click.GetPersistentEventCount()).Any(index =>
                    click.GetPersistentTarget(index) != null && click.GetPersistentMethodName(index) == expected), "Missing Canvas button binding: " + expected);
            }
            return checks;
        }
    }
}
