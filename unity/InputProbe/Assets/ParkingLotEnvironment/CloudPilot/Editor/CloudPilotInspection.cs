using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// All actions are manual menus. No InitializeOnLoad, automatic builders or packages.
public static class CloudPilotInspection {
    const string Kit="Assets/ParkingLotEnvironment/CloudPilot";
    const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Pilot="ParkingLot_CornerPilot_Root", Legacy="ParkingLot_V35_Preserved";
    const string Original="8E05AEF6387B1EA39EC56942A7C0BF0A57A8B4F655A8134B2A3BB17B108B89CE";
    static string Output { get { return Path.GetFullPath(Kit+"/Inspection~"); } }
    [Serializable] public class Entry {public string path,sha256;}
    [Serializable] public class Baseline {public Entry[] files;}
    static string Repo {get{return Path.GetFullPath(Application.dataPath+"/../../..");}}
    static string Latest {get{return Path.Combine(Output,"last-backup-path.txt");}}
    static string Hash(string p){using(var s=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-","");}
    static void RequireScene(bool clean=true) {
        var s=SceneManager.GetActiveScene();
        if(s.path!=ScenePath || (clean&&s.isDirty))throw new InvalidOperationException("Open the saved ParklingLot scene, with no unsaved edits.");
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
    }
    static GameObject Root(string name){foreach(var r in SceneManager.GetActiveScene().GetRootGameObjects())if(r.name==name)return r;return null;}
    static Baseline MakeBaseline() {
        var files=new SortedDictionary<string,Entry>(StringComparer.Ordinal);
        // Stable original list also protects original docs without scanning the checkout.
        foreach(var f in OriginalBaseline().files) {
            CloudPilotFileScope.Resolve(Repo,f.path);
            if(f.path=="unity/InputProbe/"+ScenePath || CloudPilotFileScope.IsNewPilot(f.path))continue;
            files.Add(f.path,new Entry{path=f.path,sha256=f.sha256});
        }
        // Capture additional existing Unity source files only, pruning caches before descent.
        foreach(string rel in CloudPilotFileScope.UnityFiles(Repo)) {
            if(rel=="unity/InputProbe/"+ScenePath || files.ContainsKey(rel))continue;
            files.Add(rel,new Entry{path=rel,sha256=Hash(CloudPilotFileScope.Resolve(Repo,rel))});
        }
        return new Baseline{files=new List<Entry>(files.Values).ToArray()};
    }
    static void Verify(Baseline b) {
        var bad=new List<string>();
        foreach(var f in b.files){string p=CloudPilotFileScope.Resolve(Repo,f.path);if(!File.Exists(p)||Hash(p)!=f.sha256)bad.Add(f.path);}
        if(bad.Count>0)throw new InvalidOperationException("Protected file changes:\n"+string.Join("\n",bad.ToArray()));
    }
    public static void VerifyOriginalSnapshot() {
        Verify(OriginalBaseline());
    }
    static Baseline OriginalBaseline() {
        var b=JsonUtility.FromJson<Baseline>(File.ReadAllText(Kit+"/Tools~/original-files-baseline.json"));
        if(b==null||b.files==null)throw new InvalidDataException("Original file manifest is missing.");
        return b;
    }
    static string BackupPath(){if(!File.Exists(Latest))throw new FileNotFoundException("No pilot backup record.");return File.ReadAllText(Latest).Trim();}

    [MenuItem("ParkingLot/Cloud pilot/Apply prepared pilot to saved v35")]
    public static void Apply() {
        RequireScene();if(Hash(ScenePath)!=Original)throw new InvalidOperationException("Expected exact original v35; review a changed scene instead of replacing it.");
        VerifyOriginalSnapshot();
        if(Root(Pilot)!=null||Root(Legacy)!=null)throw new InvalidOperationException("Pilot or preserved root already exists.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"/PilotCorner.prepared.prefab");
        if(prefab==null)throw new FileNotFoundException("Prepare the isolated prefab first.");
        string backup=Repo+"-cloud-backups/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);Directory.CreateDirectory(Output);
        File.Copy(ScenePath,Path.Combine(backup,"ParklingLot.unity"));File.Copy(ScenePath+".meta",Path.Combine(backup,"ParklingLot.unity.meta"));
        var baseline=MakeBaseline();File.WriteAllText(Path.Combine(backup,"protected-baseline.json"),JsonUtility.ToJson(baseline,true));File.WriteAllText(Latest,backup);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("PARKSIDE isolated pilot");
        try {
            GameObject[] previous=SceneManager.GetActiveScene().GetRootGameObjects();
            var preserved=new GameObject(Legacy);Undo.RegisterCreatedObjectUndo(preserved,"Preserve v35");
            foreach(var r in previous)Undo.SetTransformParent(r.transform,preserved.transform,"Preserve v35 root");
            var pilot=(GameObject)PrefabUtility.InstantiatePrefab(prefab,SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(pilot,"Add pilot");pilot.name=Pilot;preserved.SetActive(false);
            Verify(baseline);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath))throw new IOException("Scene save failed.");
            Verify(baseline);Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Path.Combine(Output,"applied.txt"),"SceneSHA256="+Hash(ScenePath)+"\nBackup="+backup+"\nVehicles, tone and render quality NOT approved.");
        } catch {
            Undo.RevertAllDownToGroup(undo);
            // Save may already have happened: restore only the authorized scene from its verified backup.
            if(Hash(Path.Combine(backup,"ParklingLot.unity"))==Original){File.Copy(Path.Combine(backup,"ParklingLot.unity"),ScenePath,true);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);}
            throw;
        }
    }
    [MenuItem("ParkingLot/Cloud pilot/Show pilot (unsaved toggle)")]
    public static void ShowPilot(){Toggle(true);}
    [MenuItem("ParkingLot/Cloud pilot/Show preserved v35 (unsaved toggle)")]
    public static void ShowV35(){Toggle(false);}
    static void Toggle(bool pilot){RequireScene(false);var a=Root(Pilot);var b=Root(Legacy);if(a==null||b==null)throw new InvalidOperationException("Apply pilot first.");Undo.RecordObjects(new UnityEngine.Object[]{a,b},"Toggle comparison");a.SetActive(pilot);b.SetActive(!pilot);EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());}

    [MenuItem("ParkingLot/Cloud pilot/Add existing vehicles for comparison only")]
    public static void AddComparisonVehicles() {
        RequireScene(false);var pilot=Root(Pilot);if(pilot==null)throw new InvalidOperationException("Apply pilot first.");
        if(pilot.transform.Find("EXISTING_VEHICLES_UNAPPROVED")!=null)throw new InvalidOperationException("Comparison vehicles already present.");
        var layout=JsonUtility.FromJson<CloudPilotPreparation.Layout>(File.ReadAllText(Kit+"/candidate-layout.json"));
        var group=new GameObject("EXISTING_VEHICLES_UNAPPROVED");Undo.RegisterCreatedObjectUndo(group,"Comparison vehicles");group.transform.SetParent(pilot.transform,false);
        string[] keys={"hyundai-tucson","hyundai-i30","hyundai-ioniq5-openx","hyundai-azera","hyundai-i30","hyundai-tucson","hyundai-azera"};
        try {
            for(int i=0;i<layout.vehicleSlots.Length;i++) {
                string key=keys[i%keys.Length],path="Assets/ParkingLotEnvironment/RealVehicles/"+key+"/"+key+".prefab";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new FileNotFoundException(path);
                var car=(GameObject)PrefabUtility.InstantiatePrefab(asset,pilot.scene);car.transform.SetParent(group.transform,false);
                car.transform.localPosition=layout.vehicleSlots[i];car.transform.localRotation=Quaternion.Euler(0,90,0);
                car.name="UNAPPROVED_"+key+"_"+i;
                AddVirtualPlates(car,i);
                // No source materials, normals or shared meshes are changed.
            }
        } catch {UnityEngine.Object.DestroyImmediate(group);throw;}
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
    static void AddVirtualPlates(GameObject car,int index) {
        var bounds=car.GetComponent<BoxCollider>();
        if(bounds==null)throw new InvalidOperationException("Normalized source vehicle collider missing.");
        Font font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ParkingLotEnvironment/Fonts/ParkingSigns.ttf");
        Material backing=AssetDatabase.LoadAssetAtPath<Material>(Kit+"/Materials/PlateBacking.mat");
        if(font==null||backing==null)throw new FileNotFoundException("Pilot plate font/material missing.");
        for(int end=-1;end<=1;end+=2) {
            var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Fictional_plate_"+end;
            plate.transform.SetParent(car.transform,false);
            plate.transform.localPosition=new Vector3(bounds.center.x,.58f,bounds.center.z+end*(bounds.size.z*.5f+.015f));
            plate.transform.localScale=new Vector3(.52f,.12f,.012f);
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());plate.GetComponent<MeshRenderer>().sharedMaterial=backing;
            var text=new GameObject("Fictional_number_text").AddComponent<TextMesh>();
            text.transform.SetParent(car.transform,false);text.transform.localPosition=plate.transform.localPosition+new Vector3(0,0,end*.012f);
            text.transform.localRotation=Quaternion.Euler(0,end>0?180:0,0);
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.font=font;text.fontSize=64;text.characterSize=.018f;text.color=new Color(.04f,.04f,.04f);
            text.text=(123+index)+"가 "+(4567+index*17);
            text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
    }

    [MenuItem("ParkingLot/Cloud pilot/Verify protected files and saved reopen")]
    public static void VerifyReopen() {
        RequireScene();string before=Hash(ScenePath);var b=JsonUtility.FromJson<Baseline>(File.ReadAllText(Path.Combine(BackupPath(),"protected-baseline.json")));
        Verify(b);EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        if(Hash(ScenePath)!=before || Root(Pilot)==null || Root(Legacy)==null)throw new InvalidOperationException("Saved reopen validation failed.");
        Verify(b);File.WriteAllText(Path.Combine(Output,"reopen-verified.txt"),"SceneSHA256="+before+"\nProtectedFiles="+b.files.Length+"\nThis is not visual quality validation.");
    }
    [MenuItem("ParkingLot/Cloud pilot/Restore original v35 from backup")]
    public static void Restore() {
        RequireScene();string backup=BackupPath();string source=Path.Combine(backup,"ParklingLot.unity");
        if(Hash(source)!=Original)throw new InvalidDataException("Original backup hash mismatch.");
        var b=JsonUtility.FromJson<Baseline>(File.ReadAllText(Path.Combine(backup,"protected-baseline.json")));Verify(b);
        File.Copy(source,ScenePath,true);AssetDatabase.Refresh();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Verify(b);
        if(Hash(ScenePath)!=Original)throw new IOException("Restore verification failed.");
    }

    static Camera captureCamera;static RenderTexture target;static Texture2D pixels;
    static int frame=-1;static double started;static string run;static Vector3 start;static Quaternion rotation;
    static ReflectionProbe[] probes;static int[] probeIds;
    [MenuItem("ParkingLot/Cloud pilot/Capture stills plus forward10m side3m (offline)")]
    public static void Capture() {
        RequireScene();if(frame>=0)throw new InvalidOperationException("Capture already running.");
        var pilot=Root(Pilot);if(pilot==null||!pilot.activeInHierarchy)throw new InvalidOperationException("Activate and save pilot first.");
        var source=pilot.GetComponentInChildren<Camera>();if(source==null)throw new InvalidOperationException("Pilot camera missing.");
        var go=new GameObject("Temporary_Pilot_Inspection"){hideFlags=HideFlags.HideAndDontSave};captureCamera=go.AddComponent<Camera>();captureCamera.CopyFrom(source);captureCamera.enabled=false;
        start=source.transform.position;rotation=source.transform.rotation;
        target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=1};target.Create();captureCamera.targetTexture=target;
        pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        run=Path.Combine(Output,"unity-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(run);
        File.WriteAllText(Path.Combine(run,"capture-source.txt"),"UNITY OFFLINE CAPTURE ATTEMPT: require CAPTURE_COMPLETE and inspect actual frames\nSceneSHA256="+Hash(ScenePath)+"\nResolution=1920x1080\nEyeHeight=1.65\nFOV=37,44 comparison\nForward=10m\nLateral=3m\nRealtimePerformance=NOT_MEASURED\nQualityApproval=NOT_GRANTED");
        probes=pilot.GetComponentsInChildren<ReflectionProbe>();probeIds=new int[probes.Length];
        for(int i=0;i<probes.Length;i++)probeIds[i]=probes[i].RenderProbe();
        frame=0;started=EditorApplication.timeSinceStartup;EditorApplication.update+=CaptureTick;
        EditorApplication.quitting+=Release;AssemblyReloadEvents.beforeAssemblyReload+=Release;
    }
    static void CaptureTick() {
        if(frame<0)return;
        try {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Capture stopped before Play mode.");
            for(int i=0;i<probes.Length;i++)if(!probes[i].IsFinishedRendering(probeIds[i])) {
                if(EditorApplication.timeSinceStartup-started>120)throw new TimeoutException("Probe render did not finish.");return;
            }
            Vector3 position=start;float fov=44;string label;
            if(frame<2){fov=frame==0?37:44;label="still-fov"+fov;}
            else if(frame<62){float t=(frame-2)/59f;position+=Vector3.forward*(10*t);label="forward-"+(frame-2).ToString("D3");}
            else{float t=(frame-62)/59f;position+=Vector3.right*(-1.5f+3*t);label="side-"+(frame-62).ToString("D3");}
            captureCamera.transform.SetPositionAndRotation(position,rotation);captureCamera.fieldOfView=fov;
            var previous=RenderTexture.active;
            try{captureCamera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();File.WriteAllBytes(Path.Combine(run,label+".png"),pixels.EncodeToPNG());}
            finally{RenderTexture.active=previous;}
            frame++;if(frame>=122){File.WriteAllText(Path.Combine(run,"CAPTURE_COMPLETE.txt"),"122 offline images. Visual review and target-device performance remain separate.");Release();}
        } catch(Exception e){if(run!=null)File.WriteAllText(Path.Combine(run,"CAPTURE_FAILED.txt"),e.ToString());Release();Debug.LogException(e);}
    }
    [MenuItem("ParkingLot/Cloud pilot/Cancel offline capture")]
    public static void Release() {
        EditorApplication.update-=CaptureTick;EditorApplication.quitting-=Release;AssemblyReloadEvents.beforeAssemblyReload-=Release;frame=-1;
        if(captureCamera!=null)UnityEngine.Object.DestroyImmediate(captureCamera.gameObject);
        if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
        captureCamera=null;target=null;pixels=null;
    }
}
}
