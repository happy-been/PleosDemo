using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Temporary shading-only candidates. No mesh, material or scene asset is saved here.
[InitializeOnLoad]
public static class ParkingLotTucsonSubdivisionReview {
    const string Root="Assets/ParkingLotEnvironment";
    static readonly string Output=ParkingLotEditorPaths.Output;
    [Serializable] class Report {public int vertices,triangles;}
    static ParkingLotTucsonSubdivisionReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/tucson-subdivision-review.request";if(!File.Exists(request))return;
        string mode;try{mode=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
        try{Review(mode.StartsWith("door"),mode=="door-light");}catch(Exception e){File.WriteAllText(Output+"/tucson-subdivision-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(bool door,bool light){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved map first.");
        var source=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>()).First(t=>t.name.StartsWith("SUV_bay_")&&t.name.EndsWith("_hyundai-tucson")).gameObject;
        var current=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/SurfaceNormals/Tucson_paint_weighted_normals.asset");
        var sourcePaint=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Real_carpaint_1.mat");
        if(source==null||current==null||sourcePaint==null)throw new InvalidOperationException("Missing reviewed Tucson sources.");
        string folder=Output+(door?"/tucson-door-shading-experiment":"/tucson-subdivision-experiment");
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(folder+"/report.json"));
        if(report.vertices!=(door?22902:57264)||report.triangles!=(door?7634:19088))throw new InvalidDataException("Unexpected shading-only candidate size.");
        var meshes=new List<Mesh>();GameObject car=null;Material paint=null;var log=new StringBuilder();
        try{
            // Read and validate all candidates before creating the temporary scene object.
            // The unbounded Loop normal source folded child faces in the offline audit; exclude it.
            foreach(var key in light?new[]{"door-50"}:door?new[]{"door-0","door-50","door-80"}:new[]{"lin1","hybrid-bounded"})meshes.Add(ReadMesh(folder,key,report));
            // Clone the saved scene instance so the reviewed glazing and details stay identical.
            car=UnityEngine.Object.Instantiate(source);car.name="Temporary_Tucson_subdivision_review";car.hideFlags=HideFlags.HideAndDontSave;
            car.transform.position=new Vector3(18,0,4);car.transform.rotation=Quaternion.Euler(0,180,0);
            foreach(var lod in car.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
            var adopted=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/SurfaceNormals/Tucson_door_reflections.asset");
            var filter=car.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh==current||(adopted!=null&&f.sharedMesh==adopted));var renderer=filter.GetComponent<MeshRenderer>();
            filter.sharedMesh=current; // The comparison baseline is the pre-door-refinement source.
            paint=new Material(sourcePaint){hideFlags=HideFlags.HideAndDontSave};renderer.sharedMaterial=paint;
            if(light){
                paint.CopyPropertiesFromMaterial(AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Real_carpaint_0.mat"));
                ParkingLotCinematicCapture.TucsonMacroViews("door-white-current",car.transform.position);
                filter.sharedMesh=meshes[0];ParkingLotCinematicCapture.TucsonMacroViews("door-white-50",car.transform.position);
                car.transform.rotation=Quaternion.identity;paint.CopyPropertiesFromMaterial(sourcePaint);
                filter.sharedMesh=current;ParkingLotCinematicCapture.TucsonMacroViews("door-lit-current",car.transform.position);
                filter.sharedMesh=meshes[0];ParkingLotCinematicCapture.TucsonMacroViews("door-lit-50",car.transform.position);
                log.AppendLine("White paint and opposite-side sun: original versus door-50.");
            }else{
            ParkingLotCinematicCapture.TucsonMacroViews(door?"door-current":"subdiv-current",car.transform.position);
            for(int i=0;i<meshes.Count;i++){
                filter.sharedMesh=meshes[i];ParkingLotCinematicCapture.TucsonMacroViews(meshes[i].name,car.transform.position);
                log.AppendLine(meshes[i].name+": vertices="+meshes[i].vertexCount+", triangles="+meshes[i].triangles.Length/3+", bounds="+meshes[i].bounds);
            }
            }
        }finally{if(car!=null)UnityEngine.Object.DestroyImmediate(car);foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(paint!=null)UnityEngine.Object.DestroyImmediate(paint);}
        File.WriteAllText(Output+"/tucson-subdivision-review-success.txt",DateTime.Now.ToString("s")+"\n"+log+"Temporary objects removed; Saved="+!scene.isDirty);
    }
    static Mesh ReadMesh(string folder,string key,Report report){
        var positions=new Vector3[report.vertices];var normals=new Vector3[report.vertices];var uv=new Vector2[report.vertices];var ids=new int[report.triangles*3];
        using(var r=new BinaryReader(File.OpenRead(folder+"/"+key+".bin"))){
            if(r.BaseStream.Length!=report.vertices*32L+ids.Length*4L)throw new InvalidDataException("Candidate byte count mismatch: "+key);
            for(int i=0;i<positions.Length;i++){
                positions[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());normals[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                if(!Finite(positions[i])||!Finite(normals[i])||Mathf.Abs(normals[i].magnitude-1)>.001f||float.IsNaN(uv[i].x)||float.IsNaN(uv[i].y)||float.IsInfinity(uv[i].x)||float.IsInfinity(uv[i].y))throw new InvalidDataException("Invalid candidate vertex: "+key);
            }
            for(int i=0;i<ids.Length;i++){ids[i]=r.ReadInt32();if(ids[i]<0||ids[i]>=positions.Length)throw new InvalidDataException("Candidate index out of range.");}
        }
        var mesh=new Mesh{name=key,hideFlags=HideFlags.HideAndDontSave,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=ids;mesh.RecalculateTangents();mesh.RecalculateBounds();mesh.UploadMeshData(false);return mesh;
    }
    static bool Finite(Vector3 p){return !(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z));}
}
}
