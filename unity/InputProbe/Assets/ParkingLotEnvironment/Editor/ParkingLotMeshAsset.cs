using System;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// CopySerialized can leave a live Mesh GPU buffer stale. Set mesh data explicitly instead.
public static class ParkingLotMeshAsset {
    public static Mesh Save(Mesh source,string path){
        if(!path.StartsWith("Assets/ParkingLotEnvironment/",StringComparison.Ordinal)||!path.EndsWith(".asset",StringComparison.Ordinal))throw new InvalidOperationException("Only dedicated map mesh assets may be saved.");
        if(source.blendShapeCount!=0)throw new InvalidOperationException("This map mesh writer does not handle animated meshes.");
        if(source.normals.Length==0)source.RecalculateNormals();
        if(source.uv.Length==0){var v=source.vertices;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=new Vector2(v[i].x,v[i].z);source.uv=uv;}
        if(source.tangents.Length==0)source.RecalculateTangents();
        var target=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(target==null){source.UploadMeshData(false);AssetDatabase.CreateAsset(source,path);return source;}
        if(target==source)throw new InvalidOperationException("Pass a new mesh, not its existing asset.");
        target.Clear(false);target.indexFormat=source.indexFormat;target.name=source.name;
        target.vertices=source.vertices;target.normals=source.normals;target.tangents=source.tangents;target.colors=source.colors;
        target.uv=source.uv;target.uv2=source.uv2;target.uv3=source.uv3;target.uv4=source.uv4;
        target.subMeshCount=source.subMeshCount;
        for(int i=0;i<source.subMeshCount;i++)target.SetIndices(source.GetIndices(i),source.GetTopology(i),i,false);
        target.bounds=source.bounds;target.UploadMeshData(false);
        UnityEngine.Object.DestroyImmediate(source);EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);return target;
    }
}
}
