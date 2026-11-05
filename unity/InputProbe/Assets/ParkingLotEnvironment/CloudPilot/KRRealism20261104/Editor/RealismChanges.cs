using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.Rendering;
namespace ParkingLotEnvironment.CloudPilot {
public static class RealismChanges {
 const string W="Assets/ParkingLotEnvironment/CloudPilot/KRRealism20261104";
 static Dictionary<Material,Material> copies=new Dictionary<Material,Material>();
 public static void Apply(GameObject root,string output){
  var notes=new List<string>();var cars=root.transform.Find("EXISTING_VEHICLES_UNAPPROVED");
  foreach(Transform car in cars){if(!car.name.ToLowerInvariant().Contains("i30"))continue;var rr=car.GetComponentsInChildren<MeshRenderer>(true);var outer=rr.FirstOrDefault(r=>r.name.ToLowerInvariant()=="paint");if(!outer)continue;var of=outer.GetComponent<MeshFilter>();if(!of||!of.sharedMesh||!of.sharedMesh.isReadable)continue;
   var cells=new HashSet<Vector3Int>(of.sharedMesh.vertices.Select(v=>Key(of.transform.TransformPoint(v))));
   foreach(var r in rr.Where(r=>r.name.ToLowerInvariant()=="black1")){var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh||!f.sharedMesh.isReadable||f.sharedMesh.subMeshCount!=1)continue;var src=f.sharedMesh;var verts=src.vertices;var tri=src.triangles;var close=verts.Select(v=>Near(cells,Key(f.transform.TransformPoint(v)))).ToArray();var keep=new List<int>();int removed=0;for(int i=0;i<tri.Length;i+=3){if(close[tri[i]]&&close[tri[i+1]]&&close[tri[i+2]]){removed++;continue;}keep.Add(tri[i]);keep.Add(tri[i+1]);keep.Add(tri[i+2]);}if(removed>0){var copy=UnityEngine.Object.Instantiate(src);copy.name="Deduplicated_"+car.name;copy.triangles=keep.ToArray();copy.RecalculateBounds();AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(W+"/"+copy.name+".asset"));f.sharedMesh=copy;EditorUtility.SetDirty(f);}notes.Add(car.name+" overlapping black1 triangles removed="+removed+" / "+tri.Length/3);}
  }
  var road=AssetDatabase.LoadAssetAtPath<Shader>(W+"/RoadPBR.shader");var glass=AssetDatabase.LoadAssetAtPath<Shader>(W+"/GlassPBR.shader");
  foreach(var r in root.GetComponentsInChildren<Renderer>(true)){var mats=r.sharedMaterials;bool changed=false;bool vehicle=r.transform.IsChildOf(cars);for(int j=0;j<mats.Length;j++){var m=mats[j];if(!m||!m.shader)continue;var sn=m.shader.name;var rn=r.name.ToLowerInvariant();string mode=null;
   if(sn=="ParkingLot/AntigravityAsphalt")mode="road";else if(sn=="ParkingLot/AntigravityPreLitBuilding")mode="building";else if(vehicle&&sn.Contains("AntigravityGlass"))mode="glass";else if(vehicle&&(sn.Contains("AntigravityCarPaint")||sn.Contains("LinearCarPaint")||sn.Contains("WholeLookPaint")))mode="paint";
   if(vehicle&&r.transform.parent&&r.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("i30"))&&(rn=="black1"||rn=="black0"||rn=="color_c01"))mode="silver";
   if(mode==null)continue;Material c;if(!copies.TryGetValue(m,out c)||mode=="silver") {c=new Material(m){name="Realism_"+mode+"_"+m.name};c.shader=mode=="road"?road:mode=="glass"?glass:Shader.Find("Standard");
    if(mode=="road"){c.SetColor("_Color",new Color(.8f,.79f,.77f));}
    else if(mode=="building"){c.SetColor("_Color",new Color(.86f,.86f,.86f));c.SetFloat("_Metallic",0);c.SetFloat("_Glossiness",.23f);c.SetFloat("_GlossMapScale",.3f);c.SetFloat("_BumpScale",.25f);if(c.GetTexture("_BumpMap"))c.EnableKeyword("_NORMALMAP");if(c.GetTexture("_MetallicGlossMap"))c.EnableKeyword("_METALLICGLOSSMAP");}
    else if(mode=="glass"){c.SetColor("_Color",new Color(.025f,.035f,.04f,.82f));c.SetFloat("_Glossiness",.94f);}
    else{if(mode=="silver")c.SetColor("_Color",new Color(.64f,.66f,.68f));c.SetFloat("_Metallic",.38f);c.SetFloat("_Glossiness",.78f);c.DisableKeyword("_METALLICGLOSSMAP");c.SetTexture("_MetallicGlossMap",null);}
    c.DisableKeyword("_EMISSION");AssetDatabase.CreateAsset(c,AssetDatabase.GenerateUniqueAssetPath(W+"/"+c.name+".mat"));if(mode!="silver")copies[m]=c;
   }mats[j]=c;changed=true;
  }if(changed){r.sharedMaterials=mats;EditorUtility.SetDirty(r);}}
  // Roof edges contribute a readable silhouette in the aerial camera without touching imported building meshes.
  var holder=root.transform.Find("AG_Photorealistic3D_Assets");var roofMat=new Material(Shader.Find("Standard")){name="RoofConcrete",color=new Color(.48f,.47f,.44f)};roofMat.SetFloat("_Glossiness",.13f);AssetDatabase.CreateAsset(roofMat,W+"/RoofConcrete.mat");
  if(holder)foreach(Transform b in holder.Cast<Transform>().Where(t=>t.name.Contains("Building")||t.name.Contains("Villa")||t.name.Contains("Cafe")).ToArray()){var rr=b.GetComponentsInChildren<Renderer>();if(rr.Length==0)continue;var box=rr[0].bounds;foreach(var r in rr.Skip(1))box.Encapsulate(r.bounds);if(box.size.y<5)continue;var p=new GameObject("RoofParapet_"+b.name).transform;p.SetParent(root.transform,false);for(int k=0;k<4;k++){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Parapet";g.transform.SetParent(p);g.transform.position=new Vector3(box.center.x+(k<2?(k==0?-1:1)*(box.extents.x-.1f):0),box.max.y+.18f,box.center.z+(k>=2?(k==2?-1:1)*(box.extents.z-.1f):0));g.transform.localScale=k<2?new Vector3(.18f,.36f,box.size.z):new Vector3(box.size.x,.36f,.18f);UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=roofMat;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);}}
  var camera=root.GetComponentInChildren<Camera>();camera.allowHDR=true;foreach(var component in camera.GetComponents<MonoBehaviour>()){if(component.GetType().Name=="ParkingLotIntegratedPhotoFinish")continue;if(component.GetType().GetMethod("OnRenderImage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)!=null){component.enabled=false;notes.Add("Disabled duplicate post effect "+component.GetType().Name);}}
  var fx=camera.GetComponent<ParkingLotEnvironment.CloudPilot.Integrated.ParkingLotIntegratedPhotoFinish>();fx.shader=AssetDatabase.LoadAssetAtPath<Shader>(W+"/PhotoFinish.shader");fx.contactStrength=.7f;
  var sun=root.GetComponentsInChildren<Light>().First(l=>l.type==LightType.Directional);sun.intensity=1.05f;sun.color=new Color(1,.98f,.95f);sun.shadows=LightShadows.Soft;RenderSettings.ambientIntensity=1.0f;
  notes.Add("Material copies="+copies.Count+"; HDR camera; one post-processing stack; GI capable building and road shaders.");File.WriteAllLines(output+"/changes.txt",notes);
 }
 static Vector3Int Key(Vector3 v){return new Vector3Int(Mathf.RoundToInt(v.x*1000),Mathf.RoundToInt(v.y*1000),Mathf.RoundToInt(v.z*1000));}
 static bool Near(HashSet<Vector3Int> cells,Vector3Int k){for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(cells.Contains(k+new Vector3Int(x,y,z)))return true;return false;}
}}
