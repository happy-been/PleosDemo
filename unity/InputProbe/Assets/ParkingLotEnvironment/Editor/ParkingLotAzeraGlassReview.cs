using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotAzeraGlassReview {
    const string Root="Assets/ParkingLotEnvironment";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotAzeraGlassReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/azera-glass-review.request";if(!File.Exists(request))return;
        try{File.Delete(request);Review();}catch(Exception e){File.WriteAllText(Output+"/azera-glass-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/hyundai-azera/hyundai-azera.prefab");
        var car=UnityEngine.Object.Instantiate(prefab);car.name="Temporary_Azera_glass_review";car.hideFlags=HideFlags.HideInHierarchy;car.transform.position=new Vector3(18,0,4);car.transform.rotation=Quaternion.Euler(0,180,0);
        var materials=new List<Material>();
        try{
            var paint=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Real_carpaint_0.mat");
            var windows=new List<MeshRenderer>();var frames=new List<MeshRenderer>();
            foreach(var r in car.GetComponentsInChildren<MeshRenderer>()){
                string name=r.gameObject.name;var m=r.sharedMaterial;
                if(Mathf.Abs(m.color.r-Mathf.LinearToGammaSpace(.749f))<.003f&&Mathf.Abs(m.color.r-m.color.g)<.003f)r.sharedMaterial=paint;
                if(name=="front_doo_1"||name=="right_doo_1"||name=="rear_door_1"||name=="rear_do38_1"||name=="tail_wind"||name=="tail_wi39")windows.Add(r);
                if(name=="tail_wind_1"||name=="tail_wi39_1")frames.Add(r);
            }
            ParkingLotCinematicCapture.CandidateViews("azera-glass-baseline",car.transform.position);
            var red=new Material(Shader.Find("Unlit/Color")){color=Color.red};var blue=new Material(Shader.Find("Unlit/Color")){color=Color.blue};materials.Add(red);materials.Add(blue);
            foreach(var r in windows)r.sharedMaterial=red;foreach(var r in frames)r.sharedMaterial=blue;
            ParkingLotCinematicCapture.CandidateViews("azera-glass-mask",car.transform.position);
            var glass=new Material(Shader.Find("ParkingLot/AutomotiveGlass")){color=new Color(.11f,.16f,.18f,.65f)};materials.Add(glass);
            var trim=new Material(Shader.Find("Standard")){color=new Color(.035f,.04f,.045f,1)};trim.SetFloat("_Glossiness",.45f);materials.Add(trim);
            foreach(var r in windows)r.sharedMaterial=glass;foreach(var r in frames)r.sharedMaterial=trim;
            ParkingLotCinematicCapture.CandidateViews("azera-glass-corrected",car.transform.position);
            // Verified source asymmetry: tail_wi39_1 is glass, while tail_wind_1 is the surround.
            foreach(var r in car.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial==glass||r.sharedMaterial==trim)r.sharedMaterial=paint;
            ParkingLotAzeraFinish.Apply(car);
            ParkingLotCinematicCapture.CandidateViews("azera-glass-finished",car.transform.position);
            File.WriteAllText(Output+"/azera-glass-review-success.txt",DateTime.Now.ToString("s")+"\nCandidate glass meshes="+windows.Count+"; trim meshes="+frames.Count+"; saved="+!scene.isDirty);
        }finally{UnityEngine.Object.DestroyImmediate(car);foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);}
    }
}
}
