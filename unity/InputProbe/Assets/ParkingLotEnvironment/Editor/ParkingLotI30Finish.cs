using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// This older exterior model has no usable cabin detail behind its opaque windows.
// Dark reflective tint avoids exposing the interior shell; both source face directions remain.
public static class ParkingLotI30Finish {
    const string Folder="Assets/ParkingLotEnvironment/Materials/I30";
    public static void Apply(GameObject car){
        if(car.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("i30 finish is restricted to this map.");
        var glass=Get("I30_tinted_glass",new Color(.018f,.026f,.032f,1),.94f);
        var trim=Get("I30_window_frit",new Color(.025f,.03f,.034f,1),.5f);
        int panes=0,borders=0;
        foreach(var r in car.GetComponentsInChildren<MeshRenderer>()){
            string n=r.gameObject.name;
            if(n=="window-0-1"||n=="window-0-2"||n=="window-0-3-1"||n=="window-0-3-2"){r.sharedMaterial=glass;panes++;}
            else if(n=="window-1"){r.sharedMaterial=trim;borders++;}
        }
        if(panes!=4||borders!=1)throw new InvalidOperationException("Unexpected i30 window layout: "+panes+" pane groups, "+borders+" border groups.");
    }
    static Material Get(string name,Color color,float gloss){
        Directory.CreateDirectory(Folder);string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",0);m.SetFloat("_Glossiness",gloss);m.enableInstancing=true;
        EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
}
}
