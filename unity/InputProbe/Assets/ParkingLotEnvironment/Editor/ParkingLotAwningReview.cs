using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Temporary review only: original shop mesh/material references are restored in finally.
[InitializeOnLoad]
public static class ParkingLotAwningReview {
    const string Root="Assets/ParkingLotEnvironment";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotAwningReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string flag=Output+"/awning-review.request";if(!File.Exists(flag))return;
        try{File.Delete(flag);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/awning-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    internal static void PrepareTexture(string name,bool normal){
        string path=Root+"/KoreanAssets/"+name;var imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(imp==null)throw new InvalidOperationException("Missing candidate source "+path);
        var type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if(imp.textureType!=type||imp.sRGBTexture==normal||imp.maxTextureSize!=1024||imp.anisoLevel!=8||!imp.mipmapEnabled||imp.wrapMode!=TextureWrapMode.Repeat||imp.filterMode!=FilterMode.Trilinear||imp.textureCompression!=TextureImporterCompression.CompressedHQ){
            imp.textureType=type;imp.sRGBTexture=!normal;imp.maxTextureSize=1024;imp.anisoLevel=8;imp.mipmapEnabled=true;imp.wrapMode=TextureWrapMode.Repeat;imp.filterMode=FilterMode.Trilinear;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();
        }
    }
    internal static Material FabricMaterial(Material source){
        var shader=Shader.Find("ParkingLot/AwningFabric");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Fabric shader unavailable.");
        var material=new Material(shader){name=source.name+"_fabric",hideFlags=HideFlags.HideAndDontSave,color=source.color};
        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/KoreanAssets/terlenka_diff_1k.jpg");material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/KoreanAssets/terlenka_nor_gl_1k.jpg"));
        if(material.mainTexture==null||material.GetTexture("_BumpMap")==null){UnityEngine.Object.DestroyImmediate(material);throw new InvalidOperationException("Missing cloth sources.");}
        material.SetVector("_FabricRepeat",new Vector4(1/.26570815f,1/.2663f,0,0));material.enableInstancing=true;
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");return material;
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var root=scene.GetRootGameObjects().Single(g=>g.name=="09_Korean_neighborhood_context");
        var shops=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("Shop_block_",StringComparison.Ordinal)).ToArray();
        if(shops.Length!=3)throw new InvalidOperationException("Expected exactly three shop meshes.");
        if(shops.Any(f=>f.transform.Find("Shop_awning_fabric")!=null))throw new InvalidOperationException("This comparison requires the saved pre-adoption map; do not layer candidates over finished cloth.");
        var originals=shops.ToDictionary(f=>f,f=>f.sharedMesh);
        var temporaries=new List<UnityEngine.Object>();var children=new List<GameObject>();
        try{
            PrepareTexture("terlenka_diff_1k.jpg",false);PrepareTexture("terlenka_nor_gl_1k.jpg",true);
            var shader=Shader.Find("ParkingLot/AwningFabric");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Candidate fabric shader failed.");
            ParkingLotCinematicCapture.AwningViews("original");
            var cloth=ParkingLotAwningGeometry.Create();cloth.hideFlags=HideFlags.HideAndDontSave;temporaries.Add(cloth);
            if(cloth.triangles.Length/3>=1500||cloth.subMeshCount!=2||cloth.normals.Any(n=>float.IsNaN(n.x)||n.sqrMagnitude<.9f))throw new InvalidOperationException("Invalid awning candidate geometry.");
            var materials=new Dictionary<Material,Material>();int removed=0;
            foreach(var f in shops){
                var original=f.sharedMesh;var slots=f.GetComponent<MeshRenderer>().sharedMaterials;
                var indices=Enumerable.Range(0,slots.Length).Where(i=>slots[i]!=null&&slots[i].name.StartsWith("Korean_shop_awning_",StringComparison.Ordinal)).ToArray();
                if(indices.Length!=2||slots.Length!=original.subMeshCount)throw new InvalidOperationException("Unexpected shop awning slots.");
                var body=UnityEngine.Object.Instantiate(original);body.hideFlags=HideFlags.HideAndDontSave;temporaries.Add(body);
                var pair=new Material[2];
                foreach(int index in indices){
                    removed+=body.GetIndices(index).Length/3;body.SetTriangles(new int[0],index,false);
                    var source=slots[index];Material candidate;
                    if(!materials.TryGetValue(source,out candidate)){
                        candidate=FabricMaterial(source);materials.Add(source,candidate);temporaries.Add(candidate);
                    }
                    pair[source.name.EndsWith("_cream",StringComparison.Ordinal)?1:0]=candidate;
                }
                if(pair.Any(m=>m==null))throw new InvalidOperationException("Missing cloth stripe material.");
                f.sharedMesh=body;
                var go=new GameObject("Temporary_awning_candidate"){hideFlags=HideFlags.HideAndDontSave};children.Add(go);go.transform.SetParent(f.transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=cloth;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=pair;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            }
            ParkingLotCinematicCapture.AwningViews("candidate");
            foreach(var child in children){var r=child.GetComponent<MeshRenderer>();r.sharedMaterials=r.sharedMaterials.Select(m=>materials.Single(p=>p.Value==m).Key).ToArray();}
            ParkingLotCinematicCapture.AwningClose("geometry-original-material");
            foreach(var child in children){var r=child.GetComponent<MeshRenderer>();r.sharedMaterials=r.sharedMaterials.Select(m=>materials[m]).ToArray();}
            foreach(var m in materials.Values){m.SetFloat("_BumpScale",0);m.SetFloat("_WeaveStrength",0);}
            ParkingLotCinematicCapture.AwningClose("fabric-flat-control");
            File.WriteAllText(Output+"/awning-review-geometry.txt","ClothVertices="+cloth.vertexCount+"\nClothTrianglesEach="+cloth.triangles.Length/3+"\nRemovedOriginalTriangles="+removed+"\nAddedTotalTriangles="+(cloth.triangles.Length-removed));
        }finally{
            foreach(var f in shops)f.sharedMesh=originals[f];foreach(var go in children)UnityEngine.Object.DestroyImmediate(go);foreach(var item in temporaries)UnityEngine.Object.DestroyImmediate(item);
        }
        File.WriteAllText(Output+"/awning-review-success.txt",DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nShopMeshesRestored=True\nTemporaryObjectsRemoved=True");
    }
}
}
