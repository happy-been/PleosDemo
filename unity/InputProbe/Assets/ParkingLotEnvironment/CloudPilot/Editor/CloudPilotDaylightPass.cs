using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Explicit one-pass tool. No automatic execution and no runtime quality component.
public static class CloudPilotDaylightPass {
    const string Kit="Assets/ParkingLotEnvironment/CloudPilot";
    const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Env="Assets/ParkingLotEnvironment";
    static GameObject pilot;
    static string output;
    static int frame;
    static double started;
    static ReflectionProbe[] probes;
    static int[] probeIds;
    static readonly StringBuilder report=new StringBuilder();
    static readonly Dictionary<Material,Material> foliage=new Dictionary<Material,Material>();
    static QualitySnapshot quality;
    static bool qualityActive;
    static Camera Camera {get{return pilot.GetComponentInChildren<Camera>();}}
    static void StopCheck() {
        if(File.Exists("/workspaces/parkside-setup/STOP"))throw new OperationCanceledException("STOP file exists");
    }
    static string Hash(string p) {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","");}
    static GameObject Root(string n){return SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x=>x.name==n);}

    public static void ImproveAndCaptureStills() {
        try {
            StopCheck();
            if(!Application.isBatchMode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Explicit batch OpenGL rendering required");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            pilot=Root("ParkingLot_CornerPilot_Root");
            if(pilot==null || !pilot.activeInHierarchy || Root("ParkingLot_V35_Preserved").activeSelf)throw new InvalidOperationException("Unexpected pilot/preserved state");
            if(pilot.transform.Find("DaylightPass_Context")!=null)throw new InvalidOperationException("Pass already applied. Review before rerunning.");
            output=Path.GetFullPath(Kit+"/Inspection~/daylight-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(output);
            string backup="/workspaces/parkside-source-cloud-backups/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-daylight-scene";
            Directory.CreateDirectory(backup);File.Copy(ScenePath,backup+"/ParklingLot.unity");File.Copy(ScenePath+".meta",backup+"/ParklingLot.unity.meta");
            report.AppendLine("ACTUAL UNITY OFFLINE RENDER; not a photograph or AI concept.\nUnity="+Application.unityVersion+"\nRenderer="+SystemInfo.graphicsDeviceName+"\nAPI="+SystemInfo.graphicsDeviceVersion+"\nColorSpace="+QualitySettings.activeColorSpace+"\nBackup="+backup+"\nBeforeSceneSHA256="+Hash(ScenePath));
            Diagnostics("BEFORE");
            Capture("before-eye-native-quality",Camera.transform.position,Camera.transform.rotation,44);
            Capture("before-aerial-native-quality",new Vector3(23,26,-28),Quaternion.LookRotation(new Vector3(-23,-25,30)),48);
            Apply();
            // Save only CloudPilot assets explicitly. Never SaveAssets on the project.
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath))throw new IOException("Scene save failed");
            CloudPilotInspection.VerifyReopen();pilot=Root("ParkingLot_CornerPilot_Root");
            report.AppendLine("AfterSceneSHA256="+Hash(ScenePath)+"\nSavedAndReopened=true");Diagnostics("AFTER_SCENE_NATIVE_QUALITY");
            quality=new QualitySnapshot();qualityActive=true;quality.ApplyOffline();Diagnostics("OFFLINE_INSPECTION_OVERRIDE");
            File.WriteAllText(output+"/render-metadata.txt",report.ToString());
            probes=pilot.GetComponentsInChildren<ReflectionProbe>();probeIds=new int[probes.Length];
            for(int i=0;i<probes.Length;i++){probes[i].timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;probeIds[i]=probes[i].RenderProbe();}
            frame=0;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
            EditorApplication.quitting+=RestoreQuality;AssemblyReloadEvents.beforeAssemblyReload+=RestoreQuality;
        }catch(Exception e){Finish(e);}
    }
    // Read-only recapture entry point for the saved pilot; exactly three views.
    public static void CaptureSavedPilotStills(){ContinueSaved(false);}
    // One explicitly bounded correction after reviewing the first stills.
    public static void FinishContextAndCaptureStills(){ContinueSaved(true);}
    static void ContinueSaved(bool finishContext) {
        try {
            StopCheck();
            if(!Application.isBatchMode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Explicit batch OpenGL rendering required");
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);pilot=Root("ParkingLot_CornerPilot_Root");
            if(pilot==null || !pilot.activeInHierarchy)throw new InvalidOperationException("Saved active pilot required");
            output=Path.GetFullPath(Kit+"/Inspection~/daylight-final-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(output);
            report.AppendLine("ACTUAL UNITY OFFLINE RENDER; not photograph or AI concept.\nUnity="+Application.unityVersion+"\nRenderer="+SystemInfo.graphicsDeviceName+"\nAPI="+SystemInfo.graphicsDeviceVersion+"\nColorSpace="+QualitySettings.activeColorSpace);
            Diagnostics("SAVED_NATIVE_QUALITY");
            if(finishContext) {
                var context=pilot.transform.Find("DaylightPass_Context");
                if(context==null || context.Find("Daylight_FinalGround")!=null)throw new InvalidOperationException("Unexpected or already finalized context");
                string backup="/workspaces/parkside-source-cloud-backups/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-before-context-finish";
                Directory.CreateDirectory(backup);File.Copy(ScenePath,backup+"/ParklingLot.unity");File.Copy(ScenePath+".meta",backup+"/ParklingLot.unity.meta");report.AppendLine("Backup="+backup+"\nBeforeSceneSHA256="+Hash(ScenePath));
                var brick=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Materials/Brick.mat");
                var contextBrick=Clone(brick,"Daylight_ContextBrick_MetreScale");contextBrick.mainTextureScale=new Vector2(4,5);Save(contextBrick);
                foreach(var r in context.GetComponentsInChildren<Renderer>())if(r.sharedMaterial==brick)r.sharedMaterial=contextBrick;
                var ground=Clone(AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Materials/Daylight_Asphalt.mat"),"Daylight_ContinuousGround");ground.mainTextureScale=new Vector2(12,16);Save(ground);
                Box(context,"Daylight_FinalGround",new Vector3(0,-.22f,25),new Vector3(100,.30f,110),ground,true);
                var paving=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Materials/Paving.mat");
                Box(context,"RightStreetFootway",new Vector3(11.35f,.04f,4),new Vector3(2.15f,.08f,32),paving,true);
                // Reuse real leaf geometry at different rotations to fill the sparse crown.
                // Transforms only: no shared source mesh or original material is edited.
                var leaves=pilot.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("leaves")).ToArray();
                foreach(var leaf in leaves)for(int n=0;n<2;n++) {
                    var fill=UnityEngine.Object.Instantiate(leaf.gameObject,leaf.transform.parent);fill.name="Daylight_CanopyFill_"+n;
                    fill.transform.localRotation=leaf.transform.localRotation*Quaternion.Euler(0,n==0?47:-61,0);
                    fill.transform.localScale=leaf.transform.localScale*(n==0?1.04f:.96f);
                }
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath))throw new IOException("Final scene save failed");
                CloudPilotInspection.VerifyReopen();pilot=Root("ParkingLot_CornerPilot_Root");report.AppendLine("Context texture scale, continuous ground and sparse canopy fill corrected after actual image review.");
            }
            report.AppendLine("AfterSceneSHA256="+Hash(ScenePath)+"\nSavedAndReopened=true");
            // Saved scene, actual project quality. This image must accompany offline shadows.
            Capture("after-eye-native-settings",Camera.transform.position,Camera.transform.rotation,44);
            quality=new QualitySnapshot();qualityActive=true;quality.ApplyOffline();Diagnostics("OFFLINE_INSPECTION_OVERRIDE");
            File.WriteAllText(output+"/render-metadata.txt",report.ToString());
            probes=pilot.GetComponentsInChildren<ReflectionProbe>();probeIds=new int[probes.Length];
            for(int i=0;i<probes.Length;i++){probes[i].timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;probeIds[i]=probes[i].RenderProbe();}
            frame=0;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;
            EditorApplication.quitting+=RestoreQuality;AssemblyReloadEvents.beforeAssemblyReload+=RestoreQuality;
        }catch(Exception e){Finish(e);}
    }
    static void Diagnostics(string label) {
        report.AppendLine("\n["+label+"] qualityIndex="+QualitySettings.GetQualityLevel()+" name="+QualitySettings.names[QualitySettings.GetQualityLevel()]+" shadows="+QualitySettings.shadows+" shadowDistance="+QualitySettings.shadowDistance+" shadowResolution="+QualitySettings.shadowResolution+" cascades="+QualitySettings.shadowCascades+" AA="+QualitySettings.antiAliasing+" pixelLights="+QualitySettings.pixelLightCount);
        report.AppendLine("Ambient="+RenderSettings.ambientMode+" sky="+RenderSettings.ambientSkyColor+" equator="+RenderSettings.ambientEquatorColor+" ground="+RenderSettings.ambientGroundColor+" intensity="+RenderSettings.ambientIntensity+" fog="+RenderSettings.fog+" fogDensity="+RenderSettings.fogDensity+" sun="+(RenderSettings.sun==null?"null":RenderSettings.sun.name));
        foreach(var l in pilot.GetComponentsInChildren<Light>())report.AppendLine("Light="+l.name+" intensity="+l.intensity+" shadows="+l.shadows+" strength="+l.shadowStrength+" bias="+l.shadowBias+" normalBias="+l.shadowNormalBias);
        foreach(string name in new[]{"Zone_Asphalt","Zone_SignPanel","Zone_DrainSteel"}) {
            var t=pilot.transform.Find(name);if(t!=null){var r=t.GetComponentInChildren<Renderer>();report.AppendLine(name+" actual bounds="+r.bounds);}
        }
        var renderers=pilot.GetComponentsInChildren<Renderer>();report.AppendLine("ActiveRenderers="+renderers.Length+" shadowCasters="+renderers.Count(r=>r.shadowCastingMode!=ShadowCastingMode.Off)+" receivers="+renderers.Count(r=>r.receiveShadows));
    }
    static Material Mat(string name,Color color,float smooth=.2f) {
        var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Glossiness",smooth);
        AssetDatabase.CreateAsset(m,Kit+"/Materials/"+name+".mat");return m;
    }
    static Material Clone(Material original,string name) {
        var m=new Material(original){name=name};AssetDatabase.CreateAsset(m,Kit+"/Materials/"+name+".mat");return m;
    }
    static void Save(Material m){EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}
    static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,bool collider=false) {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void SetMat(Transform t,Material m){foreach(var r in t.GetComponentsInChildren<Renderer>())r.sharedMaterial=m;}
    static void Apply() {
        Directory.CreateDirectory(Kit+"/Materials");
        // Unity OBJ importer changes handedness. Check actual bounds instead of assuming the sign side.
        var sign=pilot.transform.Find("Zone_SignPanel").GetComponentInChildren<Renderer>();
        if(sign.bounds.center.x<0) {
            foreach(Transform t in pilot.transform)if(t.name.StartsWith("Zone_"))t.localScale=new Vector3(-1,1,1);
            report.AppendLine("Corrected imported Zone geometry X handedness: sign/drain now on intended right boundary.");
        }
        var signText=pilot.transform.Find("Pilot_SignText");
        signText.localPosition=new Vector3(8.18f,2.78f,9.87f);signText.localRotation=Quaternion.identity;
        var text=signText.GetComponent<TextMesh>();text.anchor=TextAnchor.MiddleLeft;text.characterSize=.035f;
        // Remove only pilot-created fictional labels, including the erroneous side-door text.
        var vehicleGroup=pilot.transform.Find("EXISTING_VEHICLES_UNAPPROVED");
        foreach(var t in vehicleGroup.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Fictional_")).ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
        int carIndex=0;
        foreach(Transform car in vehicleGroup) {
            car.localRotation=Quaternion.Euler(0,-90,0);
            if(carIndex==0 || carIndex==5)foreach(var r in car.GetComponentsInChildren<Renderer>()) {
                var mats=r.sharedMaterials;
                for(int j=0;j<mats.Length;j++)if(mats[j]!=null && mats[j].name.Contains("varnish")) {
                    var m=Clone(mats[j],"Daylight_VehiclePaint_"+carIndex+"_"+j);m.color=carIndex==0?new Color(.78f,.79f,.77f):new Color(.20f,.23f,.25f);m.SetFloat("_Glossiness",.72f);Save(m);mats[j]=m;
                }
                r.sharedMaterials=mats;
            }
            carIndex++;
        }
        var asphalt=Clone(pilot.transform.Find("Zone_Asphalt").GetComponentInChildren<Renderer>().sharedMaterial,"Daylight_Asphalt");
        asphalt.mainTextureScale=new Vector2(4,4);asphalt.color=new Color(.70f,.70f,.69f);asphalt.SetFloat("_Glossiness",.08f);Save(asphalt);SetMat(pilot.transform.Find("Zone_Asphalt"),asphalt);
        var sky=Clone(RenderSettings.skybox,"Daylight_Sky");sky.SetFloat("_Exposure",1.15f);Save(sky);RenderSettings.skybox=sky;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.46f,.51f,.57f);RenderSettings.ambientEquatorColor=new Color(.22f,.24f,.25f);RenderSettings.ambientGroundColor=new Color(.12f,.115f,.10f);
        RenderSettings.fog=true;RenderSettings.fogDensity=.0018f;RenderSettings.fogColor=new Color(.69f,.75f,.79f);
        var sun=pilot.GetComponentInChildren<Light>();sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.92f;sun.shadowBias=.025f;sun.shadowNormalBias=.15f;sun.shadowResolution=LightShadowResolution.VeryHigh;RenderSettings.sun=sun;
        Camera.transform.SetPositionAndRotation(new Vector3(1.2f,1.65f,-13),Quaternion.Euler(2.5f,-6,0));Camera.fieldOfView=44;Camera.farClipPlane=180;
        var context=new GameObject("DaylightPass_Context").transform;context.SetParent(pilot.transform,false);
        var concrete=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Materials/Concrete.mat");
        var brick=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Materials/Brick.mat");
        var glass=Mat("Daylight_ContextGlass",new Color(.16f,.24f,.28f),.65f);
        var trim=Mat("Daylight_ContextTrim",new Color(.66f,.68f,.66f),.3f);
        var ivory=Mat("Daylight_ContextIvory",new Color(.68f,.66f,.60f));
        var dark=Mat("Daylight_ContextDark",new Color(.12f,.15f,.16f));
        var green=Mat("Daylight_AwningGreen",new Color(.10f,.24f,.21f));
        // Volumetric street context: side and rear surfaces, recessed windows, roof parapets.
        Building(context,new Vector3(-6,0,24),new Vector3(8,9.6f,7),brick,glass,trim,green);
        Building(context,new Vector3(4,0,27),new Vector3(9,12.8f,8),ivory,glass,trim,green);
        Building(context,new Vector3(15,0,24),new Vector3(7,8.6f,7),brick,glass,trim,green);
        Building(context,new Vector3(16,0,5),new Vector3(7,9.6f,13),ivory,glass,trim,green);
        Building(context,new Vector3(14,0,48),new Vector3(10,29,9),ivory,glass,trim,green);
        Building(context,new Vector3(-1,0,54),new Vector3(11,25,9),concrete,glass,trim,green);
        Box(context,"RearStreet",new Vector3(0,-.10f,21),new Vector3(49,.15f,15),asphalt,true);
        Box(context,"RightBoundaryWall",new Vector3(10.2f,.48f,1),new Vector3(.25f,.96f,28),concrete,true);
        Box(context,"RearBoundaryCurb",new Vector3(0,.08f,14.3f),new Vector3(20,.16f,.3f),concrete,true);
        // Existing credited real 3D tree meshes; no concept-image plates.
        foreach(Transform t in pilot.transform.Cast<Transform>().ToArray())if(t.name.StartsWith("tree-small")){t.localScale=Vector3.one*1.35f;ImproveTree(t.gameObject);}
        int index=0;
        foreach(var p in new[]{new Vector3(9.8f,.13f,-7),new Vector3(9.9f,.13f,1),new Vector3(10,.13f,9),new Vector3(8,.1f,17),new Vector3(-1,.1f,18),new Vector3(-8,.1f,17),new Vector3(17,.1f,17)}) {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Env+"/RealVehicles/tree-small-real/tree-small-real.prefab");
            var tree=(GameObject)PrefabUtility.InstantiatePrefab(source,pilot.scene);tree.transform.SetParent(context,false);tree.name="Daylight_StreetTree_"+index;tree.transform.localPosition=p;tree.transform.localRotation=Quaternion.Euler(0,index*73,0);tree.transform.localScale=Vector3.one*(1.25f+(index%3)*.14f);ImproveTree(tree);index++;
        }
        // Modest utility detail helps read the setting as a Korean residential street.
        for(int i=0;i<2;i++) {
            float x=i==0?9.8f:-8.8f,z=i==0?12:16;
            Box(context,"UtilityPole",new Vector3(x,4.6f,z),new Vector3(.18f,9.2f,.18f),trim);
            Box(context,"PoleCrossArm",new Vector3(x,8.5f,z),new Vector3(2,.10f,.10f),dark);
        }
        for(int i=0;i<3;i++) {
            var cable=new GameObject("StreetCable_"+i).AddComponent<LineRenderer>();cable.transform.SetParent(context,false);cable.sharedMaterial=dark;cable.startWidth=cable.endWidth=.017f;cable.positionCount=13;cable.useWorldSpace=false;
            for(int n=0;n<13;n++){float u=n/12f;cable.SetPosition(n,new Vector3(Mathf.Lerp(-8.8f,9.8f,u)+(i-1)*.4f,8.7f-Mathf.Sin(u*Mathf.PI)*.8f,Mathf.Lerp(16,12,u)));}
        }
        // Small far truck remains an explicitly provisional existing model.
        var truckAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Env+"/RealVehicles/generico-caminhao/generico-caminhao.prefab");
        var truck=(GameObject)PrefabUtility.InstantiatePrefab(truckAsset,pilot.scene);truck.name="Daylight_ProvisionalTruck";truck.transform.SetParent(context,false);truck.transform.localPosition=new Vector3(-4,0,18.8f);truck.transform.localRotation=Quaternion.Euler(0,90,0);
        foreach(var r in pilot.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.TwoSided;r.receiveShadows=true;}
    }
    static void ImproveTree(GameObject tree) {
        foreach(var r in tree.GetComponentsInChildren<Renderer>()) {
            var materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++) {
                var src=materials[i];if(src==null)continue;
                Material clone;
                if(!foliage.TryGetValue(src,out clone)) {
                    clone=Clone(src,"Daylight_Foliage_"+foliage.Count);
                    if(src.name.Contains("leaves")) {
                        clone.SetTexture("_AlphaTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Env+"/ReferenceAssets/tree_small_02_leaves_alpha_1k.png"));
                        clone.SetFloat("_Cutoff",.32f);
                    }
                    Save(clone);foliage.Add(src,clone);
                }
                materials[i]=clone;
            }
            r.sharedMaterials=materials;
        }
    }
    static void Building(Transform parent,Vector3 pos,Vector3 size,Material wall,Material glass,Material trim,Material awning) {
        var root=new GameObject("ContextBuilding_"+pos.x+"_"+pos.z).transform;root.SetParent(parent,false);root.localPosition=pos;
        Box(root,"SolidStructure",new Vector3(0,size.y*.5f,0),size,wall,true);
        Box(root,"RoofCap",new Vector3(0,size.y+.12f,0),new Vector3(size.x+.25f,.24f,size.z+.25f),trim);
        int floors=Mathf.FloorToInt(size.y/3.1f),cols=Mathf.Max(2,Mathf.FloorToInt(size.x/2.3f));
        for(int f=0;f<floors;f++)for(int c=0;c<cols;c++) {
            float x=-size.x*.5f+(c+.5f)*size.x/cols,y=1.8f+f*3.1f,z=-size.z*.5f;
            Box(root,"WindowReveal",new Vector3(x,y,z-.04f),new Vector3(1.48f,1.65f,.13f),trim);
            Box(root,"RecessedGlazing",new Vector3(x,y,z-.115f),new Vector3(1.30f,1.48f,.035f),glass);
            Box(root,"Mullion",new Vector3(x,y,z-.15f),new Vector3(.045f,1.5f,.045f),trim);
            Box(root,"Sill",new Vector3(x,y-.85f,z-.16f),new Vector3(1.64f,.10f,.30f),trim);
        }
        // Side glazing gives actual parallax from lateral and elevated inspection views.
        for(int f=0;f<floors;f++)for(int c=0;c<2;c++) {
            float z=(c-.5f)*size.z*.5f;
            Box(root,"SideReveal",new Vector3(-size.x*.5f-.04f,1.8f+f*3.1f,z),new Vector3(.13f,1.65f,1.5f),trim);
            Box(root,"SideGlazing",new Vector3(-size.x*.5f-.115f,1.8f+f*3.1f,z),new Vector3(.035f,1.48f,1.3f),glass);
        }
        if(size.y<15) {
            var a=Box(root,"GroundFloorAwning",new Vector3(0,2.9f,-size.z*.5f-.5f),new Vector3(size.x*.86f,.12f,1.15f),awning);a.transform.localRotation=Quaternion.Euler(-9,0,0);
            Box(root,"RooftopStairwell",new Vector3(size.x*.25f,size.y+.9f,0),new Vector3(2.3f,1.8f,2.4f),wall);
        }
    }
    static void Capture(string name,Vector3 position,Quaternion rotation,float fov) {
        StopCheck();var go=new GameObject("Temporary_Daylight_Capture"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera);cam.enabled=false;cam.transform.SetPositionAndRotation(position,rotation);cam.fieldOfView=fov;
        var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=4};var pix=new Texture2D(1920,1080,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try {rt.Create();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;pix.ReadPixels(new Rect(0,0,1920,1080),0,0);pix.Apply();File.WriteAllBytes(output+"/"+name+".png",pix.EncodeToPNG());report.AppendLine("Image="+name+" position="+position+" rotation="+rotation.eulerAngles+" VFOV="+fov+" Resolution=1920x1080 MSAA=4");}
        finally {RenderTexture.active=previous;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pix);UnityEngine.Object.DestroyImmediate(go);}
    }
    static void Tick() {
        try {
            StopCheck();if(EditorApplication.timeSinceStartup-started>240)throw new TimeoutException("Short still capture exceeded 4 minutes");
            for(int i=0;i<probes.Length;i++)if(!probes[i].IsFinishedRendering(probeIds[i]))return;
            if(frame==0)Capture("after-eye-offline-shadows",Camera.transform.position,Camera.transform.rotation,44);
            else if(frame==1)Capture("after-side3m-offline-shadows",Camera.transform.position+Vector3.right*3,Camera.transform.rotation,44);
            else Capture("after-aerial-offline-shadows",new Vector3(23,26,-28),Quaternion.LookRotation(new Vector3(-23,-25,30)),48);
            frame++;if(frame==3)Finish(null);
        }catch(Exception e){Finish(e);}
    }
    static void RestoreQuality(){if(qualityActive){quality.Restore();qualityActive=false;}}
    static void Finish(Exception error) {
        EditorApplication.update-=Tick;EditorApplication.quitting-=RestoreQuality;AssemblyReloadEvents.beforeAssemblyReload-=RestoreQuality;
        try {if(error!=null)Debug.LogException(error);}
        finally {
            RestoreQuality();
            if(output!=null) {
                report.AppendLine("\nOfflineOverridesRestored="+(!qualityActive)+"\nRealtimeFPS=NOT_MEASURED\nProductionMobileValidation=NOT_PERFORMED\nNo global runtime quality behavior added. ProjectSettings not saved.\nOnly 3 after stills; full 122-frame workflow unchanged and NOT completed by this pass.");
                if(pilot!=null)Diagnostics("RESTORED_NATIVE_QUALITY");
                File.WriteAllText(output+"/render-metadata.txt",report.ToString());
                File.WriteAllText(output+(error==null?"/SHORT_CAPTURE_COMPLETE.txt":"/CAPTURE_FAILED.txt"),error==null?"3 after actual offline stills (plus 2 before only in initial run); inspect images. No video or realtime performance claim.":error.ToString());
            }
            Debug.Log("PARKSIDE_DAYLIGHT_"+(error==null?"COMPLETE ":"FAILED ")+output);EditorApplication.Exit(error==null?0:1);
        }
    }
    sealed class QualitySnapshot {
        readonly ShadowQuality shadows=QualitySettings.shadows;
        readonly ShadowResolution resolution=QualitySettings.shadowResolution;
        readonly float distance=QualitySettings.shadowDistance;
        readonly int cascades=QualitySettings.shadowCascades,aa=QualitySettings.antiAliasing,pixelLights=QualitySettings.pixelLightCount;
        public void ApplyOffline(){QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=100;QualitySettings.shadowCascades=4;QualitySettings.antiAliasing=4;QualitySettings.pixelLightCount=Mathf.Max(2,pixelLights);}
        public void Restore(){QualitySettings.shadows=shadows;QualitySettings.shadowResolution=resolution;QualitySettings.shadowDistance=distance;QualitySettings.shadowCascades=cascades;QualitySettings.antiAliasing=aa;QualitySettings.pixelLightCount=pixelLights;}
    }
}
}
