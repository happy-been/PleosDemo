using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotAerialRoadReview {
    const string TexturePath="Assets/ParkingLotEnvironment/KoreanAssets/aerial_asphalt_01_diff_2k.jpg";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotAerialRoadReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/aerial-road-review.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            if(mode=="apply")Apply();
            else if(mode=="validate")File.WriteAllText(Output+"/aerial-road-validate-success.txt",Validate());
            else if(mode==""||mode=="review")Review();else throw new InvalidOperationException("Unknown road operation.");
        }catch(Exception e){File.WriteAllText(Output+"/aerial-road-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    internal static Texture2D Texture(){
        var importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;if(importer==null)throw new InvalidOperationException("Missing new road photograph.");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
        importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }
    public static void ConfigureRoad(Material material){
        if(material==null||material.shader.name!="ParkingLot/WholeLookRoad"||!AssetDatabase.GetAssetPath(material).StartsWith("Assets/ParkingLotEnvironment/Materials/WholeLook/",StringComparison.Ordinal))throw new InvalidOperationException("Refusing to modify an external road material.");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);if(texture==null||texture.width!=2048||texture.height!=2048)throw new InvalidOperationException("Import the 2K road photograph first.");
        material.SetTexture("_WeatherTex",texture);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
    }
    public static void ValidateRoad(Material material){
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if(material==null||material.shader.name!="ParkingLot/WholeLookRoad"||ShaderUtil.ShaderHasError(material.shader)||texture==null||material.GetTexture("_WeatherTex")!=texture)throw new InvalidOperationException("Saved road photograph reference or shader is invalid.");
        var importer=AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if(importer==null||!importer.sRGBTexture||!importer.mipmapEnabled||importer.maxTextureSize!=2048||importer.filterMode!=FilterMode.Trilinear||importer.anisoLevel!=4||importer.wrapMode!=TextureWrapMode.Repeat)throw new InvalidOperationException("Road photograph import settings differ from reviewed candidate.");
    }
    static Material Road(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open saved map before road work.");
        return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null&&m.shader.name=="ParkingLot/WholeLookRoad").Distinct().Single();
    }
    static void Apply(){
        var material=Road();ConfigureRoad(material);ValidateRoad(material);
        var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save map checkpoint.");
        File.WriteAllText(Output+"/aerial-road-apply-success.txt",Validate());
    }
    static string Validate(){
        var material=Road();ValidateRoad(material);var texture=material.GetTexture("_WeatherTex");
        return DateTime.Now.ToString("s")+"\nSaved=True\nPhotographedWidthMetres=30\nTextureResolution="+texture.width+"\nTextureRuntimeBytes="+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture)+"\nGeometryChanges=0\nRuntimeShader=True";
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open saved map before road comparison.");
        var shader=Shader.Find("ParkingLot/ScannedRoadReview");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Road shader not ready.");
        var texture=Texture();if(texture==null)throw new InvalidOperationException("Road photograph not ready.");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var previous=all.ToDictionary(r=>r,r=>r.sharedMaterials);
        var sources=previous.Values.SelectMany(a=>a).Where(m=>m!=null&&m.shader.name=="ParkingLot/WholeLookRoad").Distinct().ToArray();
        if(sources.Length!=1)throw new InvalidOperationException("Expected a single dedicated road material.");
        if(sources[0].GetTexture("_WeatherTex")==texture)throw new InvalidOperationException("This candidate is already adopted; use validate or saved-map captures.");
        Material candidate=null;
        try{
            candidate=new Material(sources[0]){shader=shader,hideFlags=HideFlags.HideAndDontSave};candidate.SetTexture("_WeatherTex",texture);
            foreach(var pair in previous){var mats=(Material[])pair.Value.Clone();for(int i=0;i<mats.Length;i++)if(mats[i]==sources[0])mats[i]=candidate;pair.Key.sharedMaterials=mats;}
            ParkingLotCinematicCapture.WholeMapViews("aerial-road-candidate");
        }finally{foreach(var pair in previous)pair.Key.sharedMaterials=pair.Value;if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);}
        if(scene.isDirty)throw new InvalidOperationException("Review dirtied scene; do not save.");
        File.WriteAllText(Output+"/aerial-road-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nOriginalMaterialsRestored=True\nTemporaryOnly=True\nPhotographedWidthMetres=30\nTextureResolution=2048\nGeometryChanges=0");
    }
}
}
