using System.Collections.Generic;
using UnityEngine;

namespace ParkingLotEnvironment {
// Owns only the generated map's leaf renderers. No source mesh or prefab is edited.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class ParkingLotFoliageBounds : MonoBehaviour {
    // LeafWind's maximum displacement at strength .02 is below .05m on every axis.
    const float WorldMargin=.06f;
    sealed class Entry {
        public MeshRenderer renderer;
        public Bounds original;
        public Matrix4x4 matrix;
    }
    readonly List<Entry> entries=new List<Entry>();
    bool initialized;
    public int RendererCount { get { return entries.Count; } }
    void OnEnable(){Refresh();}
    void OnDisable(){Release();}
    void OnPreCull(){
        // OnEnable can run while the scene is still loading. Retry before its first render.
        if(!initialized)Refresh();
        foreach(var e in entries)if(e.renderer!=null&&e.matrix!=e.renderer.localToWorldMatrix)Apply(e);
    }
    [ContextMenu("Refresh map foliage bounds")]
    public void Refresh(){
        Release();
        var scene=gameObject.scene;if(!scene.IsValid()||!scene.isLoaded)return;
        foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
            bool foliage=false;
            foreach(var material in renderer.sharedMaterials)if(material!=null&&material.shader!=null&&material.shader.name=="ParkingLot/RealismFoliage"){foliage=true;break;}
            if(!foliage)continue;
            var e=new Entry{renderer=renderer,original=renderer.localBounds};entries.Add(e);Apply(e);
        }
        initialized=true;
    }
    static void Apply(Entry e){
        var m=e.renderer.worldToLocalMatrix;
        // Transform a world-axis box conservatively, including rotated/nonuniform/mirrored scales.
        var padding=new Vector3(Mathf.Abs(m.m00)+Mathf.Abs(m.m01)+Mathf.Abs(m.m02),Mathf.Abs(m.m10)+Mathf.Abs(m.m11)+Mathf.Abs(m.m12),Mathf.Abs(m.m20)+Mathf.Abs(m.m21)+Mathf.Abs(m.m22))*WorldMargin;
        var bounds=e.original;bounds.Expand(padding*2);e.renderer.localBounds=bounds;e.matrix=e.renderer.localToWorldMatrix;
    }
    void Release(){foreach(var e in entries)if(e.renderer!=null)e.renderer.ResetLocalBounds();entries.Clear();initialized=false;}
}
}
