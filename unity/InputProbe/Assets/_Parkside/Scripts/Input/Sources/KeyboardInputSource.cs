using System;
using UnityEngine;

namespace Parkside.Input
{
    public sealed class KeyboardInputSource : VehicleInputSource
    {
        [SerializeField] private KeyCode acceleratorKey = KeyCode.W;
        [SerializeField] private KeyCode brakeKey = KeyCode.Space;
        [SerializeField] private KeyCode steerLeftKey = KeyCode.LeftArrow;
        [SerializeField] private KeyCode steerRightKey = KeyCode.RightArrow;
        [SerializeField, Range(1f, 60f)] private float steeringAngle = 35f;
        private bool connected;
        private long sequence;

        public override string Description => "Keyboard: " + acceleratorKey + " throttle / " + brakeKey + " brake";
        public KeyCode AcceleratorKey => acceleratorKey;
        public KeyCode BrakeKey => brakeKey;
        public KeyCode SteerLeftKey => steerLeftKey;
        public KeyCode SteerRightKey => steerRightKey;

        public override void Connect()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            throw new InvalidOperationException("Keyboard input is for PC testing. Assign PleosInputSource for the Android APK.");
#else
            connected = true;
#endif
        }

        public override InputSnapshot ReadSnapshot()
        {
            if (!connected) throw new InvalidOperationException("Keyboard source is not connected");
            return CreateSnapshot(UnityEngine.Input.GetKey(acceleratorKey), UnityEngine.Input.GetKey(brakeKey),
                UnityEngine.Input.GetKey(steerLeftKey), UnityEngine.Input.GetKey(steerRightKey));
        }

        public InputSnapshot CreateSnapshot(bool accelerating, bool braking, bool steeringLeft, bool steeringRight)
        {
            var steering = ((steeringRight ? 1f : 0f) - (steeringLeft ? 1f : 0f)) * steeringAngle;
            return new InputSnapshot {
                schemaVersion = 1, sequence = ++sequence, source = "KEYBOARD PREVIEW",
                active = true, allowed = true, realGear = 4, realSpeedMps = 0f,
                steeringDegrees = steering, steeringNormalized = steering / steeringAngle,
                acceleratorPercent = accelerating ? 100f : 0f, throttle = accelerating ? 1f : 0f,
                brakePercent = braking ? 100f : 0f, brake = braking ? 1f : 0f, sdkBrakePressed = braking,
                steeringAgeMs = 0, pedalAgeMs = 0
            };
        }

        public override void Disconnect() => connected = false;
    }
}
