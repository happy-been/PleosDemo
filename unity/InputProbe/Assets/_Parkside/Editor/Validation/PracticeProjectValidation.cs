using System;
using Parkside.Core;
using Parkside.Input;
using Parkside.Vehicle;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parkside.Editor
{
    public static class PracticeProjectValidation
    {
        private static int checks;

        [MenuItem("PARKSIDE/Validate scene and input")]
        public static void RunChecks()
        {
            checks = 0;
            var activeScene = SceneManager.GetActiveScene();
            var wasDirty = activeScene.isDirty;
            checks += PracticeSceneValidator.ValidateBuildScenes();
            var snapshot = InputSnapshotInterpreter.Parse("{\"schemaVersion\":1,\"active\":true,\"allowed\":true,\"realGear\":4,\"throttle\":0.37,\"brake\":0.5,\"steeringDegrees\":-25}");
            var input = InputSnapshotInterpreter.Interpret(snapshot, 0f);
            Require(input.IsValid && Mathf.Abs(input.Throttle - 0.37f) < 0.001f && input.Brake == 0.5f, "Input contract conversion");
            Require(!InputSnapshotInterpreter.Interpret(snapshot, 0.66f).IsValid, "Stale Unity snapshot must stop");
            snapshot.pedalAgeMs = 601;
            Require(!InputSnapshotInterpreter.Interpret(snapshot, 0f).IsValid, "Stale pedal sample must stop");
            snapshot.pedalAgeMs = 0;
            snapshot.realGear = 8;
            Require(!InputSnapshotInterpreter.Interpret(snapshot, 0f).IsValid, "Non-P gear must stop");
            snapshot.realGear = 4;
            snapshot.realSpeedMps = 1f;
            Require(!InputSnapshotInterpreter.Interpret(snapshot, 0f).IsValid, "Actual motion must stop practice");
            snapshot.realSpeedMps = 0f;
            snapshot.throttle = float.NaN;
            Require(!InputSnapshotInterpreter.Interpret(snapshot, 0f).IsValid, "NaN controls must stop");
            snapshot.throttle = 0.37f;
            snapshot.allowed = false;
            var blocked = InputSnapshotInterpreter.Interpret(snapshot, 0f);
            Require(!blocked.IsValid && blocked.Throttle == 0f && blocked.Brake == 1f, "Blocked input must fail closed");
            var rejectedSchema = false;
            try { InputSnapshotInterpreter.Parse("{\"schemaVersion\":2}"); }
            catch (ArgumentException) { rejectedSchema = true; }
            Require(rejectedSchema, "Unknown JSON schema must be rejected");
            var rejectedJson = false;
            try { InputSnapshotInterpreter.Parse("not json"); }
            catch (ArgumentException) { rejectedJson = true; }
            Require(rejectedJson, "Malformed JSON must be rejected");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Parkside/Prefabs/Vehicles/PracticeCar.prefab");
            Require(prefab != null, "Practice vehicle prefab is missing");
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var menu = new GameObject("Menu scene check");
                SceneManager.MoveGameObjectToScene(menu, previewScene);
                Require(PracticeSceneValidator.ValidateScene(previewScene) == 1, "Menu scenes must not require practice objects");
                menu.AddComponent<GameManager>();
                var brokenPracticeRejected = false;
                try { PracticeSceneValidator.ValidateScene(previewScene); }
                catch (BuildFailedException) { brokenPracticeRejected = true; }
                Require(brokenPracticeRejected, "Incomplete practice scenes must still be rejected");
                UnityEngine.Object.DestroyImmediate(menu);
                var sourceObject = new GameObject("Keyboard contract check");
                SceneManager.MoveGameObjectToScene(sourceObject, previewScene);
                var keyboard = sourceObject.AddComponent<KeyboardInputSource>();
                Require(keyboard.AcceleratorKey == KeyCode.W && keyboard.BrakeKey == KeyCode.Space, "Pedal keys must be W and Space");
                Require(keyboard.SteerLeftKey == KeyCode.LeftArrow && keyboard.SteerRightKey == KeyCode.RightArrow, "Steering uses arrow keys, not A/D");
                var accelerator = InputSnapshotInterpreter.Interpret(keyboard.CreateSnapshot(true, false, false, false), 0f);
                Require(accelerator.IsValid && accelerator.Throttle == 1f && accelerator.Brake == 0f && accelerator.SteeringDegrees == 0f, "Keyboard throttle uses the shared contract");
                var brake = InputSnapshotInterpreter.Interpret(keyboard.CreateSnapshot(false, true, false, false), 0f);
                Require(brake.IsValid && brake.Throttle == 0f && brake.Brake == 1f, "Keyboard brake uses the shared contract");
                Require(keyboard.CreateSnapshot(false, false, true, false).steeringDegrees < 0f &&
                    keyboard.CreateSnapshot(false, false, false, true).steeringDegrees > 0f, "Keyboard steering signs");
                Require(keyboard.CreateSnapshot(false, false, true, true).steeringDegrees == 0f, "Opposite steering keys cancel");
                keyboard.Connect();
                keyboard.Disconnect();
                var stoppedSource = false;
                try { keyboard.ReadSnapshot(); } catch (InvalidOperationException) { stoppedSource = true; }
                Require(stoppedSource, "Disconnected keyboard source must stop producing samples");
                var pleos = sourceObject.AddComponent<PleosInputSource>();
                var desktopPleosRejected = false;
                try { pleos.Connect(); } catch (InvalidOperationException) { desktopPleosRejected = true; }
                Require(desktopPleosRejected, "Pleos adapter must not silently fall back to keyboard in the Editor");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
                var controller = instance.GetComponent<VehicleController>();
                controller.ResetToPose(Vector3.zero, Quaternion.identity);
                for (var i = 0; i < 10; i++) controller.Simulate(new VehicleControlInput(0f, 1f, 0f), true, 0.1f);
                Require(controller.SpeedMps > 0f && instance.transform.position.z > 0f, "Forward movement");
                controller.Simulate(InputSnapshotInterpreter.Interpret(keyboard.CreateSnapshot(true, true, false, false), 0f), true, 0.1f);
                Require(controller.SpeedMps == 0f, "Space brake overrides W throttle");
                controller.Simulate(new VehicleControlInput(0f, 0f, 0.5f), true, 0.1f);
                Require(controller.SpeedMps == 0f, "Brake stops the preview");
                controller.ToggleDirection();
                var beforeReverse = instance.transform.position.z;
                controller.Simulate(new VehicleControlInput(0f, 1f, 0f), true, 0.1f);
                Require(instance.transform.position.z < beforeReverse, "Virtual reverse movement");
                controller.Simulate(VehicleControlInput.Blocked("Disconnected"), true, 0.1f);
                Require(controller.SpeedMps == 0f, "Disconnect stops the preview");
                controller.ResetToPose(new Vector3(1f, 0f, 2f), Quaternion.identity);
                Require(instance.transform.position == new Vector3(1f, 0f, 2f), "Reset uses the supplied scene spawn pose");
            }
            finally { EditorSceneManager.ClosePreviewScene(previewScene); }
            Require(activeScene.isDirty == wasDirty, "Validation must not modify the active scene");
            Debug.Log("PARKSIDE_VALIDATION_PASS checks=" + checks + " target=" + EditorUserBuildSettings.activeBuildTarget);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new BuildFailedException(message);
            checks++;
        }
    }
}
