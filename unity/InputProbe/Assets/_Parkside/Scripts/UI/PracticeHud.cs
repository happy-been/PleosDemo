using Parkside.Cameras;
using Parkside.Core;
using Parkside.Diagnostics;
using Parkside.Input;
using Parkside.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace Parkside.UI
{
    /// <summary>Updates scene-authored Canvas elements. Layout, styles and button events live in the scene.</summary>
    public sealed class PracticeHud : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private VehicleInputReceiver inputReceiver;
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private VehicleCamera vehicleCamera;
        [SerializeField] private InputDiagnostics diagnostics;

        [Header("Canvas readouts")]
        [SerializeField] private Text sourceText;
        [SerializeField] private Text steeringText;
        [SerializeField] private Text acceleratorText;
        [SerializeField] private Text brakeText;
        [SerializeField] private Text sdkBrakeText;
        [SerializeField] private Text realVehicleText;
        [SerializeField] private Text sampleAgeText;
        [SerializeField] private Text frameRateText;
        [SerializeField] private Text speedText;
        [SerializeField] private Text blockReasonText;

        [Header("Canvas state indicators")]
        [SerializeField] private GameObject readyStatus;
        [SerializeField] private GameObject blockedStatus;
        [SerializeField] private Button startPauseButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button directionButton;
        [SerializeField] private Button cameraButton;
        [SerializeField] private Button reconnectButton;
        [SerializeField] private GameObject startLabel;
        [SerializeField] private GameObject pauseLabel;
        [SerializeField] private GameObject forwardLabel;
        [SerializeField] private GameObject reverseLabel;
        [SerializeField] private Text cameraIndexText;
        public Button[] ControlButtons => new[] { startPauseButton, resetButton, directionButton, cameraButton, reconnectButton };

        public bool IsConfigured => gameManager != null && inputReceiver != null && vehicleController != null &&
            vehicleCamera != null && diagnostics != null && GetComponentInParent<Canvas>() != null &&
            sourceText != null && steeringText != null && acceleratorText != null && brakeText != null &&
            sdkBrakeText != null && realVehicleText != null && sampleAgeText != null && frameRateText != null &&
            speedText != null && blockReasonText != null && readyStatus != null && blockedStatus != null &&
            startPauseButton != null && resetButton != null && directionButton != null && cameraButton != null &&
            reconnectButton != null && startLabel != null && pauseLabel != null && forwardLabel != null &&
            reverseLabel != null && cameraIndexText != null;

        private void LateUpdate()
        {
            if (!IsConfigured) return;
            var snapshot = inputReceiver.Snapshot;
            sourceText.text = inputReceiver.Description;
            steeringText.text = snapshot.steeringDegrees.ToString("+0.0;-0.0;0.0") + " deg";
            acceleratorText.text = snapshot.acceleratorPercent.ToString("F1") + " %";
            brakeText.text = snapshot.brakePercent.ToString("F1") + " %";
            sdkBrakeText.text = snapshot.sdkBrakePressed ? "PRESSED" : "released";
            realVehicleText.text = snapshot.realGear + " / " + snapshot.realSpeedMps.ToString("F2") + " m/s";
            sampleAgeText.text = snapshot.pedalAgeMs + " ms";
            frameRateText.text = snapshot.sequence + " / " + diagnostics.FramesPerSecond.ToString("F1");
            speedText.text = vehicleController.SpeedKph.ToString("F1") + " km/h";
            blockReasonText.text = inputReceiver.Controls.BlockReason;
            readyStatus.SetActive(gameManager.InputReady);
            blockedStatus.SetActive(!gameManager.InputReady);
            startPauseButton.interactable = gameManager.InputReady;
            startLabel.SetActive(!gameManager.IsRunning);
            pauseLabel.SetActive(gameManager.IsRunning);
            forwardLabel.SetActive(!vehicleController.IsReversing);
            reverseLabel.SetActive(vehicleController.IsReversing);
            cameraIndexText.text = (vehicleCamera.ViewIndex + 1).ToString();
        }
    }
}
