using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotTreeAlphaReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotTreeAlphaReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        bool cards=File.Exists(Output+"/tree-cards-review.request");
        string path=Output+(cards?"/tree-cards-review.request":"/tree-alpha-review.request");if(!File.Exists(path))return;
        try{File.Delete(path);}catch(IOException){return;}
        try{Review(cards);}catch(Exception e){File.WriteAllText(Output+"/tree-alpha-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    [Serializable] class CardReport {public int vertices,triangles;}
    static Mesh ReadCards(){
        string folder=Output+"/leaf-cards-experiment";
        var report=JsonUtility.FromJson<CardReport>(File.ReadAllText(folder+"/report.json"));
        var source=JsonUtility.FromJson<ParkingLotVehicleImporter.ModelData>(File.ReadAllText("Assets/ParkingLotEnvironment/SourceModels~/tree-small-real/model.json")).bounds;
        var offset=new Vector3(-(source.min[0]+source.max[0])*.5f,-source.min[1],(source.min[2]+source.max[2])*.5f);
        var positions=new Vector3[report.vertices];var normals=new Vector3[report.vertices];var uv=new Vector2[report.vertices];var indices=new int[report.triangles*3];
        using(var reader=new BinaryReader(File.OpenRead(folder+"/leaf-cards.bin"))){
            if(reader.BaseStream.Length!=report.vertices*32L+indices.Length*4L)throw new InvalidDataException("Leaf-card byte count mismatch.");
            for(int i=0;i<positions.Length;i++){positions[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),-reader.ReadSingle())+offset;normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),-reader.ReadSingle());uv[i]=new Vector2(reader.ReadSingle(),1-reader.ReadSingle());}
            for(int i=0;i<indices.Length;i+=3){int a=reader.ReadInt32(),b=reader.ReadInt32(),c=reader.ReadInt32();indices[i]=a;indices[i+1]=c;indices[i+2]=b;}
        }
        var mesh=new Mesh{name="Temporary_leaf_island_cards",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};
        mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    static void RefreshBounds(UnityEngine.SceneManagement.Scene scene){foreach(var root in scene.GetRootGameObjects())foreach(var b in root.GetComponentsInChildren<ParkingLotFoliageBounds>())b.Refresh();}
    static void Review(bool cards){
        const string root="Assets/ParkingLotEnvironment";
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved map first.");
        var original=AssetDatabase.LoadAssetAtPath<Material>(root+"/RealVehicles/tree-small-real/Materials/001_tree_small_02_leaves.mat");
        var mask=AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/ReferenceAssets/tree_small_02_leaves_alpha_1k.png");
        if(original==null||mask==null)throw new InvalidOperationException("Missing leaf source or matching source atlas mask.");
        var originals=new Dictionary<MeshRenderer,Material[]>();
        foreach(var obj in scene.GetRootGameObjects())foreach(var r in obj.GetComponentsInChildren<MeshRenderer>())
            foreach(var m in r.sharedMaterials)if(m==original){originals[r]=r.sharedMaterials;break;}
        if(originals.Count!=15)throw new InvalidOperationException("Expected 15 detailed tree leaf renderers, found "+originals.Count);
        var candidate=new Material(original){hideFlags=HideFlags.HideAndDontSave};candidate.SetTexture("_AlphaTex",mask);
        var originalMeshes=new Dictionary<MeshFilter,Mesh>();Mesh cardMesh=null;
        try{
            if(!cards)ParkingLotCinematicCapture.LeafViews("original");
            foreach(var pair in originals){var mats=(Material[])pair.Value.Clone();for(int i=0;i<mats.Length;i++)if(mats[i]==original)mats[i]=candidate;pair.Key.sharedMaterials=mats;}
            if(cards){
                candidate.SetFloat("_Cutoff",.25f);cardMesh=ReadCards();
                foreach(var pair in originals){var filter=pair.Key.GetComponent<MeshFilter>();originalMeshes[filter]=filter.sharedMesh;filter.sharedMesh=cardMesh;}
                RefreshBounds(scene);ParkingLotCinematicCapture.LeafViews("cards");
            }else foreach(float cutoff in new[]{.25f,.5f}){candidate.SetFloat("_Cutoff",cutoff);ParkingLotCinematicCapture.LeafViews("mask"+Mathf.RoundToInt(cutoff*100));}
        }finally{foreach(var pair in originalMeshes)if(pair.Key!=null)pair.Key.sharedMesh=pair.Value;foreach(var pair in originals)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;RefreshBounds(scene);if(cardMesh!=null)UnityEngine.Object.DestroyImmediate(cardMesh);UnityEngine.Object.DestroyImmediate(candidate);}
        File.WriteAllText(Output+(cards?"/tree-cards-review-success.txt":"/tree-alpha-review-success.txt"),DateTime.Now.ToString("s")+"\nOriginal meshes/materials restored; Saved="+!scene.isDirty);
    }
}
}
