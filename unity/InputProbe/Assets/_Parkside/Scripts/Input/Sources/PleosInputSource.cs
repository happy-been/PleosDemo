using System;
using UnityEngine;

namespace Parkside.Input
{
    public sealed class PleosInputSource : VehicleInputSource
    {
        private AndroidJavaClass bridge;
        public override string Description => "Pleos SDK + AVD console pedals";

        public override void Connect()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Disconnect();
            bridge = new AndroidJavaClass("com.example.pleosdemo.unity.PleosUnityBridge");
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                bridge.CallStatic("start", activity);
#else
            throw new InvalidOperationException("Pleos input requires the Android APK. Use KeyboardInputSource for PC testing.");
#endif
        }

        public override InputSnapshot ReadSnapshot()
        {
            if (bridge == null) throw new InvalidOperationException("Pleos source is not connected");
            return InputSnapshotInterpreter.Parse(bridge.CallStatic<string>("readSnapshot"));
        }

        public override void Disconnect()
        {
            if (bridge == null) return;
            try { bridge.CallStatic("stop"); }
            finally { bridge.Dispose(); bridge = null; }
        }
    }
}
