using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace ParkingLotEnvironment.Editor {
// Consumes a small offline intermediate format. No glTF runtime or project package changes.
public static class ParkingLotVehicleImporter {
    const string Root="Assets/ParkingLotEnvironment/RealVehicles";
    [Serializable] public class ModelData { public string name; public MaterialData[] materials; public MeshData[] meshes; public BoundsData bounds; }
    [Serializable] public class BoundsData { public float[] min,max; }
    [Serializable] public class MaterialData {
        public string name,baseColorTexture,normalTexture,metallicRoughnessTexture,emissiveTexture,alphaMode,occlusionTexture;
        public float[] baseColor,emissiveFactor; public float metallic,roughness=0.5f,alphaCutoff=0.5f; public bool doubleSided;
    }
    [Serializable] public class MeshData { public string name,file; public int material,vertexCount,indexCount; }
    static string Safe(string s) { foreach(char c in Path.GetInvalidFileNameChars())s=s.Replace(c,'_');return s.Replace('/','_').Replace('\\','_'); }
    public static void RefreshMaterials(string jsonPath,string key){var data=JsonUtility.FromJson<ModelData>(File.ReadAllText(jsonPath));for(int i=0;i<data.materials.Length;i++)MakeMaterial(data.materials[i],Path.GetDirectoryName(jsonPath),Root+"/"+key,i);}
    public static GameObject Import(string jsonPath,string key,float realLength,string materialSourceKey=null,string normalizationJson=null) {
        string folder=Root+"/"+key;Directory.CreateDirectory(folder+"/Textures");Directory.CreateDirectory(folder+"/Materials");Directory.CreateDirectory(folder+"/Meshes");AssetDatabase.Refresh();
        var data=JsonUtility.FromJson<ModelData>(File.ReadAllText(jsonPath));string src=Path.GetDirectoryName(jsonPath);
        var materials=new Material[data.materials.Length];
        for(int i=0;i<materials.Length;i++){
            if(materialSourceKey==null)materials[i]=MakeMaterial(data.materials[i],src,folder,i);
            else {string path=Root+"/"+materialSourceKey+"/Materials/"+i.ToString("D3")+"_"+Safe(data.materials[i].name)+".mat";materials[i]=AssetDatabase.LoadAssetAtPath<Material>(path);if(materials[i]==null)throw new InvalidOperationException("LOD material order must match source: "+path);}
        }
        var parts=new Dictionary<int,List<CombineInstance>>();
        foreach(var item in data.meshes) {
            var mesh=new Mesh{name=item.name,indexFormat=IndexFormat.UInt32};var positions=new Vector3[item.vertexCount];var normals=new Vector3[item.vertexCount];var uv=new Vector2[item.vertexCount];
            var indices=new int[item.indexCount];
            using(var r=new BinaryReader(File.OpenRead(Path.Combine(src,item.file)))) {
                for(int i=0;i<positions.Length;i++){positions[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),-r.ReadSingle());normals[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),-r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),1-r.ReadSingle());}
                for(int i=0;i<indices.Length;i+=3){int a=(int)r.ReadUInt32(),b=(int)r.ReadUInt32(),c=(int)r.ReadUInt32();indices[i]=a;indices[i+1]=c;indices[i+2]=b;}
            }
            mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(!parts.ContainsKey(item.material))parts[item.material]=new List<CombineInstance>();
            parts[item.material].Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});
        }
        var root=new GameObject(key);var content=new GameObject("Model_geometry").transform;content.SetParent(root.transform,false);
        foreach(var pair in parts) {
            var combined=new Mesh{name=key+"_"+pair.Key,indexFormat=IndexFormat.UInt32};combined.CombineMeshes(pair.Value.ToArray(),true,true);combined.RecalculateBounds();
            string path=folder+"/Meshes/part_"+pair.Key+".asset";combined=SaveMesh(combined,path);
            var go=new GameObject(data.materials[pair.Key].name);go.transform.SetParent(content,false);go.AddComponent<MeshFilter>().sharedMesh=combined;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[pair.Key];renderer.shadowCastingMode=ShadowCastingMode.TwoSided;renderer.receiveShadows=true;renderer.reflectionProbeUsage=ReflectionProbeUsage.BlendProbesAndSkybox;
            foreach(var c in pair.Value)UnityEngine.Object.DestroyImmediate(c.mesh);
        }
        Bounds bounds=BoundsOf(root);
        if(normalizationJson==null){
            if(realLength>0&&bounds.size.x>bounds.size.z){content.localRotation=Quaternion.Euler(0,90,0);bounds=BoundsOf(root);}
            float factor=realLength>0?realLength/bounds.size.z:1;content.localScale=Vector3.one*factor;bounds=BoundsOf(root);content.position=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
        }else{
            // The reduced mesh must use exactly the original normalization, not its changed bounds.
            var original=JsonUtility.FromJson<ModelData>(File.ReadAllText(normalizationJson)).bounds;
            var b=new Bounds();b.SetMinMax(new Vector3(original.min[0],original.min[1],-original.max[2]),new Vector3(original.max[0],original.max[1],-original.min[2]));
            if(b.size.x>b.size.z){content.localRotation=Quaternion.Euler(0,90,0);b=new Bounds(content.localRotation*b.center,new Vector3(b.size.z,b.size.y,b.size.x));}
            float factor=realLength/b.size.z;content.localScale=Vector3.one*factor;content.localPosition=new Vector3(-b.center.x,-b.min.y,-b.center.z)*factor;
        }
        // Bake centimeter/SketchUp-unit normalization into vertices. Extremely tiny transforms
        // overflow half-precision inverse scale in mobile Standard shader normal calculations.
        var bake=content.localToWorldMatrix;var rotation=content.rotation;
        foreach(var filter in content.GetComponentsInChildren<MeshFilter>()){var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;for(int i=0;i<vertices.Length;i++){vertices[i]=bake.MultiplyPoint3x4(vertices[i]);normals[i]=(rotation*normals[i]).normalized;}mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);}
        content.localPosition=Vector3.zero;content.localRotation=Quaternion.identity;content.localScale=Vector3.one;
        bounds=BoundsOf(root);var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
        foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.isStatic=true;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+key+".prefab");UnityEngine.Object.DestroyImmediate(root);
        File.WriteAllText(folder+"/IMPORT.txt","Offline source: "+jsonPath+"\nBaked glTF node transforms. Flip Z/winding, flip V for Unity. Normalized vehicle length "+realLength+"m.\n"+DateTime.Now.ToString("s"));
        return prefab;
    }
    static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static Mesh SaveMesh(Mesh mesh,string path){mesh.name=Path.GetFileNameWithoutExtension(path);return ParkingLotMeshAsset.Save(mesh,path);}
    static Texture2D LoadTexture(string name,string src,string dst,bool normal=false,bool linear=false) {
        if(string.IsNullOrEmpty(name))return null;string source=Path.Combine(src,name);if(!File.Exists(source))return null;
        string path=dst+"/Textures/"+Path.GetFileName(name);if(!File.Exists(path))File.Copy(source,path);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=2048;importer.anisoLevel=8;importer.sRGBTexture=!linear&&!normal;importer.alphaIsTransparency=!normal&&!linear;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        if(normal)importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material MakeMaterial(MaterialData d,string src,string folder,int index) {
        string path=folder+"/Materials/"+index.ToString("D3")+"_"+Safe(d.name)+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(d.doubleSided&&d.alphaMode=="OPAQUE"?"ParkingLot/DoubleSidedPBR":"Standard");m.shaderKeywords=new string[0];
        var rgba=d.baseColor??new[]{1f,1f,1f,1f};var color=new Color(rgba[0],rgba[1],rgba[2],rgba.Length>3?rgba[3]:1);m.color=new Color(Mathf.LinearToGammaSpace(color.r),Mathf.LinearToGammaSpace(color.g),Mathf.LinearToGammaSpace(color.b),color.a);
        m.mainTexture=LoadTexture(d.baseColorTexture,src,folder);m.SetFloat("_Metallic",d.metallic);m.SetFloat("_Glossiness",Mathf.Clamp01(1-d.roughness));
        var normal=LoadTexture(d.normalTexture,src,folder,true);if(normal!=null){m.SetTexture("_BumpMap",normal);m.EnableKeyword("_NORMALMAP");}
        var ao=LoadTexture(d.occlusionTexture,src,folder,false,true);if(ao!=null){m.SetTexture("_OcclusionMap",ao);m.SetFloat("_OcclusionStrength",1);}
        // glTF packs roughness in G / metallic in B; Unity Standard expects metallic R / smoothness A.
        if(!string.IsNullOrEmpty(d.metallicRoughnessTexture)) {
            string source=Path.Combine(src,d.metallicRoughnessTexture);if(File.Exists(source)) {
                var input=new Texture2D(2,2);input.LoadImage(File.ReadAllBytes(source));var pixels=input.GetPixels();
                for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(pixels[i].b*d.metallic,0,0,1-pixels[i].g*d.roughness);
                var packed=new Texture2D(input.width,input.height,TextureFormat.RGBA32,false);packed.SetPixels(pixels);packed.Apply();string packedPath=folder+"/Textures/metallic_"+index+".png";File.WriteAllBytes(packedPath,packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(input);UnityEngine.Object.DestroyImmediate(packed);AssetDatabase.ImportAsset(packedPath);var imp=(TextureImporter)AssetImporter.GetAtPath(packedPath);imp.sRGBTexture=false;imp.SaveAndReimport();m.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));m.EnableKeyword("_METALLICGLOSSMAP");m.SetFloat("_GlossMapScale",1);
            }
        }
        m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.Zero);m.SetInt("_ZWrite",1);m.renderQueue=-1;m.SetOverrideTag("RenderType","");m.SetFloat("_Mode",0);
        if(d.alphaMode=="MASK"){m.SetFloat("_Mode",1);m.SetFloat("_Cutoff",d.alphaCutoff);m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=2450;}
        else if(d.alphaMode=="BLEND") {m.SetFloat("_Mode",3);m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.EnableKeyword("_ALPHAPREMULTIPLY_ON");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;}
        string lower=d.name.ToLowerInvariant();
        if(lower.Contains("glass")||lower.Contains("window")||lower.Contains("windshiel")||lower=="rear_wind"){m.SetFloat("_Glossiness",.94f);m.SetFloat("_Metallic",0);if(d.alphaMode=="BLEND"){m.shader=Shader.Find("ParkingLot/AutomotiveGlass");m.color=new Color(.15f,.21f,.23f,.55f);}}
        if(lower.Contains("tire")||lower.Contains("tyre")||lower.Contains("rubber")){m.SetFloat("_Glossiness",.2f);m.SetFloat("_Metallic",0);if(m.mainTexture==null)m.color=new Color(.055f,.06f,.064f,1);}
        if(lower.Contains("chrome")||lower.Contains("mirror")){m.SetFloat("_Metallic",1);m.SetFloat("_Glossiness",.93f);}
        else if(lower=="metal"||lower=="metal_dark"){m.SetFloat("_Metallic",.8f);m.SetFloat("_Glossiness",.7f);}
        if(lower=="radiator"||lower=="radiator__1"||lower=="exhust"||lower=="head_lig2"||lower=="lamps_fro"){m.SetFloat("_Metallic",.9f);m.SetFloat("_Glossiness",.8f);m.color=new Color(.65f,.67f,.68f,1);}
        if(lower=="disc"||lower.StartsWith("volk_raing_wheel")||lower=="wheel1"||lower.StartsWith("cylinde")){m.SetFloat("_Metallic",.9f);m.SetFloat("_Glossiness",.62f);m.color=new Color(.56f,.58f,.60f,1);}
        if(lower=="paint"||lower=="varnish"){m.SetFloat("_Metallic",.42f);m.SetFloat("_Glossiness",.86f);}
        if(lower=="brakes"){m.color=new Color(.29f,.30f,.29f,1);m.SetFloat("_Metallic",.65f);m.SetFloat("_Glossiness",.3f);}
        if(folder.EndsWith("tree-small-far")&&lower.Contains("leaves"))m.color=new Color(.65f,.76f,.55f,1);
        if(folder.EndsWith("generico-caminhao")&&lower=="lct3000_bodymat"){
            // The source's noisy packed metal mask reads as television static at map distance.
            m.DisableKeyword("_METALLICGLOSSMAP");m.SetTexture("_MetallicGlossMap",null);m.SetFloat("_Metallic",.22f);m.SetFloat("_Glossiness",.42f);m.SetFloat("_OcclusionStrength",.2f);m.color=new Color(.69f,.71f,.71f,1);
        }
        // This source omits photographic tail-lens maps; avoid white plastic rear lamps.
        if(folder.EndsWith("hyundai-azera")&&lower=="tail_ligh"){m.color=new Color(.42f,.018f,.025f,1);m.SetFloat("_Metallic",.08f);m.SetFloat("_Glossiness",.88f);}
        if(folder.EndsWith("hyundai-azera")&&(lower=="trunk_1"||lower=="trunk_2")){m.color=new Color(.57f,.59f,.60f,1);m.SetFloat("_Metallic",1);m.SetFloat("_Glossiness",.84f);}
        if(lower.Contains("black")&&m.mainTexture==null)m.color=new Color(.045f,.05f,.054f,1);
        if(folder.EndsWith("tree-small-real")&&lower.Contains("leaves")){var colorTexture=m.mainTexture;var normalTexture=m.GetTexture("_BumpMap");m.shader=Shader.Find("ParkingLot/RealismFoliage");m.SetTexture("_MainTex",colorTexture);m.SetTexture("_BumpMap",normalTexture);m.SetTexture("_AlphaTex",Texture2D.whiteTexture);m.SetFloat("_Cutoff",.1f);}
        if(d.emissiveFactor!=null&&d.emissiveFactor.Length>=3){var e=new Color(d.emissiveFactor[0],d.emissiveFactor[1],d.emissiveFactor[2]);if(e.maxColorComponent>0){m.SetColor("_EmissionColor",e);m.SetTexture("_EmissionMap",LoadTexture(d.emissiveTexture,src,folder));m.EnableKeyword("_EMISSION");}}
        m.enableInstancing=true;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
}
}
