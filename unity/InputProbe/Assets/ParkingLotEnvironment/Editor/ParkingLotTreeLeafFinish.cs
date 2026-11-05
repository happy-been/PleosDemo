using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// A map-instance override only. Imported source meshes, prefabs and materials are retained.
public static class ParkingLotTreeLeafFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string MeshPath=Root+"/Meshes/RealismDetails/Tree_leaf_island_cards.asset";
    const string MaterialPath=Root+"/Materials/RealismDetails/Tree_leaf_island_cards.mat";
    [Serializable] class Report {public int vertices,triangles;}
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Leaf finish belongs only to the parking map.");
        var targets=new List<MeshFilter>();
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name=="Photographic_3D_tree"){
            var leaf=t.Find("Model_geometry/tree_small_02_leaves");
            if(leaf==null||leaf.GetComponent<MeshFilter>()==null||leaf.GetComponent<MeshRenderer>()==null)throw new InvalidOperationException("Detailed tree has an unexpected structure.");
            targets.Add(leaf.GetComponent<MeshFilter>());
        }
        if(targets.Count!=15)throw new InvalidOperationException("Expected 15 map-owned near trees, found "+targets.Count);
        var original=AssetDatabase.LoadAssetAtPath<Material>(Root+"/RealVehicles/tree-small-real/Materials/001_tree_small_02_leaves.mat");
        var alpha=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/tree_small_02_leaves_alpha_1k.png");
        if(original==null||alpha==null)throw new InvalidOperationException("Leaf source material or silhouette mask is missing.");
        Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));AssetDatabase.Refresh();
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if(mesh==null)mesh=ParkingLotMeshAsset.Save(ReadMesh(),MeshPath);
        if(mesh.vertexCount!=121000||mesh.triangles.Length!=181500)throw new InvalidOperationException("Unexpected derivative mesh; review before applying.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null){material=new Material(original){name="Tree_leaf_island_cards"};AssetDatabase.CreateAsset(material,MaterialPath);}
        material.SetTexture("_AlphaTex",alpha);material.SetFloat("_Cutoff",.25f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        foreach(var target in targets){target.sharedMesh=mesh;target.GetComponent<MeshRenderer>().sharedMaterial=material;}
        foreach(var root in scene.GetRootGameObjects())foreach(var bounds in root.GetComponentsInChildren<ParkingLotFoliageBounds>())bounds.Refresh();
        File.WriteAllText(ParkingLotEditorPaths.Output+"/tree-leaf-finish-audit.txt",DateTime.Now.ToString("s")+"\nNearTrees=15\nSharedMeshes=1\nLeafTrianglesPerTree=60500\nPreviousLeafTrianglesPerTree=75247\nLeafVerticesPerTree=121000\nSourceAssetsUnchanged=True\nCutoff=.25");
    }
    static Mesh ReadMesh(){
        string folder=Root+"/SourceModels~/tree-leaf-cards";
        var report=JsonUtility.FromJson<Report>(File.ReadAllText(folder+"/report.json"));
        if(report.vertices!=121000||report.triangles!=60500)throw new InvalidDataException("Leaf card manifest mismatch.");
        var source=JsonUtility.FromJson<ParkingLotVehicleImporter.ModelData>(File.ReadAllText(Root+"/SourceModels~/tree-small-real/model.json")).bounds;
        var offset=new Vector3(-(source.min[0]+source.max[0])*.5f,-source.min[1],(source.min[2]+source.max[2])*.5f);
        var positions=new Vector3[report.vertices];var normals=new Vector3[report.vertices];var uv=new Vector2[report.vertices];var indices=new int[report.triangles*3];
        using(var reader=new BinaryReader(File.OpenRead(folder+"/leaf-cards.bin"))){
            if(reader.BaseStream.Length!=report.vertices*32L+indices.Length*4L)throw new InvalidDataException("Leaf card byte count mismatch.");
            for(int i=0;i<positions.Length;i++){
                positions[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),-reader.ReadSingle())+offset;
                normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),-reader.ReadSingle());uv[i]=new Vector2(reader.ReadSingle(),1-reader.ReadSingle());
                if(!Finite(positions[i])||!Finite(normals[i])||float.IsNaN(uv[i].x)||float.IsNaN(uv[i].y)||float.IsInfinity(uv[i].x)||float.IsInfinity(uv[i].y))throw new InvalidDataException("Non-finite leaf vertex.");
            }
            for(int i=0;i<indices.Length;i+=3){int a=reader.ReadInt32(),b=reader.ReadInt32(),c=reader.ReadInt32();if(a<0||b<0||c<0||a>=positions.Length||b>=positions.Length||c>=positions.Length)throw new InvalidDataException("Leaf index out of range.");indices[i]=a;indices[i+1]=c;indices[i+2]=b;}
        }
        var mesh=new Mesh{name="Tree_leaf_island_cards",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    static bool Finite(Vector3 p){return !(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z));}
}
}
