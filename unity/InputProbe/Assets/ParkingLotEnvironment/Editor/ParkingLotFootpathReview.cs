using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotFootpathReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotFootpathReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/footpath-review.request";if(!File.Exists(path))return;
        try{File.Delete(path);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/footpath-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        const string root="Assets/ParkingLotEnvironment";
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved map first.");
        var shader=Shader.Find("ParkingLot/ArchitecturalSurface");
        var pavement=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/KoreanAssets/concrete_pavement_diff_2k.jpg");
        var concrete=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/KoreanAssets/concrete_wall_009_diff_2k.jpg");
        if(shader==null||pavement==null||concrete==null)throw new InvalidOperationException("Missing existing surface sources.");
        var originals=new Dictionary<Renderer,Material[]>();var paths=new List<Renderer>();var curbs=new List<Renderer>();
        foreach(var obj in scene.GetRootGameObjects())if(obj.name=="01_Ground_and_drainage")foreach(var r in obj.GetComponentsInChildren<MeshRenderer>()){
            if(r.name=="Perimeter_footpath")paths.Add(r);
            else if(r.name=="Individual_curb"||r.name=="End_curb")curbs.Add(r);
        }
        if(paths.Count!=2||curbs.Count<150)throw new InvalidOperationException("Unexpected parking footpath/curb layout.");
        var pathMat=new Material(shader){hideFlags=HideFlags.HideAndDontSave,mainTexture=pavement,color=new Color(.76f,.77f,.75f)};
        var curbMat=new Material(shader){hideFlags=HideFlags.HideAndDontSave,mainTexture=concrete,color=new Color(.50f,.50f,.48f)};
        pathMat.SetFloat("_WorldScale",1/1.8f);pathMat.SetFloat("_TextureAsColor",1);
        curbMat.SetFloat("_WorldScale",1/18.05f);curbMat.SetFloat("_TextureAsColor",0);curbMat.SetFloat("_DetailStrength",.7f);
        foreach(var mat in new[]{pathMat,curbMat}){mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");mat.enableInstancing=true;}
        try{
            ParkingLotCinematicCapture.LeafViews("paving-before");
            foreach(var r in paths){originals[r]=r.sharedMaterials;r.sharedMaterial=pathMat;}
            foreach(var r in curbs){originals[r]=r.sharedMaterials;r.sharedMaterial=curbMat;}
            ParkingLotCinematicCapture.LeafViews("paving-candidate");
        }finally{foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;UnityEngine.Object.DestroyImmediate(pathMat);UnityEngine.Object.DestroyImmediate(curbMat);}
        File.WriteAllText(Output+"/footpath-review-success.txt",DateTime.Now.ToString("s")+"\nPaths="+paths.Count+"\nCurbs="+curbs.Count+"\nOriginal materials restored; Saved="+!scene.isDirty);
    }
}
}
