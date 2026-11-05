using Parkside.Cameras;
using Parkside.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parkside.UI
{
    public sealed class PracticeMenuLink : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private VehicleCamera vehicleCamera;
        private bool loading;
        private void Start() => vehicleCamera.SetView(ParksideMenu.SelectedCameraView);
        private void Update() { if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) ReturnToMenu(); }
        public void ReturnToMenu()
        {
            if (loading) return;
            gameManager.PausePractice();
            loading = true;
            SceneManager.LoadSceneAsync(ParksideMenu.ScenePath);
        }
    }
}
