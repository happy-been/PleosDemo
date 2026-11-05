using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Uses derived shop meshes: only old awning triangles are omitted. Original assets stay intact.
[InitializeOnLoad]
public static class ParkingLotAwningFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string MeshDir=Root+"/Meshes/ShopAwnings";
    const string ChildName="Shop_awning_fabric";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotAwningFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string flag=Output+"/awning-finish.request";if(!File.Exists(flag))return;
        string mode;try{mode=File.ReadAllText(flag).Trim();File.Delete(flag);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved map before awning work.");
            if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save awning map.");
            }else if(mode!="validate")throw new InvalidOperationException("Unknown awning request.");
            File.WriteAllText(Output+"/awning-"+mode+"-success.txt",Validate(scene));
        }catch(Exception e){File.WriteAllText(Output+"/awning-finish-error.txt",e.ToString());Debug.LogException(e);}
    }
    static MeshFilter[] Shops(Scene scene){
        if(scene.path!=Target||!scene.isLoaded)throw new InvalidOperationException("Awning finish belongs only to ParklingLot.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="09_Korean_neighborhood_context");
        var shops=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Shop_block_",StringComparison.Ordinal)).ToArray();
        if(shops.Length!=3)throw new InvalidOperationException("Expected three original shop objects.");return shops;
    }
    static Mesh Source(MeshFilter f){
        var source=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/KoreanContext/"+f.name+".asset");
        if(source==null||f.sharedMesh==null)throw new InvalidOperationException("Missing original shop mesh.");
        string path=AssetDatabase.GetAssetPath(f.sharedMesh);
        if(f.sharedMesh!=source&&path!=MeshDir+"/"+f.name+"_body.asset")throw new InvalidOperationException("Unrecognized shop mesh; preserve manual edits.");return source;
    }
    static int[] ClothSlots(MeshFilter f,Mesh source){
        var materials=f.GetComponent<MeshRenderer>().sharedMaterials;
        if(materials.Length!=source.subMeshCount)throw new InvalidOperationException("Shop material layout changed.");
        var slots=Enumerable.Range(0,materials.Length).Where(i=>materials[i]!=null&&materials[i].name.StartsWith("Korean_shop_awning_",StringComparison.Ordinal)).ToArray();
        if(slots.Length!=2||slots.Sum(i=>source.GetIndices(i).Length)!=804)throw new InvalidOperationException("Original awning geometry changed.");return slots;
    }
    public static void Build(Scene scene){
        var shops=Shops(scene);
        foreach(var f in shops){var source=Source(f);ClothSlots(f,source);var existing=f.transform.Find(ChildName);if(existing!=null&&(existing.GetComponent<MeshFilter>()==null||AssetDatabase.GetAssetPath(existing.GetComponent<MeshFilter>().sharedMesh)!=MeshDir+"/Shop_awning_fabric.asset"))throw new InvalidOperationException("Unrecognized existing awning child.");}
        ParkingLotAwningReview.PrepareTexture("terlenka_diff_1k.jpg",false);ParkingLotAwningReview.PrepareTexture("terlenka_nor_gl_1k.jpg",true);
        Directory.CreateDirectory(MeshDir);AssetDatabase.Refresh();
        var cloth=ParkingLotMeshAsset.Save(ParkingLotAwningGeometry.Create(),MeshDir+"/Shop_awning_fabric.asset");
        foreach(var f in shops){
            var source=Source(f);var slots=ClothSlots(f,source);var originalMaterials=f.GetComponent<MeshRenderer>().sharedMaterials;
            var body=UnityEngine.Object.Instantiate(source);body.name=f.name+"_body";
            foreach(int index in slots)body.SetTriangles(new int[0],index,false);
            var pair=new Material[2];
            foreach(int index in slots){
                var old=originalMaterials[index];string path=Root+"/Materials/RealismDetails/"+old.name+"_fabric.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=ParkingLotAwningReview.FabricMaterial(old);material.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(material,path);}
                else if(material.shader.name!="ParkingLot/AwningFabric"||material.color!=old.color||!material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")||!material.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF"))throw new InvalidOperationException("Existing cloth material was edited.");
                pair[old.name.EndsWith("_cream",StringComparison.Ordinal)?1:0]=material;
            }
            if(pair.Any(m=>m==null)){UnityEngine.Object.DestroyImmediate(body);throw new InvalidOperationException("Incomplete fabric colors.");}
            var oldChild=f.transform.Find(ChildName);if(oldChild!=null)UnityEngine.Object.DestroyImmediate(oldChild.gameObject);
            f.sharedMesh=ParkingLotMeshAsset.Save(body,MeshDir+"/"+f.name+"_body.asset");
            var go=new GameObject(ChildName);go.transform.SetParent(f.transform,false);go.AddComponent<MeshFilter>().sharedMesh=cloth;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=pair;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
        }
        AssetDatabase.SaveAssets();
    }
    public static string Validate(Scene scene){
        var shops=Shops(scene);var expected=ParkingLotAwningGeometry.Create();
        try{
            foreach(var f in shops){
                var source=Source(f);var body=f.sharedMesh;var slots=ClothSlots(f,source);
                if(!body.vertices.SequenceEqual(source.vertices)||!body.normals.SequenceEqual(source.normals)||!body.uv.SequenceEqual(source.uv)||!body.tangents.SequenceEqual(source.tangents)||body.subMeshCount!=source.subMeshCount)throw new InvalidOperationException("Unrelated shop vertex data changed.");
                if(!body.colors.SequenceEqual(source.colors)||!body.uv2.SequenceEqual(source.uv2)||!body.uv3.SequenceEqual(source.uv3)||!body.uv4.SequenceEqual(source.uv4))throw new InvalidOperationException("Unrelated shop extra vertex data changed.");
                for(int i=0;i<source.subMeshCount;i++)if(!body.GetIndices(i).SequenceEqual(slots.Contains(i)?new int[0]:source.GetIndices(i)))throw new InvalidOperationException("Unrelated shop triangles changed.");
                var child=f.transform.Find(ChildName);if(child==null||child.localPosition!=Vector3.zero||child.localRotation!=Quaternion.identity||child.localScale!=Vector3.one||child.GetComponents<Collider>().Length!=0)throw new InvalidOperationException("Invalid fabric child.");
                var mesh=child.GetComponent<MeshFilter>().sharedMesh;var renderer=child.GetComponent<MeshRenderer>();
                if(mesh.vertexCount!=1326||!mesh.vertices.SequenceEqual(expected.vertices)||!mesh.normals.SequenceEqual(expected.normals)||!mesh.uv.SequenceEqual(expected.uv)||!mesh.triangles.SequenceEqual(expected.triangles))throw new InvalidOperationException("Saved cloth differs from reviewed geometry.");
                if(renderer.sharedMaterials.Length!=2||renderer.sharedMaterials.Any(m=>m==null||m.shader.name!="ParkingLot/AwningFabric"||ShaderUtil.ShaderHasError(m.shader)||!m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")||!m.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF")))throw new InvalidOperationException("Invalid cloth material.");
            }
        }finally{UnityEngine.Object.DestroyImmediate(expected);}
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nShops=3\nClothTrianglesEach=1102\nAddedTriangles=2502\nAddedRenderers=3\nOtherShopMeshDataUnchanged=True\nOriginalShopMaterialsRetained=True";
    }
}
}
