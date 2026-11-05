using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Correct the legacy model's mislabeled opaque window material on scene instances, including LOD.
public static class ParkingLotAzeraFinish {
    const string Folder="Assets/ParkingLotEnvironment/Materials/Azera";
    public static void Apply(GameObject car){
        if(car.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Azera finish is restricted to this map.");
        var glass=Get("Side_glass","ParkingLot/AutomotiveGlass",new Color(.11f,.16f,.18f,.65f),0,.94f);
        var trim=Get("Window_surround","Standard",new Color(.035f,.04f,.045f,1),.05f,.45f);
        var interior=Get("Interior_leather","Standard",new Color(.14f,.135f,.12f,1),0,.25f);
        var hub=Get("Hub_centres","Standard",new Color(.56f,.58f,.6f,1),.85f,.65f);
        foreach(var r in car.GetComponentsInChildren<MeshRenderer>()){
            string n=r.gameObject.name;
            if(n=="front_doo_1"||n=="right_doo_1"||n=="rear_door_1"||n=="rear_do38_1"||n=="tail_wind"||n=="tail_wi39_1")r.sharedMaterial=glass;
            else if(n=="tail_wind_1"||n=="tail_wi39")r.sharedMaterial=trim;
            else if(n=="Cylinde48"||n=="Cylinde49"||n=="Cylinde56"||n=="Cylinder0")r.sharedMaterial=hub;
            else {var c=r.sharedMaterial.color;if(Mathf.Abs(c.r-Mathf.LinearToGammaSpace(.7922f))<.003f&&Mathf.Abs(c.g-Mathf.LinearToGammaSpace(.7608f))<.003f)r.sharedMaterial=interior;}
        }
    }
    static Material Get(string name,string shader,Color color,float metallic,float smoothness){
        Directory.CreateDirectory(Folder);string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smoothness);m.enableInstancing=true;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
}
}
