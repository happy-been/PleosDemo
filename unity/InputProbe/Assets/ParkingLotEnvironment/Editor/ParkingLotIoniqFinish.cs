using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Instance-only finish for the licensed OpenX Ioniq. Original downloaded/native assets stay intact.
public static class ParkingLotIoniqFinish {
    const string Folder="Assets/ParkingLotEnvironment/Materials/Ioniq";
    public static void Apply(GameObject car){
        if(car.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Ioniq finish is restricted to the parking map.");
        foreach(var r in car.GetComponentsInChildren<MeshRenderer>()){
            string name=r.sharedMaterial.name;
            if(name.StartsWith("Real_carpaint"))ParkingLotPaintNormals.Refine(r.GetComponent<MeshFilter>(),"Ioniq_body_35",35);
            else if(name.EndsWith("_Trim_Grey"))ParkingLotPaintNormals.Refine(r.GetComponent<MeshFilter>(),"Ioniq_arch_trim_35",35);
            else if(name.EndsWith("_Seat_Fabric"))r.sharedMaterial=MaterialCopy(r.sharedMaterial,"Seat_fabric",new Color(.085f,.09f,.095f,1),0,.18f);
            else if(name.EndsWith("_Grey_Plastic"))r.sharedMaterial=MaterialCopy(r.sharedMaterial,"Interior_grey",new Color(.075f,.08f,.085f,1),0,.28f);
            else if(name.EndsWith("_Chrome_Satin"))r.sharedMaterial=MaterialCopy(r.sharedMaterial,"Satin_trim",new Color(.65f,.66f,.67f,1),.85f,.65f);
        }
    }
    static Material MaterialCopy(Material source,string name,Color color,float metallic,float smoothness){
        Directory.CreateDirectory(Folder);string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(source){name=name};AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smoothness);m.enableInstancing=true;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
}
}
