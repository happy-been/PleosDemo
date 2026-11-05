using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Adds a lightweight distant tree to a scene instance; source prefabs, meshes and materials remain unchanged.
public static class ParkingLotTreeLod {
    const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string LowPrefab="Assets/ParkingLotEnvironment/RealVehicles/tree-small-far/tree-small-far.prefab";
    static readonly string[] PartNames={"tree_small_02_branches","tree_small_02_leaves","tree_small_02_trunk"};

    public static void Add(GameObject tree,float transition=.45f){
        if(tree==null)throw new ArgumentNullException(nameof(tree));
        if(tree.scene.path!=ScenePath)throw new InvalidOperationException("Map scene only.");
        if(!(transition>0&&transition<1))throw new ArgumentOutOfRangeException(nameof(transition),"Transition must be between 0 and 1.");
        if(tree.GetComponent<LODGroup>()!=null||tree.transform.Find("Distant_tree")!=null)throw new InvalidOperationException("Tree LOD is already installed.");
        var highModel=tree.transform.Find("Model_geometry");
        if(highModel==null)throw new InvalidOperationException("Tree has no Model_geometry.");
        var high=Parts(highModel);
        var bounds=LocalBounds(tree.transform,high);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LowPrefab);
        if(prefab==null)throw new InvalidOperationException("Distant tree prefab is missing: "+LowPrefab);

        GameObject low=null;LODGroup lod=null;
        try{
            low=(GameObject)PrefabUtility.InstantiatePrefab(prefab,tree.scene);
            PrefabUtility.UnpackPrefabInstance(low,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            low.name="Distant_tree";low.transform.SetParent(tree.transform,false);
            low.transform.localPosition=Vector3.zero;low.transform.localRotation=Quaternion.identity;low.transform.localScale=Vector3.one;
            foreach(var collider in low.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);

            var lowModel=low.transform.Find("Model_geometry");
            if(lowModel==null)throw new InvalidOperationException("Distant tree has no Model_geometry.");
            var lowParts=Parts(lowModel);
            if(low.GetComponentsInChildren<Renderer>(true).Length!=lowParts.Length)throw new InvalidOperationException("Distant tree has unexpected extra renderers.");
            for(int i=0;i<high.Length;i++)CopyRenderState(high[i],lowParts[i]);
            // Both models share source coordinates, but each import recentred its own bounds.
            lowModel.localPosition=highModel.localPosition;lowModel.localRotation=highModel.localRotation;lowModel.localScale=highModel.localScale;

            lod=tree.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.None;
            // The last level has screen height 0, so the tree is never culled.
            lod.SetLODs(new[]{new LOD(transition,high),new LOD(0,lowParts)});
            lod.localReferencePoint=bounds.center;lod.size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        }catch{
            if(lod!=null)UnityEngine.Object.DestroyImmediate(lod);
            if(low!=null)UnityEngine.Object.DestroyImmediate(low);
            throw;
        }
    }

    // Direct-child MeshRenderers of Model_geometry, ordered by PartNames. Missing, extra or duplicate names throw.
    static Renderer[] Parts(Transform model){
        var parts=new Renderer[PartNames.Length];int count=0;
        foreach(Transform child in model){
            var renderer=child.GetComponent<MeshRenderer>();if(renderer==null)continue;
            int index=Array.IndexOf(PartNames,child.name);
            if(index<0||parts[index]!=null)throw new InvalidOperationException("Unexpected tree renderer '"+child.name+"' in "+model.parent.name+".");
            parts[index]=renderer;count++;
        }
        if(count!=PartNames.Length)throw new InvalidOperationException(model.parent.name+" needs exactly "+PartNames.Length+" tree renderers under Model_geometry.");
        return parts;
    }

    // Each mesh's local 8 corners go straight into tree-root space, so no world AABB inflation.
    static Bounds LocalBounds(Transform root,Renderer[] renderers){
        var bounds=new Bounds();bool first=true;
        foreach(var renderer in renderers){
            var filter=renderer.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;
            if(mesh==null||mesh.vertexCount==0)throw new InvalidOperationException("Tree renderer has no mesh: "+renderer.name);
            var toRoot=root.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
            Vector3 c=mesh.bounds.center,e=mesh.bounds.extents;
            for(int i=0;i<8;i++){
                var corner=toRoot.MultiplyPoint3x4(c+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z));
                if(!Finite(corner))throw new InvalidOperationException("Tree bounds are invalid: "+renderer.name);
                if(first){bounds=new Bounds(corner,Vector3.zero);first=false;}else bounds.Encapsulate(corner);
            }
        }
        if(first||Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)<=0)throw new InvalidOperationException("Tree bounds are empty.");
        return bounds;
    }

    static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);

    static void CopyRenderState(Renderer high,Renderer low){
        var materials=high.sharedMaterials;
        if(materials.Length==0||materials.Any(m=>m==null))throw new InvalidOperationException("Tree material is missing: "+high.name);
        if(low.sharedMaterials.Length!=materials.Length)throw new InvalidOperationException("Material slot count differs: "+high.name);
        low.sharedMaterials=materials;
        low.shadowCastingMode=high.shadowCastingMode;low.receiveShadows=high.receiveShadows;
        low.reflectionProbeUsage=high.reflectionProbeUsage;low.lightProbeUsage=high.lightProbeUsage;
    }
}
}
