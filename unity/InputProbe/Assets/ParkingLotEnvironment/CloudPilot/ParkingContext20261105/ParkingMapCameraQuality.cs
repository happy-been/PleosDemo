using UnityEngine;
namespace ParkingLotEnvironment.CloudPilot {
[ExecuteAlways,RequireComponent(typeof(Camera)),DisallowMultipleComponent]
public sealed class ParkingMapCameraQuality:MonoBehaviour {
 int originalAA;bool held;
 void OnEnable(){GetComponent<Camera>().allowMSAA=true;}
 void OnPreCull(){if(held)return;originalAA=QualitySettings.antiAliasing;held=true;QualitySettings.antiAliasing=4;}
 void OnPostRender(){Restore();}
 void Restore(){if(!held)return;QualitySettings.antiAliasing=originalAA;held=false;}
 void OnDisable(){Restore();}
 void OnDestroy(){OnDisable();}
}}
