using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotVehicleReflectionReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotVehicleReflectionReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/vehicle-reflection-review.request";if(!File.Exists(path))return;
        try{File.Delete(path);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/vehicle-reflection-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open saved map before reflection comparison.");
        var shader=Shader.Find("ParkingLot/SoftReflectionReview");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Review shader not ready.");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var previous=all.ToDictionary(r=>r,r=>r.sharedMaterials);
        var replacements=new Dictionary<Material,Material>();
        try{
            foreach(var m in previous.Values.SelectMany(a=>a).Where(m=>m!=null&&m.shader.name=="ParkingLot/WholeLookPaint").Distinct())replacements.Add(m,new Material(m){shader=shader,hideFlags=HideFlags.HideAndDontSave});
            if(replacements.Count!=5)throw new InvalidOperationException("Expected five paint materials.");
            foreach(var p in previous){var a=(Material[])p.Value.Clone();for(int i=0;i<a.Length;i++)if(a[i]!=null&&replacements.TryGetValue(a[i],out var replacement))a[i]=replacement;p.Key.sharedMaterials=a;}
            ParkingLotCinematicCapture.WholeMapViews("soft-reflection-candidate");
        }finally{foreach(var p in previous)p.Key.sharedMaterials=p.Value;foreach(var m in replacements.Values)UnityEngine.Object.DestroyImmediate(m);}
        if(scene.isDirty)throw new InvalidOperationException("Review dirtied scene; do not save.");
        File.WriteAllText(Output+"/vehicle-reflection-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nOriginalMaterialsRestored=True\nTemporaryOnly=True\nReflectionRoughnessFloor=.30\nGeometryChanges=0");
    }
}
}
