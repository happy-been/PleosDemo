using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Smooths scene-owned paint derivatives, leaving source positions, triangles, UVs and imported meshes intact.
public static class ParkingLotPaintNormals {
    const string Folder="Assets/ParkingLotEnvironment/Meshes/SurfaceNormals";
    static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
    struct Face {public Vector3 normal;public float weight;}
    public static void Reset(){cache.Clear();Directory.CreateDirectory(Folder);}
    public static void Refine(MeshFilter filter,string key="Tucson",float creaseDegrees=60,bool angleWeightOnly=false){
        if(filter==null||filter.sharedMesh==null)return;
        if(filter.gameObject.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Paint normal derivatives are restricted to the map.");
        var source=filter.sharedMesh;string cacheKey=source.GetInstanceID()+":"+key+":"+creaseDegrees+":"+angleWeightOnly;if(cache.TryGetValue(cacheKey,out var refined)){filter.sharedMesh=refined;return;}
        var p=source.vertices;var original=source.normals;var ids=source.triangles;var keys=new Vector3Int[p.Length];var faces=new Dictionary<Vector3Int,List<Face>>();
        for(int i=0;i<p.Length;i++){keys[i]=new Vector3Int(Mathf.RoundToInt(p[i].x*100000),Mathf.RoundToInt(p[i].y*100000),Mathf.RoundToInt(p[i].z*100000));if(!faces.ContainsKey(keys[i]))faces[keys[i]]=new List<Face>();}
        for(int i=0;i<ids.Length;i+=3){
            int a=ids[i],b=ids[i+1],c=ids[i+2];var cross=Vector3.Cross(p[b]-p[a],p[c]-p[a]);float area=cross.magnitude;if(area<1e-12f)continue;var normal=cross/area;
            for(int j=0;j<3;j++){int at=ids[i+j],next=ids[i+(j+1)%3],last=ids[i+(j+2)%3];float angle=Vector3.Angle(p[next]-p[at],p[last]-p[at])*Mathf.Deg2Rad;faces[keys[at]].Add(new Face{normal=normal,weight=(angleWeightOnly?1:Mathf.Sqrt(area))*angle});}
        }
        var normals=new Vector3[p.Length];int changed=0;
        for(int i=0;i<p.Length;i++){
            var sum=Vector3.zero;var basis=original[i].normalized;
            foreach(var face in faces[keys[i]])if(Vector3.Dot(face.normal,basis)>Mathf.Cos(creaseDegrees*Mathf.Deg2Rad))sum+=face.normal*face.weight;
            normals[i]=sum.sqrMagnitude>1e-12f?sum.normalized:basis;if(Vector3.Angle(normals[i],basis)>.5f)changed++;
        }
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name=key+"_paint_weighted_normals";mesh.normals=normals;mesh.RecalculateTangents();
        string path=Folder+"/"+mesh.name+".asset";refined=ParkingLotMeshAsset.Save(mesh,path);cache[cacheKey]=refined;filter.sharedMesh=refined;
        File.WriteAllText(ParkingLotEditorPaths.Output+"/paint-normal-refinement-"+key+".txt",key+" painted surface only. Source untouched; vertex positions, topology and UVs retained.\nVertices="+p.Length+"\nNormalsChanged="+changed);
    }
}
}
