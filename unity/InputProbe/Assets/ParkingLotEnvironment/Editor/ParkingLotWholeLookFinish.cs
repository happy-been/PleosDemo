using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotWholeLookFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static readonly Dictionary<string,string> Shaders=new Dictionary<string,string>{
        {"ParkingLot/RoadSurface","ParkingLot/WholeLookRoad"},
        {"ParkingLot/LinearCarPaint","ParkingLot/WholeLookPaint"},
        {"ParkingLot/ContactShadowMultiply","ParkingLot/WholeLookContact"}
    };
    static ParkingLotWholeLookFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/whole-look-finish.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved parking map before applying the look.");
            if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save map look.");
            }else if(mode!="validate")throw new InvalidOperationException("Unknown whole-look operation.");
            File.WriteAllText(Output+"/whole-look-"+mode+"-success.txt",Validate(scene));
        }catch(Exception e){File.WriteAllText(Output+"/whole-look-finish-error.txt",e.ToString());Debug.LogException(e);}
    }
    static Shader Checked(string name){var s=Shader.Find(name);if(s==null||!s.isSupported||ShaderUtil.ShaderHasError(s))throw new InvalidOperationException("Shader not ready: "+name);return s;}
    static MeshRenderer[] Renderers(Scene scene){
        if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Whole look belongs only to ParklingLot.");
        return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
    }
    static ParkingLotPhotoFinish Finish(Scene scene){return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(c=>c.name=="ParkingLot_PreviewCamera").GetComponent<ParkingLotPhotoFinish>();}
    static Mesh MeshOf(MeshRenderer renderer){var filter=renderer.GetComponent<MeshFilter>();return filter==null?null:filter.sharedMesh;}
    public static void Build(Scene scene){
        var renderers=Renderers(scene);var originals=renderers.ToDictionary(r=>r,r=>r.sharedMaterials);
        var meshes=renderers.ToDictionary(r=>r,r=>MeshOf(r));
        var transforms=renderers.ToDictionary(r=>r,r=>r.transform.localToWorldMatrix);
        var replacements=new Dictionary<Material,Material>();
        var sun=RenderSettings.sun;var finish=Finish(scene);var post=Checked("Hidden/ParkingLot/WholeLook");
        if(sun==null||sun.gameObject.scene!=scene||finish==null)throw new InvalidOperationException("Missing map light/camera.");
        foreach(var source in originals.Values.SelectMany(a=>a).Where(m=>m!=null).Distinct()){
            if(!Shaders.TryGetValue(source.shader.name,out var shaderName))continue;
            string sourcePath=AssetDatabase.GetAssetPath(source);
            if(!sourcePath.StartsWith(Root+"/",StringComparison.Ordinal))throw new InvalidOperationException("Refusing a shared external material.");
            string path=Root+"/Materials/WholeLook/"+source.name+"_"+AssetDatabase.AssetPathToGUID(sourcePath).Substring(0,8)+".mat";
            var clone=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(clone==null){
                clone=new Material(source){name=source.name+"_WholeLook",shader=Checked(shaderName)};
                if(shaderName=="ParkingLot/WholeLookRoad"){clone.color=new Color(.68f,.64f,.575f);clone.SetFloat("_BumpScale",.4f);}
                AssetDatabase.CreateAsset(clone,path);AssetDatabase.SaveAssetIfDirty(clone);
            }else if(clone.shader.name!=shaderName)throw new InvalidOperationException("Saved look material was manually edited.");
            if(shaderName=="ParkingLot/WholeLookRoad")ParkingLotAerialRoadReview.ConfigureRoad(clone);
            replacements.Add(source,clone);
        }
        foreach(var pair in originals){var arr=(Material[])pair.Value.Clone();for(int i=0;i<arr.Length;i++)if(arr[i]!=null&&replacements.TryGetValue(arr[i],out var clone))arr[i]=clone;pair.Key.sharedMaterials=arr;}
        sun.intensity=1.3f;RenderSettings.ambientSkyColor=new Color(.52f,.56f,.60f);RenderSettings.ambientEquatorColor=new Color(.29f,.30f,.32f);
        // Ground fill, sun direction, shadow bias and sky image retain their reviewed values.
        scene.GetRootGameObjects().Single(g=>g.name=="13_Weathered_ground_repairs").SetActive(false);
        bool wasEnabled=finish.enabled;finish.enabled=false;finish.shader=post;finish.exposure=.75f;finish.enabled=wasEnabled;
        foreach(var r in renderers)if(MeshOf(r)!=meshes[r]||r.transform.localToWorldMatrix!=transforms[r])throw new InvalidOperationException("Geometry changed during material-only look pass.");
        File.WriteAllText(Output+"/whole-look-finish-audit.txt",Validate(scene)+"\nNewMaterialCopies="+replacements.Count+"\nSourceMaterialsModified=False\nGeometryTransformsUnchanged=True\nAddedRenderers=0\nAddedTriangles=0");
    }
    public static string Validate(Scene scene){
        var all=Renderers(scene);var mats=all.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().ToArray();
        if(mats.Any(m=>Shaders.ContainsKey(m.shader.name)))throw new InvalidOperationException("Some map surfaces retain the earlier shaders.");
        int roads=0,paint=0,contact=0;
        foreach(var m in mats){
            if(!Shaders.Values.Contains(m.shader.name))continue;
            if(!AssetDatabase.GetAssetPath(m).StartsWith(Root+"/Materials/WholeLook/",StringComparison.Ordinal)||ShaderUtil.ShaderHasError(m.shader))throw new InvalidOperationException("Invalid saved look material.");
            if(m.shader.name=="ParkingLot/WholeLookRoad"){roads++;ParkingLotAerialRoadReview.ValidateRoad(m);if(Vector4.Distance(m.color,new Color(.68f,.64f,.575f))>.0001f||Mathf.Abs(m.GetFloat("_BumpScale")-.4f)>.0001f)throw new InvalidOperationException("Road look changed.");}
            else if(m.shader.name=="ParkingLot/WholeLookPaint")paint++;else contact++;
        }
        var finish=Finish(scene);var sun=RenderSettings.sun;
        if(roads!=1||paint!=5||contact!=5||finish==null||!finish.enabled||finish.shader!=Checked("Hidden/ParkingLot/WholeLook")||Mathf.Abs(finish.exposure-.75f)>.0001f||sun==null||Mathf.Abs(sun.intensity-1.3f)>.0001f)throw new InvalidOperationException("Saved whole-look settings differ from reviewed candidate.");
        if(Vector4.Distance(RenderSettings.ambientSkyColor,new Color(.52f,.56f,.60f))>.0001f||Vector4.Distance(RenderSettings.ambientEquatorColor,new Color(.29f,.30f,.32f))>.0001f||scene.GetRootGameObjects().Single(g=>g.name=="13_Weathered_ground_repairs").activeSelf)throw new InvalidOperationException("Saved ambient/patch state differs.");
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nRoadMaterials="+roads+"\nPaintMaterials="+paint+"\nContactMaterials="+contact+"\nSunIntensity="+sun.intensity+"\nExposure="+finish.exposure+"\nRepairPatchesVisible=False";
    }
}
}
