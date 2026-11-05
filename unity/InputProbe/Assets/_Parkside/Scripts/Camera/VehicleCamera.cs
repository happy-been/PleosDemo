using UnityEngine;

namespace Parkside.Cameras
{
    [RequireComponent(typeof(Camera))]
    public sealed class VehicleCamera : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform chaseAnchor;
        [SerializeField] private Transform topAnchor;
        [SerializeField] private Transform driverAnchor;
        [SerializeField, Min(1f)] private float topViewSize = 10f;

        public int ViewIndex { get; private set; }
        public bool IsConfigured => viewCamera != null && chaseAnchor != null && topAnchor != null && driverAnchor != null;
        public void CycleView() => ViewIndex = (ViewIndex + 1) % 3;
        public void SetView(int index) => ViewIndex = Mathf.Clamp(index, 0, 2);

        private void LateUpdate()
        {
            if (!IsConfigured) return;
            var anchor = ViewIndex == 0 ? chaseAnchor : ViewIndex == 1 ? topAnchor : driverAnchor;
            viewCamera.orthographic = ViewIndex == 1;
            viewCamera.orthographicSize = topViewSize;
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }
    }
}
