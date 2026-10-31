using System;
using UnityEngine;

namespace Parkside.Input
{
    /// <summary>Parses the bridge contract and keeps invalid/stale values out of the controller.</summary>
    public static class InputSnapshotInterpreter
    {
        public static InputSnapshot Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Empty input snapshot");
            var snapshot = JsonUtility.FromJson<InputSnapshot>(json);
            if (snapshot == null || snapshot.schemaVersion != 1)
                throw new ArgumentException("Unsupported input schema");
            return snapshot;
        }

        public static VehicleControlInput Interpret(InputSnapshot snapshot, float ageSeconds)
        {
            if (snapshot == null) return VehicleControlInput.Blocked("Waiting for input");
            if (!Finite(ageSeconds) || ageSeconds < 0f || ageSeconds > 0.65f)
                return VehicleControlInput.Blocked("Input snapshot is stale");
            if (snapshot.schemaVersion != 1) return VehicleControlInput.Blocked("Unsupported input schema");
            if (!snapshot.active || !snapshot.allowed)
                return VehicleControlInput.Blocked(string.IsNullOrEmpty(snapshot.blockReason)
                    ? "Input is not available" : snapshot.blockReason);
            if (snapshot.realGear != 4 || !Finite(snapshot.realSpeedMps) || Mathf.Abs(snapshot.realSpeedMps) > 0.01f)
                return VehicleControlInput.Blocked("Real vehicle must stay in P / 0 speed");
            if (snapshot.pedalAgeMs < 0 || snapshot.pedalAgeMs > 600 ||
                snapshot.steeringAgeMs < 0 || snapshot.steeringAgeMs > 3000)
                return VehicleControlInput.Blocked("Vehicle samples are stale");
            if (!Finite(snapshot.steeringDegrees) || !UnitRange(snapshot.throttle) || !UnitRange(snapshot.brake))
                return VehicleControlInput.Blocked("Invalid control values");
            return new VehicleControlInput(snapshot.steeringDegrees, snapshot.throttle, snapshot.brake);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool UnitRange(float value) => Finite(value) && value >= 0f && value <= 1f;
    }
}
