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
public static class ParkingLotPlantingFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Destination=Root+"/Materials/PlantingFinish";
    static readonly string[] Names={"Photographed_soil_and_leaves","Photographed_shrub_03","Tree_leaf_island_cards"};
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotPlantingFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/planting-finish.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open saved map before planting changes.");
            if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save planting finish.");
            }else if(mode=="review"||mode==""){Review();return;}
            else if(mode!="validate")throw new InvalidOperationException("Unknown planting operation.");
            File.WriteAllText(Output+"/planting-"+mode+"-success.txt",Validate(scene));
        }catch(Exception e){File.WriteAllText(Output+"/planting-finish-error.txt",e.ToString());Debug.LogException(e);}
    }
    static string Original(string name){return Root+"/Materials/RealismDetails/"+name+".mat";}
    static string CopyPath(string name){return Destination+"/"+name+"_Matte.mat";}
    static bool Selected(Material m){return m!=null&&Names.Any(n=>AssetDatabase.GetAssetPath(m)==Original(n)||AssetDatabase.GetAssetPath(m)==CopyPath(n));}
    static void Matte(Material m){
        if(m.HasProperty("_SpecularHighlights"))m.SetFloat("_SpecularHighlights",0);
        if(m.HasProperty("_GlossyReflections"))m.SetFloat("_GlossyReflections",0);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
    }
    static MeshRenderer[] Renderers(Scene scene){
        if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Planting finish belongs only to ParklingLot.");
        return scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
    }
    public static void Build(Scene scene){
        var all=Renderers(scene);var previous=all.ToDictionary(r=>r,r=>r.sharedMaterials);
        var materials=previous.Values.SelectMany(a=>a).Where(Selected).Distinct().ToArray();
        if(materials.Length!=3)throw new InvalidOperationException("Expected exactly three planting materials.");
        var shader=Shader.Find("ParkingLot/RealismFoliage");
        if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Runtime foliage shader not ready.");
        if(!AssetDatabase.IsValidFolder(Destination))AssetDatabase.CreateFolder(Root+"/Materials","PlantingFinish");
        var replacements=new Dictionary<Material,Material>();
        foreach(var name in Names){
            var original=AssetDatabase.LoadAssetAtPath<Material>(Original(name));
            if(original==null)throw new InvalidOperationException("Missing source planting material: "+name);
            var copy=AssetDatabase.LoadAssetAtPath<Material>(CopyPath(name));
            if(copy==null){copy=new Material(original){name=name+"_Matte"};Matte(copy);AssetDatabase.CreateAsset(copy,CopyPath(name));AssetDatabase.SaveAssetIfDirty(copy);}
            replacements.Add(original,copy);
        }
        try{
            foreach(var pair in previous){var arr=(Material[])pair.Value.Clone();for(int i=0;i<arr.Length;i++)if(arr[i]!=null&&replacements.TryGetValue(arr[i],out var copy))arr[i]=copy;pair.Key.sharedMaterials=arr;}
            File.WriteAllText(Output+"/planting-finish-audit.txt",Validate(scene)+"\nSourceMaterialsModified=False\nGeometryChanges=0\nNewTextures=0");
        }catch{foreach(var pair in previous)pair.Key.sharedMaterials=pair.Value;throw;}
    }
    public static string Validate(Scene scene){
        var all=Renderers(scene);var mats=all.SelectMany(r=>r.sharedMaterials).Where(Selected).Distinct().ToArray();
        if(mats.Length!=3)throw new InvalidOperationException("Expected three saved planting materials.");
        foreach(var name in Names){
            var source=AssetDatabase.LoadAssetAtPath<Material>(Original(name));var copy=AssetDatabase.LoadAssetAtPath<Material>(CopyPath(name));
            if(source==null||copy==null||!mats.Contains(copy)||mats.Contains(source)||copy.shader!=source.shader||ShaderUtil.ShaderHasError(copy.shader)||!copy.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")||!copy.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF"))throw new InvalidOperationException("Invalid planting material: "+name);
            if(copy.color!=source.color)throw new InvalidOperationException("Plant color changed: "+name);
            foreach(var property in source.GetTexturePropertyNames())if(copy.GetTexture(property)!=source.GetTexture(property)||copy.GetTextureScale(property)!=source.GetTextureScale(property)||copy.GetTextureOffset(property)!=source.GetTextureOffset(property))throw new InvalidOperationException("Plant photo mapping changed: "+name);
        }
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nCopiedMaterials="+mats.Length+"\nAffectedRenderers="+all.Count(r=>r.sharedMaterials.Any(Selected))+"\nOriginalPhotoMappings=True\nShaderErrors=0";
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open saved map before planting review.");
        var shader=Shader.Find("ParkingLot/PlantingReview");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Planting shader not ready.");
        var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var previous=renderers.ToDictionary(r=>r,r=>r.sharedMaterials);var copies=new Dictionary<Material,Material>();
        try{
            foreach(var source in previous.Values.SelectMany(a=>a).Where(Selected).Distinct()){
                if(!AssetDatabase.GetAssetPath(source).StartsWith(Root+"/",StringComparison.Ordinal))throw new InvalidOperationException("Refusing an external plant material.");
                var copy=new Material(source){hideFlags=HideFlags.HideAndDontSave};if(source.shader.name=="ParkingLot/RealismFoliage")copy.shader=shader;Matte(copy);copies.Add(source,copy);
            }
            if(copies.Count<2)throw new InvalidOperationException("Missing planting surfaces.");
            foreach(var pair in previous){var mats=(Material[])pair.Value.Clone();for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&copies.TryGetValue(mats[i],out var copy))mats[i]=copy;pair.Key.sharedMaterials=mats;}
            ParkingLotCinematicCapture.WholeMapViews("planting-candidate");
            File.WriteAllText(Output+"/planting-review-materials.txt",string.Join("\n",copies.Keys.Select(m=>AssetDatabase.GetAssetPath(m))));
        }finally{foreach(var pair in previous)pair.Key.sharedMaterials=pair.Value;foreach(var m in copies.Values)UnityEngine.Object.DestroyImmediate(m);}
        if(scene.isDirty)throw new InvalidOperationException("Review dirtied scene; do not save.");
        File.WriteAllText(Output+"/planting-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nTemporaryOnly=True\nOriginalMaterialReferencesRestored=True\nCopiedMaterials="+copies.Count+"\nGeometryChanges=0\nNewTextures=0");
    }
}
}
