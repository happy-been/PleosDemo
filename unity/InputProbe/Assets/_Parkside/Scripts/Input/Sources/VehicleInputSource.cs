using UnityEngine;

namespace Parkside.Input
{
    /// <summary>Replaceable adapter. Disconnect must be safe to call more than once.</summary>
    public abstract class VehicleInputSource : MonoBehaviour
    {
        public abstract string Description { get; }
        public abstract void Connect();
        public abstract InputSnapshot ReadSnapshot();
        public abstract void Disconnect();

        protected virtual void OnDisable() => Release();
        protected virtual void OnDestroy() => Release();

        private void Release()
        {
            try { Disconnect(); }
            catch (System.Exception exception) { Debug.LogWarning("Input cleanup: " + exception.Message, this); }
        }
    }
}
