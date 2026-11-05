using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParkingLotEnvironment.Editor {
// Exact material-equivalence batching on scene instances. Source vehicle prefabs stay intact.
public static class ParkingLotVehicleBatching {
    const string Folder="Assets/ParkingLotEnvironment/Meshes/OptimizedVehicles";
    static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
    static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
    public static int Before{get;private set;}
    public static int After{get;private set;}
    public static void Reset(){meshes.Clear();materials.Clear();Before=After=0;Directory.CreateDirectory(Folder);}
    public static void Optimize(GameObject car,string key){
        if(car.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Only the map scene may be optimized.");
        var model=car.transform.Find("Model_geometry");if(model==null)return;
        if(PrefabUtility.IsPartOfPrefabInstance(car))PrefabUtility.UnpackPrefabInstance(car,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
        var groups=new Dictionary<string,List<MeshRenderer>>();
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>()){
            var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null||filter.sharedMesh.subMeshCount!=1||renderer.sharedMaterials.Length!=1)continue;
            if(renderer.transform.parent!=model||renderer.transform.localPosition!=Vector3.zero||renderer.transform.localRotation!=Quaternion.identity||renderer.transform.localScale!=Vector3.one)throw new InvalidOperationException("Expected normalized direct-child model geometry.");
            string fingerprint=Fingerprint(renderer.sharedMaterial)+"|"+renderer.shadowCastingMode+"|"+renderer.receiveShadows+"|"+renderer.reflectionProbeUsage;
            if(!groups.TryGetValue(fingerprint,out var group)){group=new List<MeshRenderer>();groups.Add(fingerprint,group);}group.Add(renderer);Before++;
            if(materials.TryGetValue(fingerprint,out var canonical))renderer.sharedMaterial=canonical;else materials[fingerprint]=renderer.sharedMaterial;
        }
        foreach(var group in groups.Values){
            After++;if(group.Count<2)continue;
            var first=group[0];var parts=group.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
            string signature=key+"|"+string.Join("|",parts.Select(m=>AssetDatabase.GetAssetPath(m)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(m))));
            string id=Hash(signature);
            if(!meshes.TryGetValue(id,out var mesh)){
                var combined=new Mesh{name=key+"_"+id,indexFormat=IndexFormat.UInt32};
                combined.CombineMeshes(parts.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),true,false);combined.RecalculateBounds();
                string path=Folder+"/"+combined.name+".asset";mesh=ParkingLotMeshAsset.Save(combined,path);meshes[id]=mesh;
            }
            var go=new GameObject("Combined_"+first.sharedMaterial.name);go.transform.SetParent(model,false);go.isStatic=true;go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var output=go.AddComponent<MeshRenderer>();output.sharedMaterial=first.sharedMaterial;output.shadowCastingMode=first.shadowCastingMode;output.receiveShadows=first.receiveShadows;output.reflectionProbeUsage=first.reflectionProbeUsage;output.lightProbeUsage=first.lightProbeUsage;
            foreach(var old in group)UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }
    static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").Substring(0,16);}
    static string Float(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
    static void Vector(StringBuilder b,Vector4 v){b.Append(Float(v.x)).Append(',').Append(Float(v.y)).Append(',').Append(Float(v.z)).Append(',').Append(Float(v.w));}
    static string Fingerprint(Material m){
        if(m==null)throw new InvalidOperationException("Missing vehicle material.");
        var b=new StringBuilder();b.Append(m.shader.name).Append('|').Append(m.renderQueue).Append('|').Append(m.doubleSidedGI).Append('|').Append(string.Join(",",m.shaderKeywords.OrderBy(s=>s)));
        for(int i=0;i<ShaderUtil.GetPropertyCount(m.shader);i++){
            string p=ShaderUtil.GetPropertyName(m.shader,i);b.Append('|').Append(p).Append('=');
            switch(ShaderUtil.GetPropertyType(m.shader,i)){
                case ShaderUtil.ShaderPropertyType.Color:Vector(b,m.GetColor(p));break;
                case ShaderUtil.ShaderPropertyType.Vector:Vector(b,m.GetVector(p));break;
                case ShaderUtil.ShaderPropertyType.TexEnv:var t=m.GetTexture(p);b.Append(t?AssetDatabase.GetAssetPath(t):"null");Vector(b,m.GetTextureScale(p));Vector(b,m.GetTextureOffset(p));break;
                default:b.Append(Float(m.GetFloat(p)));break;
            }
        }
        return b.ToString();
    }
}
}
