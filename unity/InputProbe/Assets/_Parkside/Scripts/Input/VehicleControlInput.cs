namespace Parkside.Input
{
    /// <summary>Validated controls consumed by the virtual vehicle.</summary>
    public readonly struct VehicleControlInput
    {
        public bool IsValid { get; }
        public float SteeringDegrees { get; }
        public float Throttle { get; }
        public float Brake { get; }
        public string BlockReason { get; }

        public VehicleControlInput(float steeringDegrees, float throttle, float brake)
        {
            IsValid = true;
            SteeringDegrees = steeringDegrees;
            Throttle = throttle;
            Brake = brake;
            BlockReason = "";
        }

        private VehicleControlInput(string reason)
        {
            IsValid = false;
            SteeringDegrees = 0f;
            Throttle = 0f;
            Brake = 1f;
            BlockReason = reason;
        }

        public static VehicleControlInput Blocked(string reason) => new VehicleControlInput(reason);
    }
}
