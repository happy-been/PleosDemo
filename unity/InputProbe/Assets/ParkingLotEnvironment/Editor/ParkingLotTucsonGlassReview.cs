using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Temporary combined glazing test (source groups lenses and windows by material).
// Never writes an asset or the saved scene; adoption must inspect lamps as well.
[InitializeOnLoad]
public static class ParkingLotTucsonGlassReview {
    const string Root="Assets/ParkingLotEnvironment";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotTucsonGlassReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/tucson-glass-review.request";if(!File.Exists(request))return;
        string mode;try{mode=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
        try{Review(mode=="low");}catch(Exception e){File.WriteAllText(Output+"/tucson-glass-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(bool low){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved map first.");
        string key=low?"hyundai-tucson-lod1":"hyundai-tucson",label=low?"low-glass-":"glass-";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/"+key+"/"+key+".prefab");
        var paint=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Real_carpaint_1.mat");
        var body=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/SurfaceNormals/Tucson_paint_weighted_normals.asset");
        if(prefab==null||paint==null||body==null)throw new InvalidOperationException("Missing Tucson assets.");
        GameObject car=null;var materials=new List<Material>();var meshes=new List<Mesh>();
        try{
            car=UnityEngine.Object.Instantiate(prefab);car.name="Temporary_Tucson_glass_review";car.hideFlags=HideFlags.HideAndDontSave;
            car.transform.position=new Vector3(18,0,4);car.transform.rotation=Quaternion.Euler(0,180,0);
            var bodyRenderer=car.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterial.name.EndsWith("_varnish"));
            bodyRenderer.sharedMaterial=paint;if(!low)bodyRenderer.GetComponent<MeshFilter>().sharedMesh=body;
            var pane=car.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="glass");
            var original=pane.sharedMaterial;
            ParkingLotCinematicCapture.TucsonMacroViews(label+"current",car.transform.position);
            foreach(var mode in low?new string[0]:new[]{"Back","Depth"}){
                var shader=Shader.Find("Hidden/ParkingLot/TucsonGlass"+mode+"Review");if(shader==null)throw new InvalidOperationException("Missing review shader.");
                var material=new Material(original){name="Temporary_glass_"+mode,hideFlags=HideFlags.HideAndDontSave};materials.Add(material);material.shader=shader;
                pane.sharedMaterial=material;ParkingLotCinematicCapture.TucsonMacroViews(label+mode.ToLowerInvariant(),car.transform.position);
            }
            pane.sharedMaterial=original;
            meshes.AddRange(Split(pane.GetComponent<MeshFilter>().sharedMesh));
            float unit=Mathf.Abs(pane.transform.lossyScale.x);
            var windows=meshes.Where(m=>m.bounds.center.y*unit>1.25f&&m.bounds.size.y*unit>.15f&&Mathf.Max(m.bounds.size.x,m.bounds.size.z)*unit>.35f).ToArray();
            File.WriteAllLines(Output+"/tucson-"+label+"parts.txt",meshes.Select(m=>m.name+" vertices="+m.vertexCount+" triangles="+m.triangles.Length/3+" window="+windows.Contains(m)+" bounds="+m.bounds));
            if(windows.Length!=8)throw new InvalidOperationException("Expected exactly eight cabin panes, got "+windows.Length);
            var lenses=meshes.Except(windows).ToArray();
            var merged=new Mesh{name="combined_lenses",hideFlags=HideFlags.HideAndDontSave,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            merged.CombineMeshes(lenses.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),true,false);merged.RecalculateBounds();merged.UploadMeshData(false);meshes.Add(merged);
            foreach(var m in lenses){meshes.Remove(m);UnityEngine.Object.DestroyImmediate(m);}
            foreach(var mesh in meshes){
                var part=new GameObject("Temporary_glass_part_"+mesh.name){hideFlags=HideFlags.HideAndDontSave};part.transform.SetParent(pane.transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=mesh;var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=original;
                r.shadowCastingMode=pane.shadowCastingMode;r.receiveShadows=pane.receiveShadows;r.reflectionProbeUsage=pane.reflectionProbeUsage;r.lightProbeUsage=pane.lightProbeUsage;
            }
            pane.enabled=false;
            ParkingLotCinematicCapture.TucsonMacroViews(label+"grouped",car.transform.position);
        }finally{if(car!=null)UnityEngine.Object.DestroyImmediate(car);foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);}
        File.WriteAllText(Output+"/tucson-"+label+"review-success.txt",DateTime.Now.ToString("s")+"\nTemporary combined glass comparison; objects removed; Saved="+!scene.isDirty);
    }
    // Split disconnected transparent panes so Unity can sort their tight renderer bounds.
    // This only changes grouping; each indexed corner keeps its exact position/normal/UV.
    internal static List<Mesh> Split(Mesh source){
        var p=source.vertices;var n=source.normals;var uv=source.uv;var t=source.tangents;var ids=source.triangles;
        var parent=Enumerable.Range(0,p.Length).ToArray();var positions=new Dictionary<Vector3Int,int>();
        for(int i=0;i<p.Length;i++){
            var key=new Vector3Int(Mathf.RoundToInt(p[i].x*10000),Mathf.RoundToInt(p[i].y*10000),Mathf.RoundToInt(p[i].z*10000));
            if(positions.TryGetValue(key,out var first))Join(parent,i,first);else positions.Add(key,i);
        }
        for(int i=0;i<ids.Length;i+=3){Join(parent,ids[i],ids[i+1]);Join(parent,ids[i],ids[i+2]);}
        var groups=new SortedDictionary<int,List<int>>();
        for(int i=0;i<ids.Length;i+=3){int root=Find(parent,ids[i]);if(!groups.ContainsKey(root))groups[root]=new List<int>();groups[root].Add(ids[i]);groups[root].Add(ids[i+1]);groups[root].Add(ids[i+2]);}
        var result=new List<Mesh>();
        try{
            foreach(var group in groups.Values){
                var used=group.Distinct().ToArray();var remap=new Dictionary<int,int>();for(int i=0;i<used.Length;i++)remap.Add(used[i],i);
                var mesh=new Mesh{name="pane_"+result.Count,hideFlags=HideFlags.HideAndDontSave,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};result.Add(mesh);
                mesh.vertices=used.Select(i=>p[i]).ToArray();mesh.normals=used.Select(i=>n[i]).ToArray();mesh.uv=used.Select(i=>uv[i]).ToArray();if(t.Length==p.Length)mesh.tangents=used.Select(i=>t[i]).ToArray();
                mesh.triangles=group.Select(i=>remap[i]).ToArray();mesh.RecalculateBounds();mesh.UploadMeshData(false);
            }
            if(result.Sum(m=>m.triangles.Length)!=ids.Length)throw new InvalidOperationException("Glazing split changed triangle count.");
            return result;
        }catch{foreach(var mesh in result)UnityEngine.Object.DestroyImmediate(mesh);throw;}
    }
    static int Find(int[] p,int i){while(p[i]!=i){p[i]=p[p[i]];i=p[i];}return i;}
    static void Join(int[] p,int a,int b){a=Find(p,a);b=Find(p,b);if(a!=b)p[Math.Max(a,b)]=Math.Min(a,b);}
}
}
