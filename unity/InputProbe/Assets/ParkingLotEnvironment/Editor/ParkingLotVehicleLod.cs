using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Adds distance-only geometry on scene instances; source prefabs/materials remain unchanged.
public static class ParkingLotVehicleLod {
    const string Root="Assets/ParkingLotEnvironment/RealVehicles/";
    public static void Add(GameObject car,string key){
        if(car.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Map scene only.");
        if(car.GetComponent<LODGroup>()!=null||car.transform.Find("Distant_model")!=null)throw new InvalidOperationException("Vehicle LOD is already installed.");
        var bounds=car.GetComponent<BoxCollider>();if(bounds==null)throw new InvalidOperationException("Original vehicle bounds are required.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+key+"-lod1/"+key+"-lod1.prefab");
        if(prefab==null)throw new InvalidOperationException("Import distant vehicle first: "+key);
        var high=car.transform.Find("Model_geometry").GetComponentsInChildren<Renderer>();
        var paint=high.Select(r=>r.sharedMaterial).FirstOrDefault(m=>m.name.StartsWith("Real_carpaint"));
        if(paint==null)throw new InvalidOperationException("Vehicle paint must be assigned before LOD.");
        var low=(GameObject)PrefabUtility.InstantiatePrefab(prefab,car.scene);
        PrefabUtility.UnpackPrefabInstance(low,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
        low.name="Distant_model";low.transform.SetParent(car.transform,false);
        foreach(var collider in low.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
        foreach(var renderer in low.GetComponentsInChildren<Renderer>()){
            var original=renderer.sharedMaterial;string n=original.name.ToLowerInvariant();
            bool isPaint=n.Contains("varnish")||n.EndsWith("_paint")||(key=="hyundai-azera"&&Mathf.Abs(original.color.r-Mathf.LinearToGammaSpace(.749f))<.003f&&Mathf.Abs(original.color.r-original.color.g)<.003f);
            if(isPaint)renderer.sharedMaterial=paint;
        }
        if(key=="hyundai-azera")ParkingLotAzeraFinish.Apply(low);
        if(key=="hyundai-i30")ParkingLotI30Finish.Apply(low);
        ParkingLotVehicleBatching.Optimize(low,key+"-lod1");
        var lod=car.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;
        // Current editor quality uses lodBias=2. At 50-degree FOV this changes around 25–29 m.
        // Lower quality can use the reduced mesh sooner; the last level is never culled.
        lod.SetLODs(new[]{new LOD(.36f,high),new LOD(0,low.GetComponentsInChildren<Renderer>())});
        lod.localReferencePoint=bounds.center;lod.size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
    }
}
}
