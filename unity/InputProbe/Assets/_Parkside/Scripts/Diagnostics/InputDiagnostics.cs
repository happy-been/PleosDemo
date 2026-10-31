using Parkside.Core;
using Parkside.Input;
using Parkside.Vehicle;
using UnityEngine;

namespace Parkside.Diagnostics
{
    public sealed class InputDiagnostics : MonoBehaviour
    {
        [SerializeField] private VehicleInputReceiver inputReceiver;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private VehicleController vehicleController;
        private float elapsed;
        private int frames;
        private float nextReport;
        public float FramesPerSecond { get; private set; }
        public bool IsConfigured => inputReceiver != null && gameManager != null && vehicleController != null;

        private void Start()
        {
            Debug.Log("PLEOS_PROBE_DEVICE unity=" + Application.unityVersion +
                " graphics=" + SystemInfo.graphicsDeviceType + " gpu=" + SystemInfo.graphicsDeviceName + " cpu=" + SystemInfo.processorType);
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed >= 1f) { FramesPerSecond = frames / elapsed; elapsed = 0f; frames = 0; }
            if (!IsConfigured || Time.realtimeSinceStartup < nextReport) return;
            nextReport = Time.realtimeSinceStartup + 1f;
            Debug.Log("PLEOS_PROBE " + JsonUtility.ToJson(inputReceiver.Snapshot) + " fps=" + FramesPerSecond.ToString("F1") +
                " inputReady=" + inputReceiver.Controls.IsValid + " running=" + gameManager.IsRunning +
                " virtualKph=" + vehicleController.SpeedKph.ToString("F2"));
        }
    }
}
