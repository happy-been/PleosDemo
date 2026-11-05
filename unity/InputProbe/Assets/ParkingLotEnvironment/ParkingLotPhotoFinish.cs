using UnityEngine;
namespace ParkingLotEnvironment {
[ExecuteAlways,RequireComponent(typeof(Camera))]
public sealed class ParkingLotPhotoFinish : MonoBehaviour {
    [Range(.2f,2)] public float exposure=.95f;
    [Range(0,2)] public float contactStrength=0;
    public Shader shader;
    Material material;
    void OnEnable(){}
    void OnRenderImage(RenderTexture source,RenderTexture destination){
        if(material==null){if(shader==null)shader=Shader.Find("Hidden/ParkingLot/PhotoFinish");if(shader==null||!shader.isSupported){Graphics.Blit(source,destination);return;}material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};}
        var camera=GetComponent<Camera>();material.SetFloat("_Exposure",exposure);material.SetFloat("_ContactStrength",contactStrength);material.SetFloat("_GammaInput",QualitySettings.activeColorSpace==ColorSpace.Gamma?1:0);
        material.SetVector("_ProjectionScale",new Vector4(camera.projectionMatrix.m00,camera.projectionMatrix.m11,0,0));
        Graphics.Blit(source,destination,material);
    }
    void OnDisable(){if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);material=null;}}
}
}
