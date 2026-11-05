using UnityEngine;

namespace ParkingLotEnvironment.CloudPilot
{
    [ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class MapDaylightGrade : MonoBehaviour
    {
        public Shader gradeShader;
        [Range(-2, 2)] public float exposureEV = 1f;
        [Range(.5f, 1.5f)] public float contrast = 1.08f;
        [Range(0, 1.5f)] public float saturation = 1.12f;
        Material material;

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (gradeShader == null || !gradeShader.isSupported)
            {
                Graphics.Blit(source, destination);
                return;
            }
            if (material != null && material.shader != gradeShader) ReleaseMaterial();
            if (material == null) material = new Material(gradeShader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_Exposure", Mathf.Pow(2, exposureEV));
            material.SetFloat("_Contrast", contrast);
            material.SetFloat("_Saturation", saturation);
            Graphics.Blit(source, destination, material);
        }

        void OnDisable() { ReleaseMaterial(); }
        void ReleaseMaterial()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }
    }
}
