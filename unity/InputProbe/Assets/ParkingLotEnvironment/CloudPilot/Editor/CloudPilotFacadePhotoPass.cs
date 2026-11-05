using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// One explicit facade experiment. Generated surface texture, actual recessed 3D facade,
// existing closed building volume; never a full-screen image or camera-facing billboard.
public static class CloudPilotFacadePhotoPass {
 const string K="Assets/ParkingLotEnvironment/CloudPilot";
 const string W=K+"/FacadePhotoPilot";
 const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string Expected="96af4ac73b1bb5aab33b34dda127a12a1fd30e4942d810d1853de860033f07a7";
 static string output;
 static double started;
 static GameObject pilot;
 static readonly List<string> notes=new List<string>();
 static readonly List<Vector3> verts=new List<Vector3>();
 static readonly List<Vector2> uv=new List<Vector2>();
 static readonly List<int> tri=new List<int>();
 const float width=9f,height=12.8f,front=-4.12f,back=-4.015f;
 static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");if(QualitySettings.shadows!=ShadowQuality.Disable||QualitySettings.antiAliasing!=0)throw new Exception("Unexpected project quality");}
 static void Add(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int i=verts.Count;foreach(var p in new[]{a,b,c,d}){verts.Add(p);uv.Add(new Vector2(p.x/width+.5f,p.y/height));}tri.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
 static void Plane(float x0,float x1,float y0,float y1,float z){Add(new Vector3(x0,y0,z),new Vector3(x0,y1,z),new Vector3(x1,y1,z),new Vector3(x1,y0,z));}
 static Rect Pixels(float x0,float x1,float yTop,float yBottom){return Rect.MinMaxRect(x0/1052f*width-width*.5f,(1-yBottom/1495f)*height,x1/1052f*width-width*.5f,(1-yTop/1495f)*height);}
 static void MeshObject(string name,Transform root,Material material){
  var m=new Mesh{name=name};m.SetVertices(verts);m.SetUVs(0,uv);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();
  UnwrapParam param;UnwrapParam.SetDefaults(out param);param.packMargin=.03f;if(!Unwrapping.GenerateSecondaryUVSet(m,param))throw new Exception("Facade unwrap failed");
  AssetDatabase.CreateAsset(m,W+"/"+name+".asset");var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=m;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=1;r.shadowCastingMode=ShadowCastingMode.TwoSided;
  GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.BatchingStatic);
  notes.Add(name+" vertices="+m.vertexCount+" triangles="+(m.triangles.Length/3));verts.Clear();uv.Clear();tri.Clear();
 }
 static void Disable(Transform t){foreach(var r in t.GetComponentsInChildren<Renderer>(true)){r.enabled=false;r.shadowCastingMode=ShadowCastingMode.Off;GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);}t.gameObject.SetActive(false);}
 public static void ApplyAndBake(){try{
  Check();if(Hash(S)!=Expected)throw new Exception("Scene changed since accepted sun checkpoint");
  var args=Environment.GetCommandLineArgs();int oi=Array.IndexOf(args,"-parkingFacadeOutput");if(oi<0)throw new Exception("Missing output");output=Path.GetFullPath(args[oi+1]);Directory.CreateDirectory(output);
  if(File.Exists(W+"/FacadeWall.asset"))throw new Exception("Single-use facade already generated");
  EditorSceneManager.OpenScene(S);var roots=SceneManager.GetActiveScene().GetRootGameObjects();pilot=roots.Single(g=>g.name=="ParkingLot_CornerPilot_Root");if(roots.Single(g=>g.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
  var building=pilot.transform.Find("DaylightPass_Context/ContextBuilding_4_27");if(!building)throw new Exception("Hero building missing");
  var structure=building.Find("SolidStructure");var bounds=structure.GetComponent<Renderer>().bounds;if(Mathf.Abs(bounds.size.x-width)>.01f||Mathf.Abs(bounds.size.y-height)>.01f)throw new Exception("Hero building changed size");
  string texture=W+"/KoreanIvoryFacade_Generated.png";var ti=(TextureImporter)AssetImporter.GetAtPath(texture);if(ti==null)throw new Exception("Generated texture missing");ti.sRGBTexture=true;ti.textureType=TextureImporterType.Default;ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.anisoLevel=4;ti.wrapMode=TextureWrapMode.Clamp;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
  var wall=new Material(Shader.Find("Standard")){name="Generated_KoreanIvoryFacade",mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texture),color=Color.white};wall.SetFloat("_Glossiness",.1f);AssetDatabase.CreateAsset(wall,W+"/FacadeWall.mat");
  var glass=new Material(wall){name="Generated_FacadeWindowSurface"};glass.SetFloat("_Glossiness",.28f);AssetDatabase.CreateAsset(glass,W+"/FacadeWindow.mat");
  string[] obsolete={"WindowReveal","RecessedGlazing","Mullion","Sill","Street_RollerBlind","Street_BlindSlat","Street_ShopFrame","Street_ShopGlazing","Street_ShopMullion","Street_ShopTransom","Street_FloorJoint","Street_ShopFascia"};
  foreach(Transform t in building){if(obsolete.Contains(t.name))Disable(t);}
  // Keep actual side walls/windows, roof, pipe and AC units. A separate awning remains volumetric.
  var awning=building.Find("GroundFloorAwning");if(awning){var p=awning.localPosition;p.y=3.62f;awning.localPosition=p;}
  var text=building.Find("Street_ShopText");if(text){var p=text.localPosition;p.y=3.22f;text.localPosition=p;}
  var root=new GameObject("FacadePhoto_Recessed3D").transform;root.SetParent(building,false);
  var holes=new List<Rect>();foreach(var row in new[]{new Vector2(100,324),new Vector2(445,669),new Vector2(782,1008)})foreach(var col in new[]{new Vector2(84,288),new Vector2(426,632),new Vector2(767,973)})holes.Add(Pixels(col.x,col.y,row.x,row.y));
  foreach(var col in new[]{new Vector2(62,329),new Vector2(409,659),new Vector2(742,1008)})holes.Add(Pixels(col.x,col.y,1168,1490));
  var xs=holes.SelectMany(r=>new[]{r.xMin,r.xMax}).Concat(new[]{-width*.5f,width*.5f}).Distinct().OrderBy(x=>x).ToArray();
  var ys=holes.SelectMany(r=>new[]{r.yMin,r.yMax}).Concat(new[]{0f,height}).Distinct().OrderBy(y=>y).ToArray();
  for(int i=0;i<xs.Length-1;i++)for(int j=0;j<ys.Length-1;j++){var mid=new Vector2((xs[i]+xs[i+1])*.5f,(ys[j]+ys[j+1])*.5f);if(!holes.Any(r=>r.Contains(mid)))Plane(xs[i],xs[i+1],ys[j],ys[j+1],front);}
  foreach(var r in holes){
   Add(new Vector3(r.xMin,r.yMin,front),new Vector3(r.xMin,r.yMax,front),new Vector3(r.xMin,r.yMax,back),new Vector3(r.xMin,r.yMin,back));
   Add(new Vector3(r.xMax,r.yMin,back),new Vector3(r.xMax,r.yMax,back),new Vector3(r.xMax,r.yMax,front),new Vector3(r.xMax,r.yMin,front));
   Add(new Vector3(r.xMin,r.yMax,front),new Vector3(r.xMax,r.yMax,front),new Vector3(r.xMax,r.yMax,back),new Vector3(r.xMin,r.yMax,back));
   Add(new Vector3(r.xMin,r.yMin,back),new Vector3(r.xMax,r.yMin,back),new Vector3(r.xMax,r.yMin,front),new Vector3(r.xMin,r.yMin,front));
  }
  // Return edges close the shallow front extension against the existing solid building.
  Add(new Vector3(-4.5f,0,0-4f),new Vector3(-4.5f,12.8f,-4f),new Vector3(-4.5f,12.8f,front),new Vector3(-4.5f,0,front));
  Add(new Vector3(4.5f,0,front),new Vector3(4.5f,12.8f,front),new Vector3(4.5f,12.8f,-4f),new Vector3(4.5f,0,-4f));
  MeshObject("FacadeWall",root,wall);foreach(var r in holes)Plane(r.xMin,r.xMax,r.yMin,r.yMax,back);MeshObject("FacadeWindows",root,glass);
  notes.Add("One 9m x 12.8m closed building,12 actual recessed openings depth0.105m. Texture generated by built-in image_gen,not a captured real place. Window imagery contains static photographic-style reflections; not reconstructed glass/interiors. Camera and project quality unchanged.");
  var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="FacadePhoto_BakedLighting";settings.autoGenerate=false;AssetDatabase.CreateAsset(settings,W+"/FacadePhoto.lighting");Lightmapping.lightingSettings=settings;
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),W+"/FacadeBakeWork.unity");
  started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;if(!Lightmapping.BakeAsync())throw new Exception("Bake rejected");
 }catch(Exception e){Fail(e);}}
 public static void BalanceAndBake(){try{
  Check();if(Hash(S)!="dfc61725cb00d6fdec4940cd526477c3a5a8774d538346202a68c6d789f7a007")throw new Exception("Candidate changed before one exposure correction");
  var args=Environment.GetCommandLineArgs();int oi=Array.IndexOf(args,"-parkingFacadeOutput");if(oi<0)throw new Exception("Missing output");output=Path.GetFullPath(args[oi+1]);
  EditorSceneManager.OpenScene(S);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="ParkingLot_CornerPilot_Root");
  foreach(var name in new[]{"FacadeWall","FacadeWindow"}){var m=AssetDatabase.LoadAssetAtPath<Material>(W+"/"+name+".mat");if(m==null)throw new Exception("Candidate material missing");m.color=new Color(.5f,.5f,.5f,1);EditorUtility.SetDirty(m);}
  notes.Add("One broad material exposure correction: generated photo facade albedo tint0.5, wall and windows, to retain surface detail under the accepted baked sun. Same geometry, lighting, camera, and all other materials.");
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),W+"/FacadeBakeWork.unity");
  started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;if(!Lightmapping.BakeAsync())throw new Exception("Bake rejected");
 }catch(Exception e){Fail(e);}}
 public static void UseAcceptedReflection(){try{
  Check();if(Hash(S)!="dfc61725cb00d6fdec4940cd526477c3a5a8774d538346202a68c6d789f7a007")throw new Exception("Candidate changed before reflection comparison");
  var args=Environment.GetCommandLineArgs();int oi=Array.IndexOf(args,"-parkingFacadeOutput");if(oi<0)throw new Exception("Missing output");output=Path.GetFullPath(args[oi+1]);
  EditorSceneManager.OpenScene(S);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="ParkingLot_CornerPilot_Root");
  var cube=AssetDatabase.LoadAssetAtPath<Texture>(K+"/SunContrastLighting/DirectionalReflection.exr");if(!cube)throw new Exception("Accepted reflection missing");
  foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>()){p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=cube;EditorUtility.SetDirty(p);}
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);
  File.AppendAllText(output+"/facade-photo-report.txt","\nReuse previous accepted SunContrastLighting reflection for whole-frame comparison. New facade lightmaps unchanged. SHA256="+Hash(S)+"\n");EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 static void Tick(){try{
  Check();double elapsed=EditorApplication.timeSinceStartup-started;if(elapsed>480)throw new Exception("Bake timeout");if(elapsed<5||Lightmapping.isRunning)return;EditorApplication.update-=Tick;
  if(LightmapSettings.lightmaps.Length==0)throw new Exception("No lightmaps");
  foreach(var r in pilot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="FacadeWall"||r.name=="FacadeWindows"))if(r.lightmapIndex<0||r.lightmapIndex>=LightmapSettings.lightmaps.Length)throw new Exception("Facade missing baked receiver");
  foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>()){string path=W+"/FacadeReflection.exr";if(!Lightmapping.BakeReflectionProbe(p,path))throw new Exception("Reflection bake failed");AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);EditorUtility.SetDirty(p);}
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);AssetDatabase.SaveAssets();
  notes.Add("Bake seconds="+elapsed+" SHA256="+Hash(S));File.WriteAllText(output+"/facade-photo-report.txt",string.Join("\n",notes));EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 static void Fail(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
}
}
