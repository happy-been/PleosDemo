using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Static, subtle contact shading for the generated villa roofs; one mesh/material, no depth prepass.
public static class ParkingLotRoofContact {
    const string Root="Assets/ParkingLotEnvironment";
    const string Name="14_Rooftop_contact_shading";
    const float RoofHeight=12.8f,Feather=.45f;
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Roof contacts are restricted to this map.");
        var shader=Shader.Find("ParkingLot/ArchitecturalContact");if(shader==null)throw new InvalidOperationException("Import the roof contact shader first.");
        var verts=new List<Vector3>();var uv=new List<Vector2>();var extents=new List<Vector2>();var colors=new List<Color>();var indices=new List<int>();
        foreach(var b in ParkingLotNeighborhoodInfill.Buildings){
            var transform=Matrix4x4.TRS(b.position,Quaternion.Euler(0,b.yaw,0),b.scale);
            float hutX=b.finish==1?3.8f:-3.8f;
            Add(transform,hutX,2.2f,1.5f,1.6f,.23f,verts,uv,extents,colors,indices);
            Add(transform,-hutX,2.8f,.48f,.48f,.20f,verts,uv,extents,colors,indices);
            for(int k=0;k<3;k++)Add(transform,-2+k*1.25f,-2.8f,.5f,.21f,.16f,verts,uv,extents,colors,indices);
            // Clip each outer boundary to the roof so an AO quad cannot float outside the building.
            Add(transform,-6.41f,0,.09f,5.5f,.16f,verts,uv,extents,colors,indices);
            Add(transform,6.41f,0,.09f,5.5f,.16f,verts,uv,extents,colors,indices);
            Add(transform,0,-5.41f,6.5f,.09f,.16f,verts,uv,extents,colors,indices);
            Add(transform,0,5.41f,6.5f,.09f,.16f,verts,uv,extents,colors,indices);
        }
        var mesh=new Mesh{name="Villa_roof_contact_surfaces"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetUVs(1,extents);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        Directory.CreateDirectory(Root+"/Meshes/KoreanContext");mesh=ParkingLotMeshAsset.Save(mesh,Root+"/Meshes/KoreanContext/Villa_roof_contact_surfaces.asset");
        string path=Root+"/Materials/KoreanContext/Villa_roof_contact.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.SetFloat("_Feather",Feather);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        foreach(var old in scene.GetRootGameObjects())if(old.name==Name)UnityEngine.Object.DestroyImmediate(old);
        var go=new GameObject(Name);SceneManager.MoveGameObjectToScene(go,scene);go.isStatic=true;go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        Debug.Log("PARKINGLOT_ROOF_CONTACT: "+(indices.Count/3)+" triangles; 1 renderer; no collider or additional render target.");
    }
    static void Add(Matrix4x4 transform,float x,float z,float hx,float hz,float opacity,List<Vector3> verts,List<Vector2> uv,List<Vector2> extents,List<Color> colors,List<int> indices){
        float x0=Mathf.Max(-6.5f,x-hx-Feather),x1=Mathf.Min(6.5f,x+hx+Feather),z0=Mathf.Max(-5.5f,z-hz-Feather),z1=Mathf.Min(5.5f,z+hz+Feather);
        int start=verts.Count;
        foreach(var p in new[]{new Vector2(x0,z0),new Vector2(x0,z1),new Vector2(x1,z1),new Vector2(x1,z0)}){
            verts.Add(transform.MultiplyPoint3x4(new Vector3(p.x,RoofHeight+.008f,p.y)));uv.Add(new Vector2(p.x-x,p.y-z));extents.Add(new Vector2(hx,hz));colors.Add(new Color(1,1,1,opacity));
        }
        indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
    }
}
}
