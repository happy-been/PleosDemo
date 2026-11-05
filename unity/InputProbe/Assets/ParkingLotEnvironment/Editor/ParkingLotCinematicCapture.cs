using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Offline visual inspection of this scene only; never changes app input, build scenes or project quality.
[InitializeOnLoad]
public static class ParkingLotCinematicCapture {
    const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
    static readonly string Output=ParkingLotEditorPaths.Output;
    const int FrameCount=240;
    static Camera camera;
    static RenderTexture render,downsample;
    static Texture2D pixels;
    static int frame=-1;
    static bool walking;
    static bool windMovie;
    static bool utilityMovie;
    static GameObject orbitSubject;
    static float windOriginalStrength;
    public static bool IsCapturing { get { return frame>=0; } }
    static double nextFrame;
    static ParkingLotCinematicCapture(){EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Release;EditorApplication.quitting+=Release;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode&&frame>=0){Release();File.WriteAllText(Output+"/capture-cancelled.txt","Stopped before entering Play mode. "+DateTime.Now.ToString("s"));}};}
    static bool Take(string name){string p=Path.Combine(Output,name);try{if(!File.Exists(p))return false;File.Delete(p);return true;}catch(IOException){return false;}}
    static Camera FindCamera(){var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Open the saved ParklingLot scene first.");foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Camera>(true))if(c.name=="ParkingLot_PreviewCamera")return c;throw new InvalidOperationException("ParkingLot preview camera is missing.");}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{
            if(Take("cancel-capture.request")){Release();File.WriteAllText(Output+"/capture-cancelled.txt",DateTime.Now.ToString("s"));return;}
            // Keep still-image requests queued until an in-progress movie has released its resources.
            if(frame<0){
                if(Take("korean-stills.request"))Stills();
                if(Take("korean-driver-seat.request"))DriverSeatViews();
                if(Take("korean-roof-check.request"))RoofCheck();
                if(Take("korean-backcourt-check.request"))BackcourtCheck();
                if(Take("foliage-wind-review.request"))WindReview();
                if(Take("korean-shadow-check.request"))ShadowCheck();
                if(Take("korean-lod-compare.request"))LodCompare();
                if(Take("korean-atmosphere-check.request"))AtmosphereCheck();
            }
            if(frame<0){
                if(Take("korean-video.request"))BeginMovie(false);
                else if(Take("korean-walkthrough.request"))BeginMovie(true);
                else if(Take("foliage-wind-video.request"))BeginWindMovie();
                else if(Take("tucson-orbit.request"))BeginTucsonOrbit();
                else if(Take("utility-network-video.request"))BeginUtilityMovie();
            }
            if(frame<0||EditorApplication.timeSinceStartup<nextFrame)return;
            // One frame per editor update; capped scheduling prevents a busy capture loop.
            int count=windMovie?90:utilityMovie?120:FrameCount;
            float t=frame/(float)(count-1);float s=t*t*(3-2*t);
            Vector3 p=walking?Vector3.Lerp(new Vector3(19,1.65f,-10),new Vector3(5,1.65f,-10),s):Vector3.Lerp(new Vector3(24,20,-28),new Vector3(9,26,-20),s);
            Vector3 aim=walking?p+new Vector3(-27,-.45f,5):Vector3.Lerp(new Vector3(0,0,1),new Vector3(-2,0,3),s);
            if(windMovie){p=new Vector3(25,2.2f,-16);aim=new Vector3(29,3.6f,-10);}
            if(utilityMovie){p=Vector3.Lerp(new Vector3(-32,1.65f,-11),new Vector3(-32,1.65f,1),s);aim=p+new Vector3(-4.6f,3.35f,22);}
            if(orbitSubject!=null){float angle=(45+t*360)*Mathf.Deg2Rad;p=orbitSubject.transform.position+new Vector3(Mathf.Sin(angle)*5.6f,1.55f,Mathf.Cos(angle)*5.6f);aim=orbitSubject.transform.position+new Vector3(0,.93f,0);}
            string label=orbitSubject!=null?"tucson-orbit":utilityMovie?"utility-network":windMovie?"foliage-wind":walking?"walkthrough":"video";
            Capture(Output+"/"+label+"-frames/frame_"+frame.ToString("D4")+".png",p,aim,orbitSubject!=null?45:utilityMovie?60:walking?50:52,false,frame/30f);
            frame++;nextFrame=EditorApplication.timeSinceStartup+.05;
            if(frame%30==0)File.WriteAllText(Output+"/"+label+"-progress.txt",frame+"/"+count);
            if(frame>=count){bool native=utilityMovie;Release();File.WriteAllText(Output+"/"+label+"-success.txt",count+" frames, "+(native?"native 1920x1080 with 4x MSAA":"1920x1080 output downsampled from 3840x2160 with 4x MSAA")+", 30 fps playback, "+(count/30)+" seconds. Offline rendered preview; not a realtime FPS benchmark. "+DateTime.Now.ToString("s"));}
        }catch(Exception e){Release();Directory.CreateDirectory(Output);File.WriteAllText(Output+"/capture-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void BeginMovie(bool walk){camera=FindCamera();if(camera.gameObject.scene.isDirty)throw new InvalidOperationException("Save the map before video capture.");walking=walk;Directory.CreateDirectory(Output+(walk?"/walkthrough-frames":"/video-frames"));Allocate(1920,1080,4,true);StartProgress(walk?"walkthrough":"video",FrameCount);frame=0;nextFrame=0;}
    static void BeginWindMovie(){camera=FindCamera();if(camera.gameObject.scene.isDirty)throw new InvalidOperationException("Save the map before wind video review.");windMovie=true;var preview=camera.GetComponent<ParkingLotCamera>();windOriginalStrength=preview.foliageWindPreviewOverride;preview.foliageWindPreviewOverride=.012f;Directory.CreateDirectory(Output+"/foliage-wind-frames");Allocate(1920,1080,4,true);StartProgress("foliage-wind",90);frame=0;nextFrame=0;}
    static void BeginUtilityMovie(){camera=FindCamera();if(camera.gameObject.scene.isDirty)throw new InvalidOperationException("Save the map before utility video review.");utilityMovie=true;Directory.CreateDirectory(Output+"/utility-network-frames");Allocate(1920,1080,4,false);StartProgress("utility-network",120);frame=0;nextFrame=0;}
    static void BeginTucsonOrbit(){
        camera=FindCamera();if(camera.gameObject.scene.isDirty)throw new InvalidOperationException("Save the map before orbit review.");
        GameObject source=null;foreach(var root in camera.gameObject.scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>())if(tr.name.StartsWith("SUV_bay_")&&tr.name.EndsWith("_hyundai-tucson")){source=tr.gameObject;break;}
        if(source==null)throw new InvalidOperationException("No map Tucson found.");
        orbitSubject=UnityEngine.Object.Instantiate(source);orbitSubject.name="Temporary_Tucson_orbit";orbitSubject.hideFlags=HideFlags.HideAndDontSave;
        // The right aisle leaves the full orbit clear of the parked row at z=3.
        orbitSubject.transform.position=new Vector3(18,0,-10);orbitSubject.transform.rotation=Quaternion.Euler(0,180,0);
        foreach(var lod in orbitSubject.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
        Directory.CreateDirectory(Output+"/tucson-orbit-frames");Allocate(1920,1080,4,true);StartProgress("tucson-orbit",FrameCount);frame=0;nextFrame=0;
    }
    static void StartProgress(string label,int count){
        // A previous run's 240/240 or success flag must not describe a newly started capture.
        string success=Output+"/"+label+"-success.txt";if(File.Exists(success))File.Delete(success);
        File.WriteAllText(Output+"/"+label+"-progress.txt","0/"+count+" Started="+DateTime.Now.ToString("s"));
        string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(ScenePath))).Replace("-","");
        File.WriteAllText(Output+"/"+label+"-capture-source.txt","Started="+DateTime.Now.ToString("s")+"\nScene="+ScenePath+"\nSceneSHA256="+hash+"\nTemporaryInspectionClone="+(orbitSubject!=null)+"\nOffline=True\nFrames="+count+"\nResolution=1920x1080\nSupersampled="+!utilityMovie+"\nPlaybackFPS=30");
    }
    static void Allocate(int width,int height,int aa,bool supersample){
        int factor=supersample?2:1;
        render=new RenderTexture(width*factor,height*factor,24,RenderTextureFormat.ARGBHalf){antiAliasing=aa,hideFlags=HideFlags.HideAndDontSave};render.Create();
        if(supersample){downsample=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};downsample.Create();}
        pixels=new Texture2D(width,height,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};
    }
    internal static void CandidateViews(string key,Vector3 center){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/candidate-"+key+"-rear.png",center+new Vector3(6.4f,2.2f,-6.4f),center+new Vector3(0,.85f,0),45,false);
            Capture(Output+"/candidate-"+key+"-front.png",center+new Vector3(-6.4f,2.2f,6.4f),center+new Vector3(0,.85f,0),45,false);
        }finally{Release();}
    }
    static void RoofCheck(){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{Capture(Output+"/korean-roof-detail.png",new Vector3(-28,25,-5),new Vector3(-44,12.8f,12),44,false);File.WriteAllText(Output+"/roof-detail-success.txt",DateTime.Now.ToString("s"));}
        finally{Release();}
    }
    static void BackcourtCheck(){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/korean-backcourt-shop.png",new Vector3(-31,4,-20),new Vector3(-48,.3f,-7),52,false);
            Capture(Output+"/korean-backcourt-rear.png",new Vector3(-55,8,62),new Vector3(-57,.3f,48),52,false);
            Capture(Output+"/korean-neighborhood-wide.png",new Vector3(80,90,-80),new Vector3(0,0,30),55,false);
            File.WriteAllText(Output+"/backcourt-views-success.txt",DateTime.Now.ToString("s"));
        }finally{Release();}
    }
    internal static void PaintViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/paint-"+label+"-eye.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/paint-"+label+"-aerial.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }finally{Release();}
    }
    internal static void LightingViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/light-"+label+"-eye.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/light-"+label+"-reverse.png",new Vector3(-19,1.65f,10),new Vector3(8,1.2f,5),50,false);
            Capture(Output+"/light-"+label+"-aerial.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }finally{Release();}
    }
    internal static void WholeMapViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/whole-look-"+label+"-eye.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/whole-look-"+label+"-overview.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
            Capture(Output+"/whole-look-"+label+"-top.png",new Vector3(0,54,-.01f),Vector3.zero,50,true);
        }finally{Release();}
    }
    internal static void DriverSeatViews(string label=null){
        camera=FindCamera();var scene=camera.gameObject.scene;if(scene.isDirty)throw new InvalidOperationException("Save map before driver-seat preview.");
        GameObject source=null;
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="Electric_crossover_bay_24_hyundai-ioniq5-openx")source=t.gameObject;
        if(source==null)throw new InvalidOperationException("Missing licensed Ioniq 5 interior.");
        float oldNear=camera.nearClipPlane;GameObject preview=null;Allocate(1920,1080,4,true);
        try{
            // Temporary copy only: place the seated view along an empty aisle, leaving parked cars intact.
            preview=UnityEngine.Object.Instantiate(source);preview.name="Temporary_driver_seat_preview";
            foreach(var t in preview.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.HideAndDontSave;
            preview.transform.SetParent(null);SceneManager.MoveGameObjectToScene(preview,scene);
            preview.transform.SetPositionAndRotation(new Vector3(19,-.001f,-10),Quaternion.Euler(0,90,0));
            camera.nearClipPlane=.035f;
            // Source steering centre is (0.37,0.98,-0.42); this is the left driver's eye behind it.
            var eye=preview.transform.TransformPoint(new Vector3(.37f,1.30f,.12f));
            var forward=preview.transform.TransformDirection(new Vector3(0,-.035f,-1));
            string prefix=label==null?"korean-driver-seat":"driver-seat-"+label;
            Capture(Output+"/"+prefix+"-front.png",eye,eye+forward*20,60,false);
            var left=preview.transform.TransformDirection(new Vector3(.28f,-.035f,-1));
            Capture(Output+"/"+prefix+"-left.png",eye,eye+left*20,60,false);
        }finally{if(preview!=null)UnityEngine.Object.DestroyImmediate(preview);camera.nearClipPlane=oldNear;Release();}
        if(scene.isDirty)throw new InvalidOperationException("Driver-seat preview dirtied the map.");
        File.WriteAllText(Output+"/driver-seat-"+(label==null?"":label+"-")+"success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nVehicle=Licensed OpenX Ioniq 5\nEyeHeightMetres=1.299\nVerticalFov=60\nTemporaryInspectionVehicle=True\nTemporaryVehicleRemoved=True\nOriginalCameraRestored=True\nOriginalParkedVehiclesUnchanged=True\nDrivingLogicAdded=False");
    }
    internal static void TucsonMacroViews(string label,Vector3 center){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/tucson-macro-"+label+"-side.png",center+new Vector3(-4.5f,1.4f,0),center+new Vector3(0,.9f,0),35,false);
            Capture(Output+"/tucson-macro-"+label+"-door.png",center+new Vector3(-3.4f,1.3f,.4f),center+new Vector3(-.8f,.95f,.2f),35,false);
            // This imported Tucson faces +Z after the review's 180-degree rotation.
            Capture(Output+"/tucson-macro-"+label+"-rear-quarter.png",center+new Vector3(-3.2f,1.4f,-3.7f),center+new Vector3(0,.8f,-1.4f),38,false);
            Capture(Output+"/tucson-macro-"+label+"-front.png",center+new Vector3(-3.2f,1.4f,3.7f),center+new Vector3(0,.8f,1.4f),38,false);
        }finally{Release();}
    }
    internal static void AwningClose(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");camera=FindCamera();Allocate(1920,1080,4,false);
        try{Capture(Output+"/awning-"+label+"-close.png",new Vector3(4,3,36),new Vector3(4,2.6f,41.7f),58,false);}
        finally{Release();}
    }
    internal static void AwningViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");camera=FindCamera();Allocate(1920,1080,4,false);
        try{
            Capture(Output+"/awning-"+label+"-close.png",new Vector3(4,3,36),new Vector3(4,2.6f,41.7f),58,false);
            Capture(Output+"/awning-"+label+"-street.png",new Vector3(-32,1.65f,-11),new Vector3(-36.6f,2.5f,11),60,false);
            Capture(Output+"/awning-"+label+"-aerial.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }finally{Release();}
    }
    internal static void NeighborhoodPavingViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");camera=FindCamera();Allocate(1920,1080,4,false);
        try{
            Capture(Output+"/paving-"+label+"-street.png",new Vector3(-32,1.65f,-11),new Vector3(-36.6f,2.5f,11),60,false);
            Capture(Output+"/paving-"+label+"-rear.png",new Vector3(4,3,33),new Vector3(0,.2f,46),55,false);
            Capture(Output+"/paving-"+label+"-aerial.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }finally{Release();}
    }
    internal static void UtilityViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/utility-"+label+"-eye.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/utility-"+label+"-aerial.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
            Capture(Output+"/utility-"+label+"-street.png",new Vector3(-32,1.65f,-11),new Vector3(-36.6f,5,11),60,false);
        }finally{Release();}
    }
    internal static void UtilityNativeViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");camera=FindCamera();Allocate(1920,1080,4,false);
        try{
            Capture(Output+"/utility-native-"+label+"-eye.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/utility-native-"+label+"-street.png",new Vector3(-32,1.65f,-11),new Vector3(-36.6f,5,11),60,false);
        }finally{Release();}
    }
    internal static void LeafViews(string label){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/leaf-"+label+"-close.png",new Vector3(24,3.5f,-16),new Vector3(29,4,-10),48,false);
            Capture(Output+"/leaf-"+label+"-macro.png",new Vector3(25,4.5f,-13),new Vector3(29,4.5f,-10),24,false);
            Capture(Output+"/leaf-"+label+"-aerial.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }finally{Release();}
    }
    static void Stills(){
        if(frame>=0)throw new InvalidOperationException("Finish the current movie capture before stills.");
        camera=FindCamera();Directory.CreateDirectory(Output);Allocate(1920,1080,4,true);
        try{
            Capture(Output+"/korean-overview.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
            Capture(Output+"/korean-eye-level.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/korean-detail.png",new Vector3(13,1.45f,-11),new Vector3(9,1,-3),44,false);
            Capture(Output+"/korean-top-down.png",new Vector3(0,54,-.01f),Vector3.zero,50,true);
            Capture(Output+"/korean-entry.png",new Vector3(18,2.1f,-24),new Vector3(23.6f,1.2f,-14),52,false);
            Capture(Output+"/korean-cones.png",new Vector3(19,1.5f,-20),new Vector3(16.1f,.3f,-16),50,false);
            File.WriteAllText(Output+"/stills-success.txt",DateTime.Now.ToString("s"));
        }finally{Release();}
    }
    static void ShadowCheck(){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);var sun=RenderSettings.sun;var previous=sun.shadows;float bias=sun.shadowBias,normalBias=sun.shadowNormalBias;
        try{
            sun.shadows=LightShadows.None;Capture(Output+"/cones-without-sun-shadows.png",new Vector3(19,1.5f,-20),new Vector3(16.1f,.3f,-16),50,false);
            sun.shadows=previous;sun.shadowBias=.05f;sun.shadowNormalBias=.4f;Capture(Output+"/cones-shadow-bias-005-040.png",new Vector3(19,1.5f,-20),new Vector3(16.1f,.3f,-16),50,false);
            var originals=new System.Collections.Generic.Dictionary<Renderer,UnityEngine.Rendering.ShadowCastingMode>();
            foreach(var root in camera.gameObject.scene.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial!=null&&r.sharedMaterial.name.StartsWith("Real_carpaint")){originals[r]=r.shadowCastingMode;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;}
            try{Capture(Output+"/cones-shadow-frontfaces.png",new Vector3(19,1.5f,-20),new Vector3(16.1f,.3f,-16),50,false);}
            finally{foreach(var pair in originals)pair.Key.shadowCastingMode=pair.Value;}
            sun.shadowBias=.5f;sun.shadowNormalBias=.5f;Capture(Output+"/cones-shadow-bias-050-050.png",new Vector3(19,1.5f,-20),new Vector3(16.1f,.3f,-16),50,false);
            sun.shadowBias=1;sun.shadowNormalBias=.7f;Capture(Output+"/cones-shadow-bias-100-070.png",new Vector3(19,1.5f,-20),new Vector3(16.1f,.3f,-16),50,false);
        }
        finally{sun.shadows=previous;sun.shadowBias=bias;sun.shadowNormalBias=normalBias;Release();}
    }
    static void AtmosphereCheck(){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);float original=RenderSettings.fogDensity;
        try{foreach(float density in new[]{.0014f,.0008f,.00045f,0f}){
            RenderSettings.fogDensity=density;string label=Mathf.RoundToInt(density*100000).ToString("D3");
            Capture(Output+"/atmosphere-eye-"+label+".png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);
            Capture(Output+"/atmosphere-aerial-"+label+".png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }
            var hill=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/ParkingLotEnvironment/Materials/KoreanContext/Distant_vegetated_Korean_hills.mat");
            bool oldSpec=hill.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"),oldReflections=hill.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF");
            try{RenderSettings.fogDensity=.00045f;hill.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");hill.EnableKeyword("_GLOSSYREFLECTIONS_OFF");Capture(Output+"/atmosphere-eye-matte.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5),50,false);}
            finally{if(!oldSpec)hill.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");if(!oldReflections)hill.DisableKeyword("_GLOSSYREFLECTIONS_OFF");}
        }finally{RenderSettings.fogDensity=original;Release();}
        File.WriteAllText(Output+"/atmosphere-check-success.txt",DateTime.Now.ToString("s"));
    }
    static void LodCompare(){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();Allocate(1920,1080,4,true);var groups=new System.Collections.Generic.List<LODGroup>();
        foreach(var root in camera.gameObject.scene.GetRootGameObjects())groups.AddRange(root.GetComponentsInChildren<LODGroup>());
        try{
            foreach(var g in groups)g.ForceLOD(0);Capture(Output+"/aerial-lod-high.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
            foreach(var g in groups)g.ForceLOD(1);Capture(Output+"/aerial-lod-low.png",new Vector3(31,25,-34),new Vector3(0,1,2),52,false);
        }finally{foreach(var g in groups)g.ForceLOD(-1);Release();}
    }
    static void WindReview(){
        if(frame>=0)throw new InvalidOperationException("Finish movie capture first.");
        camera=FindCamera();if(camera.gameObject.scene.isDirty)throw new InvalidOperationException("Save the map before comparing wind.");
        var preview=camera.GetComponent<ParkingLotCamera>();float oldStrength=preview.foliageWindPreviewOverride;
        Allocate(1920,1080,4,true);
        try{
            Vector3 p=new Vector3(23,2.2f,-12),aim=new Vector3(29,3.6f,-10);
            preview.foliageWindPreviewOverride=0;Capture(Output+"/foliage-wind-baseline.png",p,aim,55,false);
            preview.foliageWindPreviewOverride=.012f;
            foreach(int t in new[]{0,2,4})Capture(Output+"/foliage-wind-t"+t+".png",p,aim,55,false,t);
            Capture(Output+"/foliage-wind-t0-repeat.png",p,aim,55,false);
        }finally{preview.foliageWindPreviewOverride=oldStrength;Release();}
        File.WriteAllText(Output+"/foliage-wind-review-success.txt",DateTime.Now.ToString("s")+"\nTemporary strength .012; restored="+oldStrength+"; Saved="+!preview.gameObject.scene.isDirty);
    }
    static void Capture(string file,Vector3 position,Vector3 target,float fov,bool top,float windTime=0){
        var tr=camera.transform;Vector3 oldPosition=tr.position;Quaternion oldRotation=tr.rotation;float oldFov=camera.fieldOfView,oldOrthoSize=camera.orthographicSize;bool oldOrtho=camera.orthographic;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        float oldWindTime=Shader.GetGlobalFloat("_ParkingLotWindTime"),oldWindOverride=Shader.GetGlobalFloat("_ParkingLotWindTimeOverride");
        try{
            Shader.SetGlobalFloat("_ParkingLotWindTime",windTime);Shader.SetGlobalFloat("_ParkingLotWindTimeOverride",1);
            tr.position=position;tr.rotation=top?Quaternion.Euler(90,0,0):Quaternion.LookRotation(target-position,Vector3.up);camera.orthographic=top;camera.orthographicSize=25.5f;camera.fieldOfView=fov;camera.targetTexture=render;camera.Render();
            if(downsample!=null){Graphics.Blit(render,downsample);RenderTexture.active=downsample;}else RenderTexture.active=render;
            pixels.ReadPixels(new Rect(0,0,pixels.width,pixels.height),0,0);pixels.Apply(false);File.WriteAllBytes(file,pixels.EncodeToPNG());
        }finally{tr.SetPositionAndRotation(oldPosition,oldRotation);camera.fieldOfView=oldFov;camera.orthographic=oldOrtho;camera.orthographicSize=oldOrthoSize;camera.targetTexture=oldTarget;RenderTexture.active=oldActive;Shader.SetGlobalFloat("_ParkingLotWindTime",oldWindTime);Shader.SetGlobalFloat("_ParkingLotWindTimeOverride",oldWindOverride);}
    }
    static void Release(){frame=-1;walking=false;utilityMovie=false;if(orbitSubject!=null){UnityEngine.Object.DestroyImmediate(orbitSubject);orbitSubject=null;}if(windMovie&&camera!=null){var preview=camera.GetComponent<ParkingLotCamera>();if(preview!=null)preview.foliageWindPreviewOverride=windOriginalStrength;}windMovie=false;if(render!=null){render.Release();UnityEngine.Object.DestroyImmediate(render);render=null;}if(downsample!=null){downsample.Release();UnityEngine.Object.DestroyImmediate(downsample);downsample=null;}if(pixels!=null){UnityEngine.Object.DestroyImmediate(pixels);pixels=null;}camera=null;}
}
}
