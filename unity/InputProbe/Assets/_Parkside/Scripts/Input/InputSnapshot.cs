using System;

namespace Parkside.Input
{
    /// <summary>Schema v1 JSON contract returned by the Kotlin bridge.</summary>
    [Serializable]
    public sealed class InputSnapshot
    {
        public int schemaVersion;
        public long sequence;
        public string source;
        public bool active;
        public bool allowed;
        public string blockReason;
        public float steeringDegrees;
        public float steeringNormalized;
        public float acceleratorPercent;
        public float brakePercent;
        public float throttle;
        public float brake;
        public bool sdkBrakePressed;
        public int realGear;
        public float realSpeedMps;
        public long steeringAgeMs;
        public long pedalAgeMs;
    }
}
