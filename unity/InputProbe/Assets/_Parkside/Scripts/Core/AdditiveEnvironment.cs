using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parkside.Core
{
    /// <summary>
    /// Loads a separately authored environment scene (the large parking lot) next to this practice rig and leaves
    /// that scene's file untouched. The environment becomes the active scene so its sky, ambient light and fog apply.
    /// Its own preview camera stops rendering; the look settings of that camera (photo finish and its shadow quality)
    /// move to the rig's camera for as long as this scene runs.
    /// </summary>
    public sealed class AdditiveEnvironment : MonoBehaviour
    {
        [SerializeField] private string sceneName = "ParklingLot";
        [SerializeField] private Camera rigCamera;
        [SerializeField] private float shadowDistance = 85f;
        private ShadowQuality oldShadows;
        private float oldDistance;
        private ShadowResolution oldResolution;
        private int oldCascades;
        private bool changedQuality;

        /// <summary>True once the environment scene is loaded and prepared.</summary>
        public bool Ready { get; private set; }
        public string SceneName => sceneName;
        public bool IsConfigured => !string.IsNullOrEmpty(sceneName) && rigCamera != null;

        private IEnumerator Start()
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(sceneName)) { Debug.LogError("Environment scene is not in the build: " + sceneName); yield break; }
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByName(sceneName);
            }
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.isActiveAndEnabled && rigCamera != null)
                    {
                        // The rig was authored for a plain ground plane with a solid background; the lot has a sky.
                        rigCamera.clearFlags = camera.clearFlags;
                        rigCamera.backgroundColor = camera.backgroundColor;
                    }
                    camera.enabled = false;
                    foreach (var behaviour in camera.GetComponents<MonoBehaviour>())
                        if (behaviour.GetType().Name == "ParkingLotCamera") behaviour.enabled = false; // its preview hotkeys and per-camera quality switch
                        else if (behaviour.GetType().Name == "ParkingLotPhotoFinish" && rigCamera != null && rigCamera.GetComponent(behaviour.GetType()) == null)
                            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(behaviour), rigCamera.gameObject.AddComponent(behaviour.GetType()));
                }
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            oldShadows = QualitySettings.shadows; oldDistance = QualitySettings.shadowDistance;
            oldResolution = QualitySettings.shadowResolution; oldCascades = QualitySettings.shadowCascades;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh; QualitySettings.shadowCascades = 2;
            changedQuality = true;
            Ready = true;
        }

        private void OnDestroy()
        {
            if (!changedQuality) return;
            QualitySettings.shadows = oldShadows; QualitySettings.shadowDistance = oldDistance;
            QualitySettings.shadowResolution = oldResolution; QualitySettings.shadowCascades = oldCascades;
        }
    }
}
