using UnityEngine;
using UnityEngine.Rendering;
namespace ParkingLotEnvironment.CloudPilot.Integrated {
[ExecuteAlways,RequireComponent(typeof(Camera)),DisallowMultipleComponent]
public sealed class ParkingLotIntegratedPhotoFinish : MonoBehaviour {
 public Shader shader;
 [Range(0f,2f)] public float contactStrength=.9f;
 [Range(.1f,2f)] public float sampleRadius=.65f;
 Camera cameraComponent;Material material;DepthTextureMode originalDepth;DepthTextureMode addedDepth;
 bool qualityHeld;ShadowQuality oldShadows;ShadowResolution oldResolution;float oldDistance;ShadowmaskMode oldMask;
 [System.NonSerialized] public int renderCount;
 [System.NonSerialized] public bool lastAppliedCorrectly;
 [System.NonSerialized] public bool lastRestoredCorrectly;
 void OnEnable(){cameraComponent=GetComponent<Camera>();originalDepth=cameraComponent.depthTextureMode;addedDepth=(DepthTextureMode.Depth|DepthTextureMode.DepthNormals)&~originalDepth;cameraComponent.depthTextureMode|=DepthTextureMode.Depth|DepthTextureMode.DepthNormals;}
 void ApplyQuality(){if(qualityHeld)return;oldShadows=QualitySettings.shadows;oldResolution=QualitySettings.shadowResolution;oldDistance=QualitySettings.shadowDistance;oldMask=QualitySettings.shadowmaskMode;qualityHeld=true;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=120f;QualitySettings.shadowmaskMode=ShadowmaskMode.DistanceShadowmask;lastAppliedCorrectly=QualitySettings.shadows==ShadowQuality.All&&QualitySettings.shadowResolution==ShadowResolution.VeryHigh&&Mathf.Abs(QualitySettings.shadowDistance-120f)<.01f&&QualitySettings.shadowmaskMode==ShadowmaskMode.DistanceShadowmask;renderCount++;}
 void OnPreCull(){ApplyQuality();}
 void OnPreRender(){ApplyQuality();}
 void OnPostRender(){RestoreQuality();}
 void RestoreQuality(){if(!qualityHeld)return;QualitySettings.shadows=oldShadows;QualitySettings.shadowResolution=oldResolution;QualitySettings.shadowDistance=oldDistance;QualitySettings.shadowmaskMode=oldMask;qualityHeld=false;lastRestoredCorrectly=QualitySettings.shadows==oldShadows&&QualitySettings.shadowResolution==oldResolution&&Mathf.Abs(QualitySettings.shadowDistance-oldDistance)<.01f&&QualitySettings.shadowmaskMode==oldMask;}
 void OnRenderImage(RenderTexture source,RenderTexture destination){if(!shader||!shader.isSupported){Graphics.Blit(source,destination);return;}if(!material)material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};material.SetFloat("_ContactStrength",contactStrength);material.SetFloat("_Radius",sampleRadius);material.SetVector("_ProjectionScale",new Vector4(cameraComponent.projectionMatrix.m00,cameraComponent.projectionMatrix.m11,0,0));material.SetFloat("_GammaInput",QualitySettings.activeColorSpace==ColorSpace.Gamma?1f:0f);Graphics.Blit(source,destination,material);}
 void OnDisable(){RestoreQuality();if(cameraComponent)cameraComponent.depthTextureMode&=~addedDepth;if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);material=null;}}
 void OnDestroy(){RestoreQuality();}
}
}

