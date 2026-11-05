using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// A shared, near-LOD shading derivative; source mesh, paint, positions and car layout are preserved.
public static class ParkingLotTucsonDoorFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Source=Root+"/Meshes/SurfaceNormals/Tucson_paint_weighted_normals.asset";
    const string Destination=Root+"/Meshes/SurfaceNormals/Tucson_door_reflections.asset";
    const string Binary=Root+"/SourceModels~/tucson-door-reflections/door-50.bin";
    const string SourceHash="3ff22dd22f2fa3f2f16712fd5be04f8fe45ad70fbbdf7fa67616a414f30c62d7";
    const string CandidateHash="98eb1ecc9ae4c133ce2ca3593d7b448b3abc7ff33b1bc3d11b41d5ed40a349f8";
    const int Vertices=22902, Triangles=7634;
    static string Hash(byte[] data){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    static byte[] Pack(Mesh m){
        var p=m.vertices;var n=m.normals;var u=m.uv;
        if(n.Length!=p.Length||u.Length!=p.Length||m.subMeshCount!=1)throw new InvalidDataException("Unexpected paint vertex streams.");
        using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
            for(int i=0;i<p.Length;i++){w.Write(p[i].x);w.Write(p[i].y);w.Write(p[i].z);w.Write(n[i].x);w.Write(n[i].y);w.Write(n[i].z);w.Write(u[i].x);w.Write(u[i].y);}
            foreach(int i in m.triangles)w.Write(i);w.Flush();return stream.ToArray();
        }
    }
    static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
    static Mesh LoadCandidate(Mesh source){
        byte[] bytes=File.ReadAllBytes(Binary);
        if(Hash(bytes)!=CandidateHash||bytes.Length!=Vertices*32+Triangles*12)throw new InvalidDataException("Reviewed door derivative does not match its checksum.");
        var p=new Vector3[Vertices];var n=new Vector3[Vertices];var u=new Vector2[Vertices];var ids=new int[Triangles*3];
        using(var r=new BinaryReader(new MemoryStream(bytes))){
            for(int i=0;i<Vertices;i++){
                p[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());n[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());u[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                if(!Finite(p[i].x)||!Finite(p[i].y)||!Finite(p[i].z)||!Finite(n[i].x)||!Finite(n[i].y)||!Finite(n[i].z)||!Finite(u[i].x)||!Finite(u[i].y)||Mathf.Abs(n[i].magnitude-1)>.001f)throw new InvalidDataException("Invalid door vertex.");
            }
            for(int i=0;i<ids.Length;i++){ids[i]=r.ReadInt32();if(ids[i]<0||ids[i]>=Vertices)throw new InvalidDataException("Invalid door triangle.");}
        }
        var mesh=new Mesh{name="Tucson_door_reflections",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.vertices=p;mesh.normals=n;mesh.uv=u;mesh.triangles=ids;mesh.RecalculateBounds();mesh.RecalculateTangents();
        if((mesh.bounds.center-source.bounds.center).sqrMagnitude>.000001f||(mesh.bounds.size-source.bounds.size).sqrMagnitude>.000001f){UnityEngine.Object.DestroyImmediate(mesh);throw new InvalidDataException("Door derivative bounds changed.");}
        return mesh;
    }
    static MeshFilter[] Filters(Scene scene,Mesh original,Mesh result){
        if(scene.path!=Target||!scene.isLoaded)throw new InvalidOperationException("Door refinement is restricted to ParklingLot.");
        var cars=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("SUV_bay_")&&t.name.EndsWith("_hyundai-tucson")).ToArray();
        if(cars.Length!=6)throw new InvalidOperationException("Expected six map Tucsons.");
        return cars.Select(car=>{
            var matches=car.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&(f.sharedMesh==original||(result!=null&&f.sharedMesh==result))).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one near paint mesh per Tucson.");
            var renderer=matches[0].GetComponent<MeshRenderer>();var group=car.GetComponentInChildren<LODGroup>();
            if(renderer==null||group==null)throw new InvalidOperationException("Missing Tucson renderer or LOD group.");
            var lods=group.GetLODs();if(lods.Length!=2||!lods[0].renderers.Contains(renderer)||lods[1].renderers.Contains(renderer))throw new InvalidOperationException("Door derivative must belong to near LOD only.");
            return matches[0];
        }).ToArray();
    }
    public static void Build(Scene scene){
        var original=AssetDatabase.LoadAssetAtPath<Mesh>(Source);var result=AssetDatabase.LoadAssetAtPath<Mesh>(Destination);
        if(original==null||Hash(Pack(original))!=SourceHash)throw new InvalidDataException("Reviewed source paint mesh has changed.");
        var filters=Filters(scene,original,result); // Preflight all instances before editing any.
        if(result==null)result=ParkingLotMeshAsset.Save(LoadCandidate(original),Destination);
        else if(Hash(Pack(result))!=CandidateHash)throw new InvalidDataException("Existing door derivative was edited; refusing to overwrite it.");
        foreach(var filter in filters)filter.sharedMesh=result;
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(ParkingLotEditorPaths.Output+"/tucson-door-finish-audit.txt",DateTime.Now.ToString("s")+"\n"+Validate(scene));
    }
    public static string Validate(Scene scene){
        var original=AssetDatabase.LoadAssetAtPath<Mesh>(Source);var result=AssetDatabase.LoadAssetAtPath<Mesh>(Destination);
        if(original==null||result==null||Hash(Pack(original))!=SourceHash||Hash(Pack(result))!=CandidateHash)throw new InvalidDataException("Door/source checksum failed.");
        var filters=Filters(scene,original,result);if(filters.Any(f=>f.sharedMesh!=result))throw new InvalidOperationException("A Tucson still uses the earlier near paint mesh.");
        return "NearCars=6\nSharedMesh=1\nVerticesPerPaintMesh="+result.vertexCount+"\nTrianglesPerPaintMesh="+result.triangles.Length/3+"\nAddedTrianglesAllSix=17172\nSourceChecksum=True\nCandidateChecksum=True\nOnlyNearLOD=True\nSaved="+!scene.isDirty;
    }
}
}
