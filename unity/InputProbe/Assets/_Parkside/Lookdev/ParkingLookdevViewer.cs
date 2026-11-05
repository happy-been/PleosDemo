using UnityEngine;
using UnityEngine.UI;
using PreviewInput = UnityEngine.Input;

namespace Parkside.Lookdev
{
    /// <summary>Local visual inspection only; independent of the practice session and Pleos input.</summary>
    public sealed class ParkingLookdevViewer : MonoBehaviour
    {
        [SerializeField] private Transform[] viewpoints;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Canvas helpCanvas;
        [SerializeField] private Text viewLabel;
        private float speed;
        private int view;
        private ShadowQuality previousShadows;
        private ShadowResolution previousResolution;
        private float previousDistance;
        private int previousAA;
        private int previousFrameRate;
        private bool configured;
        public bool IsConfigured => viewpoints != null && viewpoints.Length == 4 && viewCamera != null && helpCanvas != null && viewLabel != null;

        private void OnEnable()
        {
            previousShadows = QualitySettings.shadows;
            previousResolution = QualitySettings.shadowResolution;
            previousDistance = QualitySettings.shadowDistance;
            previousAA = QualitySettings.antiAliasing;
            previousFrameRate = Application.targetFrameRate;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 55;
            QualitySettings.antiAliasing = 4;
            configured = true;
        }
        private void Start() { SetView(0); Application.targetFrameRate = 60; }
        private void Update()
        {
            for (var i = 0; i < 4; i++) if (PreviewInput.GetKeyDown(KeyCode.Alpha1 + i)) SetView(i);
            if (PreviewInput.GetKeyDown(KeyCode.Tab)) helpCanvas.enabled = !helpCanvas.enabled;
            if (PreviewInput.GetKeyDown(KeyCode.R)) SetView(view);
            if (PreviewInput.GetKeyDown(KeyCode.Escape)) speed = 0;
            if (view != 0) return;
            var throttle = PreviewInput.GetKey(KeyCode.W) ? 1 : PreviewInput.GetKey(KeyCode.S) ? -1 : 0;
            speed = PreviewInput.GetKey(KeyCode.Space) ? 0 : Mathf.MoveTowards(speed, throttle * 2, Time.deltaTime * 1.2f);
            var steering = PreviewInput.GetKey(KeyCode.LeftArrow) || PreviewInput.GetKey(KeyCode.A) ? -1 :
                PreviewInput.GetKey(KeyCode.RightArrow) || PreviewInput.GetKey(KeyCode.D) ? 1 : 0;
            var yaw = steering * speed * 12 * Time.deltaTime;
            transform.Rotate(0,yaw,0,Space.World);
            var forward = Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            var position = transform.position + forward * speed * Time.deltaTime;
            position.x = Mathf.Clamp(position.x,-14,14);
            position.z = Mathf.Clamp(position.z,-12,7.5f);
            transform.position = position;
        }
        public void SetView(int index)
        {
            if (!IsConfigured) return;
            view = Mathf.Clamp(index,0,3); speed = 0;
            transform.SetPositionAndRotation(viewpoints[view].position,viewpoints[view].rotation);
            viewCamera.fieldOfView = view == 3 ? 46 : 56;
            viewLabel.text = new[] { "01  운전자 높이 · 이동 가능", "02  주차면과 화단", "03  바닥 재질 가까이 보기", "04  주차장 전체" }[view];
        }
        private void OnDisable()
        {
            if (!configured) return;
            QualitySettings.shadows = previousShadows;
            QualitySettings.shadowResolution = previousResolution;
            QualitySettings.shadowDistance = previousDistance;
            QualitySettings.antiAliasing = previousAA;
            Application.targetFrameRate = previousFrameRate;
        }
    }
}
