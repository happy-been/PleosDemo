using UnityEngine;
namespace ParkingLotEnvironment.CloudPilot {
[ExecuteAlways,RequireComponent(typeof(Camera))]
public sealed class MapCameraFXAA:MonoBehaviour {
 public Shader edgeShader; Material material;
 void OnRenderImage(RenderTexture source,RenderTexture destination){if(edgeShader==null||!edgeShader.isSupported){Graphics.Blit(source,destination);return;}if(material==null){material=new Material(edgeShader){hideFlags=HideFlags.HideAndDontSave};}Graphics.Blit(source,destination,material);}
 void OnDisable(){if(material!=null){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
}}
