using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Explicit read-only saved-scene inspection. No generation, save, QualitySettings writes or probe updates.
public static class CloudPilotPortableValidation {
    const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
    static Camera eye;
    static string output;
    static int step=-3;
    static Vector3 position;
    static Quaternion rotation;
    static bool wasEnabled;
    static string originalHash;
    static readonly StringBuilder report=new StringBuilder();
    public static void Capture() {
        try {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-parkingCaptureOutput");
            if(index<0||index+1>=args.Length)throw new ArgumentException("Explicit -parkingCaptureOutput path required");
            output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);
            originalHash=Hash(ScenePath);
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var roots=SceneManager.GetActiveScene().GetRootGameObjects();
            var preserved=roots.Single(x=>x.name=="ParkingLot_V35_Preserved");
            var pilot=roots.Single(x=>x.name=="ParkingLot_CornerPilot_Root");
            if(preserved.activeSelf||!pilot.activeInHierarchy)throw new InvalidOperationException("Unexpected preserved/pilot activation");
            eye=pilot.GetComponentInChildren<Camera>();if(eye==null)throw new InvalidOperationException("Missing pilot camera");
            position=eye.transform.position;rotation=eye.transform.rotation;wasEnabled=eye.enabled;eye.enabled=false;
            report.AppendLine("Actual saved-scene Unity render; not a photograph, AI concept, video FPS or mobile benchmark.");
            report.AppendLine("Unity="+Application.unityVersion+" GPU="+SystemInfo.graphicsDeviceName+" API="+SystemInfo.graphicsDeviceType);
            report.AppendLine("Scene="+ScenePath+" SHA256="+originalHash);
            report.AppendLine("ProjectQuality shadows="+QualitySettings.shadows+" AA="+QualitySettings.antiAliasing+" colorSpace="+QualitySettings.activeColorSpace);
            report.AppendLine("Lightmaps="+LightmapSettings.lightmaps.Length+" LightingData="+AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset));
            foreach(var map in LightmapSettings.lightmaps)report.AppendLine("Lightmap="+AssetDatabase.GetAssetPath(map.lightmapColor));
            foreach(var probe in pilot.GetComponentsInChildren<ReflectionProbe>())report.AppendLine("Reflection="+probe.mode+" texture="+AssetDatabase.GetAssetPath(probe.customBakedTexture));
            foreach(var behaviour in eye.GetComponents<MonoBehaviour>())report.AppendLine("ActualCameraComponent="+(behaviour==null?"MISSING_SCRIPT":behaviour.GetType().FullName+" enabled="+behaviour.enabled));
            report.AppendLine("Actual saved camera and attached effects are rendered directly; no Camera.CopyFrom, no RenderProbe, no hidden quality override; RenderTexture MSAA1.");
            EditorApplication.update+=Tick;
        } catch(Exception e){Finish(e);}
    }
    static string Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
    static void Tick() {
        try {
            if(step<0)Render(null,position,rotation,960,540);
            else if(step==0)Render("local-eye.png",position,rotation,1920,1080);
            else if(step==1)Render("local-side3m.png",position+Vector3.right*3,rotation,1920,1080);
            else if(step==2)Render("local-aerial.png",new Vector3(23,26,-28),Quaternion.LookRotation(new Vector3(-23,-25,30)),1920,1080,48);
            else if(step<11)Render("movement-"+(step-3).ToString("D2")+".png",position+Vector3.forward*((step-3)*.45f),rotation,960,540);
            else if(step==11)Render("local-eye-repeat.png",position,rotation,1920,1080);
            else {Finish(null);return;}
            step++;
        } catch(Exception e){Finish(e);}
    }
    static void Render(string name,Vector3 p,Quaternion q,int w,int h,float fov=44) {
        var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){antiAliasing=1};
        var previous=RenderTexture.active;var oldTarget=eye.targetTexture;var oldFov=eye.fieldOfView;var pix=new Texture2D(w,h,TextureFormat.RGB24,false);
        try {
            rt.Create();eye.targetTexture=rt;eye.fieldOfView=fov;eye.transform.SetPositionAndRotation(p,q);eye.Render();
            if(name!=null){RenderTexture.active=rt;pix.ReadPixels(new Rect(0,0,w,h),0,0);pix.Apply();File.WriteAllBytes(Path.Combine(output,name),pix.EncodeToPNG());}
        } finally {eye.targetTexture=oldTarget;eye.fieldOfView=oldFov;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pix);}
    }
    static void Finish(Exception error) {
        EditorApplication.update-=Tick;
        if(eye!=null){eye.transform.SetPositionAndRotation(position,rotation);eye.enabled=wasEnabled;}
        if(error==null&&originalHash!=Hash(ScenePath))error=new IOException("Saved scene bytes changed during read-only validation");
        if(error!=null)Debug.LogException(error);
        report.AppendLine("FinalProjectQuality shadows="+QualitySettings.shadows+" AA="+QualitySettings.antiAliasing);
        report.AppendLine("SavedSceneUnchanged="+(originalHash==Hash(ScenePath))+" Result="+(error==null?"CAPTURE_COMPLETE":error.ToString()));
        if(output!=null)File.WriteAllText(Path.Combine(output,"local-validation.txt"),report.ToString());
        Debug.Log("PARKSIDE_PORTABLE_"+(error==null?"COMPLETE":"FAILED"));EditorApplication.Exit(error==null?0:1);
    }
}
}
