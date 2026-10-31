using Parkside.Input;
using UnityEngine;

namespace Parkside.Vehicle
{
    /// <summary>Simple low-speed preview controller. Replace this component when adding full physics.</summary>
    public sealed class VehicleController : MonoBehaviour
    {
        [SerializeField] private Transform frontLeftWheel;
        [SerializeField] private Transform frontRightWheel;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 2f;
        [SerializeField, Min(0.1f)] private float acceleration = 1.8f;
        [SerializeField, Min(0.1f)] private float wheelbase = 2.7f;
        [SerializeField, Range(1f, 60f)] private float maximumSteeringAngle = 35f;
        private int direction = 1;

        public float SpeedKph => SpeedMps * 3.6f;
        public float SpeedMps { get; private set; }
        public bool IsReversing => direction < 0;
        public bool IsConfigured => frontLeftWheel != null && frontRightWheel != null;

        public void Simulate(VehicleControlInput input, bool driving, float deltaTime)
        {
            var steering = input.IsValid ? Mathf.Clamp(input.SteeringDegrees, -maximumSteeringAngle, maximumSteeringAngle) : 0f;
            if (frontLeftWheel != null) frontLeftWheel.localRotation = Quaternion.Euler(0f, steering, 0f);
            if (frontRightWheel != null) frontRightWheel.localRotation = Quaternion.Euler(0f, steering, 0f);
            if (!driving || !input.IsValid) { Stop(); return; }

            var dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
            SpeedMps = input.Brake > 0.01f ? 0f : Mathf.MoveTowards(SpeedMps, input.Throttle * maximumSpeed, dt * acceleration);
            var yaw = direction * SpeedMps / wheelbase * Mathf.Tan(steering * Mathf.Deg2Rad);
            transform.Rotate(0f, yaw * Mathf.Rad2Deg * dt, 0f);
            transform.position += transform.forward * (direction * SpeedMps * dt);
        }

        public void Stop() => SpeedMps = 0f;

        public void ToggleDirection()
        {
            Stop();
            direction *= -1;
        }

        public void ResetToPose(Vector3 position, Quaternion rotation)
        {
            Stop();
            transform.SetPositionAndRotation(position, rotation);
            if (frontLeftWheel != null) frontLeftWheel.localRotation = Quaternion.identity;
            if (frontRightWheel != null) frontRightWheel.localRotation = Quaternion.identity;
        }
    }
}
