using System;
using UnityEngine;

namespace Parkside.Input
{
    /// <summary>Polls the selected adapter and validates snapshots. Input details live in the sources.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class VehicleInputReceiver : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float pollInterval = 0.1f;
        [SerializeField] private VehicleInputSource desktopSource;
        [SerializeField] private VehicleInputSource androidSource;
        private InputSnapshot snapshot = new InputSnapshot { schemaVersion = 1 };
        private VehicleInputSource activeSource;
        private float lastRead = -100f;
        private float nextRead;
        private string error = "Waiting for input";
        private bool connected;
        private bool applicationPaused;
        private bool sourceEnabled;

        public InputSnapshot Snapshot => snapshot;
        public VehicleControlInput Controls { get; private set; } = VehicleControlInput.Blocked("Waiting for input");
        public string Description => SelectedSource != null ? SelectedSource.Description : "No input source assigned";
        public bool HasPlatformSources => desktopSource != null && androidSource != null;
        public VehicleInputSource DesktopSource => desktopSource;
        public VehicleInputSource AndroidSource => androidSource;
        public VehicleInputSource SelectedSource
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return androidSource;
#else
                return desktopSource;
#endif
            }
        }

        private void OnEnable() => Reconnect();
        private void OnDisable() => Disconnect();

        public void Reconnect()
        {
            Disconnect();
            if (!isActiveAndEnabled || applicationPaused) return;
            activeSource = SelectedSource;
            sourceEnabled = activeSource != null && activeSource.isActiveAndEnabled;
            if (!sourceEnabled)
            {
                error = "Assign and enable an input source in the scene";
                Controls = VehicleControlInput.Blocked(error);
                return;
            }
            try
            {
                activeSource.Connect();
                error = "";
                connected = true;
                nextRead = 0f;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                try { activeSource.Disconnect(); } catch (Exception cleanup) { Debug.LogWarning(cleanup.Message, this); }
                connected = false;
                Controls = VehicleControlInput.Blocked(error);
            }
        }

        private void Update()
        {
            if (applicationPaused) return;
            var selected = SelectedSource;
            var enabledSource = selected != null && selected.isActiveAndEnabled;
            if (selected != activeSource || enabledSource != sourceEnabled)
            {
                // A blocked frame pauses practice before changing adapters.
                Reconnect();
                return;
            }
            var now = Time.realtimeSinceStartup;
            if (connected && now >= nextRead)
            {
                nextRead = now + pollInterval;
                try
                {
                    snapshot = activeSource.ReadSnapshot();
                    lastRead = now;
                    error = "";
                }
                catch (Exception exception) { error = exception.Message; }
            }
            Controls = !string.IsNullOrEmpty(error) ? VehicleControlInput.Blocked(error)
                : InputSnapshotInterpreter.Interpret(snapshot, now - lastRead);
        }

        private void Disconnect()
        {
            connected = false;
            lastRead = -100f;
            Controls = VehicleControlInput.Blocked("Input is paused");
            if (activeSource == null) return;
            try { activeSource.Disconnect(); }
            catch (Exception exception) { Debug.LogWarning(exception.Message, this); }
            finally { activeSource = null; }
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) Disconnect();
            else if (isActiveAndEnabled) Reconnect();
        }
    }
}
