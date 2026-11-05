using UnityEngine;
namespace ParkingLotEnvironment {
// Scene-local preview only. Does not change the project's input or graphics settings.
[ExecuteAlways]
public class ParkingLotCamera : MonoBehaviour {
    public bool cinematicPreview;
    [Range(0,.02f)] public float foliageWindStrength=0;
    [System.NonSerialized] public float foliageWindPreviewOverride=-1;
    float oldWindStrength;
    float cinematicTime;
    ShadowQuality oldShadows; float oldDistance; ShadowResolution oldResolution; int oldCascades;
    bool changed;
    void OnPreCull() {
        oldWindStrength=Shader.GetGlobalFloat("_ParkingLotWindStrength");Shader.SetGlobalFloat("_ParkingLotWindStrength",foliageWindPreviewOverride>=0?foliageWindPreviewOverride:foliageWindStrength);
        oldShadows=QualitySettings.shadows;oldDistance=QualitySettings.shadowDistance;oldResolution=QualitySettings.shadowResolution;oldCascades=QualitySettings.shadowCascades;
        QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowDistance=85;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowCascades=2;changed=true;
    }
    void OnPostRender() { Restore(); }
    void OnDisable() { Restore(); }
    void Restore() {if(!changed)return;QualitySettings.shadows=oldShadows;QualitySettings.shadowDistance=oldDistance;QualitySettings.shadowResolution=oldResolution;QualitySettings.shadowCascades=oldCascades;Shader.SetGlobalFloat("_ParkingLotWindStrength",oldWindStrength);changed=false;}
    void Update() {
        if(!Application.isPlaying)return;
        var camera=GetComponent<Camera>();
        if(Input.GetKeyDown(KeyCode.Alpha5)){cinematicPreview=!cinematicPreview;cinematicTime=0;camera.orthographic=false;}
        if(cinematicPreview){cinematicTime+=Time.deltaTime;float t=.5f-.5f*Mathf.Cos(cinematicTime*.12f);transform.position=Vector3.Lerp(new Vector3(24,20,-28),new Vector3(9,26,-20),t);transform.LookAt(Vector3.Lerp(new Vector3(0,0,1),new Vector3(-2,0,3),t));camera.fieldOfView=52;if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Alpha4)||Input.GetKeyDown(KeyCode.Alpha6)||Input.GetMouseButton(1))cinematicPreview=false;else return;}
        if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Alpha3)||Input.GetKeyDown(KeyCode.Alpha6))camera.orthographic=false;
        if(Input.GetKeyDown(KeyCode.Alpha1)) {GetComponent<Camera>().fieldOfView=55; transform.position=new Vector3(23,7,-22);transform.LookAt(new Vector3(-2,.8f,0)); }
        if(Input.GetKeyDown(KeyCode.Alpha2)) {GetComponent<Camera>().fieldOfView=50; transform.position=new Vector3(19,1.65f,-10);transform.LookAt(new Vector3(-8,1.2f,-5)); }
        if(Input.GetKeyDown(KeyCode.Alpha3)) {GetComponent<Camera>().fieldOfView=44; transform.position=new Vector3(13,1.45f,-11);transform.LookAt(new Vector3(9,1,-3)); }
        if(Input.GetKeyDown(KeyCode.Alpha4)){camera.orthographic=true;camera.orthographicSize=25.5f;transform.position=new Vector3(0,54,0);transform.rotation=Quaternion.Euler(90,0,0);}
        if(Input.GetKeyDown(KeyCode.Alpha6)){camera.fieldOfView=50;transform.position=new Vector3(-19,1.65f,10);transform.LookAt(new Vector3(8,1.2f,5));}
        if(!Input.GetMouseButton(1))return;
        var e=transform.eulerAngles;e.x-=Input.GetAxis("Mouse Y")*2;e.y+=Input.GetAxis("Mouse X")*2;transform.eulerAngles=e;
        float speed=Input.GetKey(KeyCode.LeftShift)?12:4;
        var movement=new Vector3(Input.GetAxis("Horizontal"),0,Input.GetAxis("Vertical"));if(Input.GetKey(KeyCode.E))movement.y++;if(Input.GetKey(KeyCode.Q))movement.y--;
        transform.Translate(movement*speed*Time.deltaTime,Space.Self);
    }
}
}
