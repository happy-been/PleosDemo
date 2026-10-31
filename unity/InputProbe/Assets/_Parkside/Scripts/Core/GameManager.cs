using Parkside.Input;
using Parkside.Vehicle;
using UnityEngine;

namespace Parkside.Core
{
    /// <summary>Coordinates the practice session. Scene layout and input parsing belong elsewhere.</summary>
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private VehicleInputReceiver inputReceiver;
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Bounds practiceBounds = new Bounds(Vector3.zero, new Vector3(32f, 20f, 38f));

        public bool IsRunning { get; private set; }
        public bool InputReady => inputReceiver != null && inputReceiver.Controls.IsValid;
        public bool IsConfigured => inputReceiver != null && vehicleController != null && spawnPoint != null;

        private void Start()
        {
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (!IsConfigured) { Debug.LogError("GameManager scene references are missing", this); enabled = false; return; }
            ResetPractice();
        }

        private void Update()
        {
            if (!InputReady) PausePractice();
            vehicleController.Simulate(inputReceiver.Controls, IsRunning, Time.deltaTime);
            if (!practiceBounds.Contains(vehicleController.transform.position)) ResetPractice();
        }

        public void StartPractice() { if (IsConfigured && InputReady) IsRunning = true; }
        public void TogglePractice() { if (IsRunning) PausePractice(); else StartPractice(); }
        public void PausePractice() { IsRunning = false; if (vehicleController != null) vehicleController.Stop(); }

        public void ResetPractice()
        {
            PausePractice();
            if (IsConfigured) vehicleController.ResetToPose(spawnPoint.position, spawnPoint.rotation);
        }

        public void ToggleDirection() { PausePractice(); if (vehicleController != null) vehicleController.ToggleDirection(); }
        public void ReconnectInput() { PausePractice(); if (inputReceiver != null) inputReceiver.Reconnect(); }
        private void OnApplicationPause(bool paused) { if (paused) PausePractice(); }
        private void OnApplicationFocus(bool focused) { if (!focused) PausePractice(); }
        private void OnDisable() => PausePractice();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.84f, 0.97f, 0.47f);
            Gizmos.DrawWireCube(practiceBounds.center, practiceBounds.size);
        }
    }
}
