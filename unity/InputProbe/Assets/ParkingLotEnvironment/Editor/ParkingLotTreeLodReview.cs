using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotTreeLodReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotTreeLodReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/tree-lod-review.request";if(!File.Exists(request))return;
        try{File.Delete(request);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/tree-lod-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var originals=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>()).Where(t=>t.name=="Photographic_3D_tree").Select(t=>t.gameObject).ToArray();
        if(originals.Length!=15)throw new InvalidOperationException("Expected the 15 saved detailed trees.");
        var clones=new List<GameObject>();
        try{
            foreach(var original in originals){
                var clone=UnityEngine.Object.Instantiate(original,original.transform.parent);clones.Add(clone);clone.name="Temporary_tree_lod_review";clone.hideFlags=HideFlags.HideInHierarchy;
                ParkingLotTreeLod.Add(clone);original.SetActive(false);
            }
            foreach(var clone in clones)clone.GetComponent<LODGroup>().ForceLOD(0);
            ParkingLotCinematicCapture.LightingViews("tree-high");
            foreach(var clone in clones)clone.GetComponent<LODGroup>().ForceLOD(1);
            ParkingLotCinematicCapture.LightingViews("tree-low");
            foreach(var clone in clones)clone.GetComponent<LODGroup>().ForceLOD(-1);
            ParkingLotCinematicCapture.LightingViews("tree-auto045");
        }finally{
            foreach(var clone in clones)if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);
            foreach(var original in originals)if(original!=null)original.SetActive(true);
        }
        File.WriteAllText(Output+"/tree-lod-review-success.txt",DateTime.Now.ToString("s")+"\n15 temporary clones; forced high/low and automatic .45 views; originals restored. Saved="+!scene.isDirty);
    }
}
}
