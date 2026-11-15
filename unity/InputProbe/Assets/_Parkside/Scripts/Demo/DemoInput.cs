using Parkside.Input;
using UnityEngine;

namespace Parkside.Demo
{
    /// <summary>
    /// Input source for the demo video: the same InputSnapshot the Pleos bridge sends (steering, pedals, the real car's
    /// gear), but set by the demo director instead of the Virtual Console. realGear 4 is P; anything else makes the
    /// interpreter block the input exactly as a car taken out of P would.
    /// </summary>
    public sealed class DemoInput : VehicleInputSource
    {
        public float steeringDegrees;      // road-wheel angle, as the bridge reports it (about ±35°)
        public float steeringNormalized;   // -1..1, positive to the right
        public float throttle, brake;      // 0..1
        public int realGear = 4;
        private long sequence;

        public override string Description => "Demo input (same snapshot as the Pleos bridge)";
        public override void Connect() { }
        public override void Disconnect() { }

        public override InputSnapshot ReadSnapshot() => new InputSnapshot
        {
            schemaVersion = 1, sequence = ++sequence, source = "DEMO",
            active = true, allowed = true, realGear = realGear, realSpeedMps = 0f,
            steeringDegrees = steeringDegrees, steeringNormalized = Mathf.Clamp(steeringNormalized, -1f, 1f),
            throttle = Mathf.Clamp01(throttle), brake = Mathf.Clamp01(brake),
            acceleratorPercent = Mathf.Clamp01(throttle) * 100f, brakePercent = Mathf.Clamp01(brake) * 100f,
            sdkBrakePressed = brake > 0.01f, steeringAgeMs = 0, pedalAgeMs = 0
        };

        public void Set(float steeringNormalized01, float throttle01, float brake01, float maxAngle = 35f)
        {
            steeringNormalized = Mathf.Clamp(steeringNormalized01, -1f, 1f);
            steeringDegrees = steeringNormalized * maxAngle;
            throttle = Mathf.Clamp01(throttle01);
            brake = Mathf.Clamp01(brake01);
        }
    }
}
