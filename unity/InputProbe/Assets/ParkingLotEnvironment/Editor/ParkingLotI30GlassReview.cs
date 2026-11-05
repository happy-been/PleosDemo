using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotI30GlassReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotI30GlassReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/i30-glass-review.request";if(!File.Exists(request))return;
        try{File.Delete(request);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/i30-glass-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ParkingLotEnvironment/RealVehicles/hyundai-i30/hyundai-i30.prefab");
        var car=UnityEngine.Object.Instantiate(prefab);car.name="Temporary_i30_glass_review";car.hideFlags=HideFlags.HideInHierarchy;car.transform.position=new Vector3(18,0,4);car.transform.rotation=Quaternion.Euler(0,180,0);
        var materials=new List<Material>();var report=new StringBuilder();
        try{
            var paint=AssetDatabase.LoadAssetAtPath<Material>("Assets/ParkingLotEnvironment/Materials/Real_carpaint_0.mat");
            foreach(var r in car.GetComponentsInChildren<MeshRenderer>())if(r.gameObject.name=="paint")r.sharedMaterial=paint;
            ParkingLotCinematicCapture.CandidateViews("i30-glass-baseline",car.transform.position);
            string[] names={"window-0-1","window-0-3-2","glass","window-1","window-0-3-1","window-0-2"};
            Color[] colors={Color.red,Color.green,Color.magenta,Color.blue,Color.yellow,Color.cyan};
            for(int i=0;i<names.Length;i++){
                var m=new Material(Shader.Find("Unlit/Color")){color=colors[i]};materials.Add(m);
                foreach(var r in car.GetComponentsInChildren<MeshRenderer>())if(r.gameObject.name==names[i]){r.sharedMaterial=m;report.AppendLine(names[i]+" = "+colors[i]+"; bounds="+r.bounds);}
            }
            ParkingLotCinematicCapture.CandidateViews("i30-glass-mask",car.transform.position);
            var glass=new Material(Shader.Find("ParkingLot/AutomotiveGlass")){color=new Color(.09f,.13f,.15f,.72f)};materials.Add(glass);
            var trim=new Material(Shader.Find("Standard")){color=new Color(.025f,.03f,.034f,1)};trim.SetFloat("_Glossiness",.5f);materials.Add(trim);
            foreach(var r in car.GetComponentsInChildren<MeshRenderer>()){
                string n=r.gameObject.name;
                if(n=="window-0-1"||n=="window-0-2")r.sharedMaterial=glass;
                else if(n=="window-0-3-1"||n=="window-0-3-2")r.enabled=false;
                else if(n=="window-1")r.sharedMaterial=trim;
                else if(n=="glass")r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ParkingLotEnvironment/RealVehicles/hyundai-i30/Materials/035_glass.mat");
            }
            ParkingLotCinematicCapture.CandidateViews("i30-glass-transmission",car.transform.position);
            glass.color=new Color(.035f,.05f,.06f,.94f);
            ParkingLotCinematicCapture.CandidateViews("i30-glass-dark-tint",car.transform.position);
            var opaque=new Material(Shader.Find("Standard")){color=new Color(.018f,.026f,.032f,1)};opaque.SetFloat("_Glossiness",.94f);opaque.SetFloat("_Metallic",0);materials.Add(opaque);
            foreach(var r in car.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial==glass)r.sharedMaterial=opaque;
            ParkingLotCinematicCapture.CandidateViews("i30-glass-opaque-tint",car.transform.position);
            foreach(var r in car.GetComponentsInChildren<MeshRenderer>())r.enabled=true;
            ParkingLotI30Finish.Apply(car);
            ParkingLotCinematicCapture.CandidateViews("i30-glass-finished",car.transform.position);
        }finally{UnityEngine.Object.DestroyImmediate(car);foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);}
        report.AppendLine("Saved="+!scene.isDirty);File.WriteAllText(Output+"/i30-glass-review-success.txt",DateTime.Now.ToString("s")+"\n"+report);
    }
}
}
