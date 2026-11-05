using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Temporary full-frame look comparison. Never saves scene or modifies a material asset.
[InitializeOnLoad]
public static class ParkingLotWholeLookReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotWholeLookReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        var path=Output+"/whole-look-review.request";if(!File.Exists(path))return;
        try{File.Delete(path);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/whole-look-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static Shader ShaderChecked(string name){var shader=Shader.Find(name);if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Review shader not ready: "+name);return shader;}
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
        var previous=all.ToDictionary(r=>r,r=>r.sharedMaterials);
        var replacements=new Dictionary<Material,Material>();
        var road=ShaderChecked("ParkingLot/WholeLookRoadReview");var paint=ShaderChecked("ParkingLot/WholeLookPaintReview");var contact=ShaderChecked("ParkingLot/WholeLookContactReview");var post=ShaderChecked("Hidden/ParkingLot/WholeLookReview");
        var cameras=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Where(c=>c.name=="ParkingLot_PreviewCamera").ToArray();
        if(cameras.Length!=1)throw new InvalidOperationException("Expected one map camera.");
        var camera=cameras[0];var finish=camera.GetComponent<ParkingLotPhotoFinish>();if(finish==null)throw new InvalidOperationException("Missing map photo finish.");
        var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).ToArray();var sun=RenderSettings.sun;
        if(sun==null||sun.gameObject.scene!=scene||lights.Any(l=>l!=sun&&l.enabled&&l.gameObject.activeInHierarchy))throw new InvalidOperationException("Unexpected active fill light; inspect before altering the lighting balance.");
        var patches=scene.GetRootGameObjects().Single(g=>g.name=="13_Weathered_ground_repairs");bool patchActive=patches.activeSelf;
        var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
        float intensity=sun.intensity,exposure=finish.exposure;Shader originalShader=finish.shader;bool enabled=finish.enabled;
        int roadMaterials=0,paintMaterials=0,contactMaterials=0;
        try{
            ParkingLotCinematicCapture.WholeMapViews("baseline-v29");
            foreach(var m in previous.Values.SelectMany(a=>a).Where(m=>m!=null).Distinct()){
                Shader shader=null;
                if(m.shader.name=="ParkingLot/RoadSurface"){shader=road;roadMaterials++;}
                else if(m.shader.name=="ParkingLot/LinearCarPaint"){shader=paint;paintMaterials++;}
                else if(m.shader.name=="ParkingLot/ContactShadowMultiply"){shader=contact;contactMaterials++;}
                if(shader==null)continue;
                var clone=new Material(m){name=m.name+"_whole_look_review",shader=shader,hideFlags=HideFlags.HideAndDontSave};
                if(shader==road){clone.color=new Color(.68f,.64f,.575f);clone.SetFloat("_BumpScale",.4f);}
                replacements.Add(m,clone);
            }
            if(roadMaterials!=1||paintMaterials<1||contactMaterials<1)throw new InvalidOperationException("Expected road, paint and existing contact-shadow material groups.");
            foreach(var pair in previous){var arr=(Material[])pair.Value.Clone();for(int i=0;i<arr.Length;i++)if(arr[i]!=null&&replacements.TryGetValue(arr[i],out var clone))arr[i]=clone;pair.Key.sharedMaterials=arr;}
            // Measured road shadow is already dark (53/55/57 versus lit 114/115/114).
            // Use a modest key/fill adjustment; the earlier visual estimate understated this contrast.
            // Keep tested shadow bias: lower bias previously caused vehicle striping.
            sun.intensity=1.3f;RenderSettings.ambientSkyColor=new Color(.52f,.56f,.60f);RenderSettings.ambientEquatorColor=new Color(.29f,.30f,.32f);RenderSettings.ambientGroundColor=ground;
            patches.SetActive(false);
            finish.enabled=false;finish.shader=post;finish.exposure=.75f;finish.enabled=enabled;
            ParkingLotCinematicCapture.WholeMapViews("candidate-a");
            File.WriteAllText(Output+"/whole-look-review-audit.txt",DateTime.Now.ToString("s")+"\nOriginalSun="+intensity+"\nSunShadowStrength="+sun.shadowStrength+"\nActiveLights="+lights.Count(l=>l.enabled&&l.gameObject.activeInHierarchy)+"\nHDR="+camera.allowHDR+"\nRoadMaterials="+roadMaterials+"\nPaintMaterials="+paintMaterials+"\nExistingContactMaterials="+contactMaterials+"\nGeometryAdded=0\nOtherSceneAssetsModified=False\nTemporaryReviewOnly=True");
        }finally{
            foreach(var pair in previous)pair.Key.sharedMaterials=pair.Value;
            sun.intensity=intensity;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;patches.SetActive(patchActive);
            finish.enabled=false;finish.shader=originalShader;finish.exposure=exposure;finish.enabled=enabled;
            foreach(var m in replacements.Values)UnityEngine.Object.DestroyImmediate(m);
        }
        if(scene.isDirty)throw new InvalidOperationException("Review unexpectedly dirtied the scene; do not save.");
        File.WriteAllText(Output+"/whole-look-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nOriginalSceneStateRestored=True\nMaterialAssetsModified=False\nCompareAtFullFrame=True");
    }
}
}
