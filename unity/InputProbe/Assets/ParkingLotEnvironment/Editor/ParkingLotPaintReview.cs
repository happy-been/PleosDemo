using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Temporary material instances only. Does not save the scene or edit any persisted material.
[InitializeOnLoad]
public static class ParkingLotPaintReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotPaintReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/paint-review.request";if(!File.Exists(request))return;
        try{File.Delete(request);Review();}catch(Exception e){File.WriteAllText(Output+"/paint-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Paint review requires the saved map.");
        var originals=new Dictionary<MeshRenderer,Material[]>();var clones=new Dictionary<Material,Material>();
        try{
            foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()){
                var materials=renderer.sharedMaterials;bool changed=false;
                for(int i=0;i<materials.Length;i++){
                    var source=materials[i];if(source==null||!source.name.StartsWith("Real_carpaint_")||source.shader.name!="ParkingLot/LinearCarPaint")continue;
                    if(!clones.TryGetValue(source,out var copy)){copy=new Material(source){name="Temporary_paint_review_"+source.name,hideFlags=HideFlags.HideAndDontSave};clones.Add(source,copy);}
                    if(!changed){originals.Add(renderer,renderer.sharedMaterials);changed=true;}materials[i]=copy;
                }
                if(changed)renderer.sharedMaterials=materials;
            }
            if(clones.Count!=5)throw new InvalidOperationException("Expected five map paint colors, found "+clones.Count);
            ParkingLotCinematicCapture.PaintViews("baseline-v15");
            foreach(var pair in clones){var m=pair.Value;m.SetFloat("_Metallic",pair.Key.GetFloat("_Metallic")*.55f);m.SetFloat("_Glossiness",.84f);m.SetFloat("_CoatStrength",.38f);m.SetFloat("_CoatRoughness",.18f);}
            ParkingLotCinematicCapture.PaintViews("less-metal");
            foreach(var pair in clones){var m=pair.Value;m.SetFloat("_Metallic",pair.Key.GetFloat("_Metallic")*.4f);m.SetFloat("_Glossiness",.80f);m.SetFloat("_CoatStrength",.24f);m.SetFloat("_CoatRoughness",.22f);}
            ParkingLotCinematicCapture.PaintViews("softer-coat");
            // Reducing metallic alone increases diffuse energy and makes silver look white.
            // Keep its diffuse brightness approximately fixed in linear light for a fair comparison.
            foreach(var pair in clones){
                var m=pair.Value;float originalMetal=pair.Key.GetFloat("_Metallic"),metal=originalMetal*.55f;
                Color linear=pair.Key.color.linear;float compensation=(1-originalMetal)/(1-metal);
                linear.r*=compensation;linear.g*=compensation;linear.b*=compensation;m.color=linear.gamma;
                m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",.84f);m.SetFloat("_CoatStrength",.32f);m.SetFloat("_CoatRoughness",.18f);
            }
            ParkingLotCinematicCapture.PaintViews("balanced-energy");
        }finally{
            foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            foreach(var copy in clones.Values)UnityEngine.Object.DestroyImmediate(copy);
        }
        File.WriteAllText(Output+"/paint-review-success.txt",DateTime.Now.ToString("s")+"\nTemporary paint variants restored. Persisted materials unchanged. SceneSaved="+!scene.isDirty+"\nRenderer slots restored="+originals.Count);
    }
}
}
