using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
namespace ParkingLotEnvironment.CloudPilot {
public static class CloudPilotPhotoStreetPass {
 const string K="Assets/ParkingLotEnvironment/CloudPilot";
 const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string W=K+"/PhotoStreetPilot";
 const string Expected="fd6328171e6fa31bbb157c852d354e5d82b3fabf118c617e9f100e7a17f42860";
 static readonly List<Vector3> vertices=new List<Vector3>();
 static readonly List<Vector2> uv=new List<Vector2>();
 static readonly List<int> triangles=new List<int>();
 static readonly List<string> notes=new List<string>();
 static float facadeHeight;
 static string output;
 static double started;
 static GameObject pilot;
 static string Hash(string path){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");if(QualitySettings.shadows!=ShadowQuality.Disable||QualitySettings.antiAliasing!=0)throw new Exception("Unexpected project quality");}
 static void Persist(UnityEngine.Object o){EditorUtility.SetDirty(o);if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o);}
 static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int n=vertices.Count;foreach(var p in new[]{a,b,c,d}){vertices.Add(p);uv.Add(new Vector2(p.x/10f+.5f,p.y/facadeHeight));}triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
 static void Plane(float x0,float x1,float y0,float y1,float z){Quad(new Vector3(x0,y0,z),new Vector3(x1,y0,z),new Vector3(x1,y1,z),new Vector3(x0,y1,z));}
 static Rect PixelRect(float x0,float x1,float yTop,float yBottom,float pw,float ph){return Rect.MinMaxRect(x0/pw*10-5,(1-yBottom/ph)*facadeHeight,x1/pw*10-5,(1-yTop/ph)*facadeHeight);}
 static void AddMesh(string name,Transform root,Material material){
  var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
  UnwrapParam param;UnwrapParam.SetDefaults(out param);param.packMargin=.03f;if(!Unwrapping.GenerateSecondaryUVSet(mesh,param))throw new Exception("New facade UV failed");
  AssetDatabase.CreateAsset(mesh,W+"/"+name+".asset");var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.8f;r.shadowCastingMode=ShadowCastingMode.TwoSided;
  GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.BatchingStatic);notes.Add(name+" triangles="+mesh.triangles.Length/3);vertices.Clear();uv.Clear();triangles.Clear();
 }
 static Material MakeMaterial(string textureName,string name,float value,float gloss){
  string path=W+"/"+textureName+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new Exception("Texture missing");importer.sRGBTexture=true;importer.textureType=TextureImporterType.Default;importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.anisoLevel=4;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
  var mat=new Material(Shader.Find("Standard")){name=name,mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path),color=new Color(value,value,value,1)};mat.SetFloat("_Glossiness",gloss);AssetDatabase.CreateAsset(mat,W+"/"+name+".mat");return mat;
 }
 static void KeepShellWithoutOldFront(Transform part,int index,bool trim){
  foreach(var filter in part.GetComponentsInChildren<MeshFilter>()){
   var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);mesh.name="StreetPhoto_"+index+(trim?"_RoofTrim":"_ClosedSideBack");var points=mesh.vertices;var kept=new List<int>();var originalTriangles=mesh.triangles;
   for(int n=0;n<originalTriangles.Length;n+=3){int a=originalTriangles[n],b=originalTriangles[n+1],c=originalTriangles[n+2];bool keep=trim?Mathf.Min(points[a].y,Mathf.Min(points[b].y,points[c].y))>=facadeHeight-.12f:!(points[a].z> -5.3f&&points[b].z> -5.3f&&points[c].z> -5.3f);if(keep)kept.AddRange(new[]{a,b,c});}
   if(kept.Count==0||kept.Count==originalTriangles.Length)throw new Exception("Unexpected original shell topology: "+part.name);
   mesh.triangles=kept.ToArray();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,W+"/"+mesh.name+".asset");filter.sharedMesh=mesh;Persist(filter);
   // Existing collision mesh is retained. The old prefab/source mesh is never edited.
   notes.Add(part.name+" cloned triangles "+originalTriangles.Length/3+" -> "+kept.Count/3);
  }
 }
 static void DisableOldWindows(Transform part){foreach(var r in part.GetComponentsInChildren<Renderer>()){r.enabled=false;r.shadowCastingMode=ShadowCastingMode.Off;GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);Persist(r);Persist(r.gameObject);}}
 static void Building(int index,Material wall,Material window,bool plaster){
  facadeHeight=plaster?12.8f:9.6f;var shell=pilot.transform.Find("Building_"+index+"_BrickShell");var trim=pilot.transform.Find("Building_"+index+"_ConcreteTrim");if(!shell||!trim)throw new Exception("Original building missing");
  var bounds=shell.GetComponentInChildren<Renderer>().bounds;if(Mathf.Abs(bounds.size.x-3)>.01f||Mathf.Abs(bounds.size.z-10)>.01f||Mathf.Abs(bounds.size.y-facadeHeight)>.01f)throw new Exception("Original footprint changed");
  KeepShellWithoutOldFront(shell,index,false);KeepShellWithoutOldFront(trim,index,true);DisableOldWindows(pilot.transform.Find("Building_"+index+"_WindowFrames"));DisableOldWindows(pilot.transform.Find("Building_"+index+"_WindowGlass"));
  var root=new GameObject("PhotoStreet_Building_"+index).transform;root.SetParent(pilot.transform,false);root.SetPositionAndRotation(shell.position,shell.rotation);
  var holes=new List<Rect>();
  if(plaster){
   foreach(var row in new[]{new Vector2(92,301),new Vector2(416,631),new Vector2(746,959)})foreach(var col in new[]{new Vector2(49,255),new Vector2(323,526),new Vector2(589,792),new Vector2(860,1062)})holes.Add(PixelRect(col.x,col.y,row.x,row.y,1108,1419));
   holes.Add(PixelRect(25,533,1149,1416,1108,1419));holes.Add(PixelRect(565,1082,1149,1416,1108,1419));
  }else{
   foreach(var row in new[]{new Vector2(123,352),new Vector2(480,710)})foreach(var col in new[]{new Vector2(57,300),new Vector2(366,610),new Vector2(674,915),new Vector2(983,1227)})holes.Add(PixelRect(col.x,col.y,row.x,row.y,1280,1229));
   holes.Add(PixelRect(40,615,875,1225,1280,1229));holes.Add(PixelRect(663,1249,875,1225,1280,1229));
  }
  const float front=-4.96f,back=-5.16f;
  var xs=holes.SelectMany(r=>new[]{r.xMin,r.xMax}).Concat(new[]{-5f,5f}).Distinct().OrderBy(x=>x).ToArray();var ys=holes.SelectMany(r=>new[]{r.yMin,r.yMax}).Concat(new[]{0f,facadeHeight}).Distinct().OrderBy(y=>y).ToArray();
  for(int x=0;x<xs.Length-1;x++)for(int y=0;y<ys.Length-1;y++){var mid=new Vector2((xs[x]+xs[x+1])*.5f,(ys[y]+ys[y+1])*.5f);if(!holes.Any(r=>r.Contains(mid)))Plane(xs[x],xs[x+1],ys[y],ys[y+1],front);}
  foreach(var r in holes){
   Quad(new Vector3(r.xMin,r.yMin,back),new Vector3(r.xMin,r.yMax,back),new Vector3(r.xMin,r.yMax,front),new Vector3(r.xMin,r.yMin,front));
   Quad(new Vector3(r.xMax,r.yMin,front),new Vector3(r.xMax,r.yMax,front),new Vector3(r.xMax,r.yMax,back),new Vector3(r.xMax,r.yMin,back));
   Quad(new Vector3(r.xMin,r.yMax,back),new Vector3(r.xMax,r.yMax,back),new Vector3(r.xMax,r.yMax,front),new Vector3(r.xMin,r.yMax,front));
   Quad(new Vector3(r.xMin,r.yMin,front),new Vector3(r.xMax,r.yMin,front),new Vector3(r.xMax,r.yMin,back),new Vector3(r.xMin,r.yMin,back));
  }
  Quad(new Vector3(-5,0,front),new Vector3(-5,facadeHeight,front),new Vector3(-5,facadeHeight,-5),new Vector3(-5,0,-5));
  Quad(new Vector3(5,0,-5),new Vector3(5,facadeHeight,-5),new Vector3(5,facadeHeight,front),new Vector3(5,0,front));
  AddMesh("StreetPhoto_"+index+"_Front",root,wall);foreach(var r in holes)Plane(r.xMin,r.xMax,r.yMin,r.yMax,back);AddMesh("StreetPhoto_"+index+"_Windows",root,window);
  notes.Add("Building"+index+" original position/rotation/10m width/"+facadeHeight+"m height/3m depth preserved. Openings="+holes.Count+" recessDepth0.2m. Source closed back and side walls retained.");
 }
 public static void ApplyAndBake(){try{
  Check();if(Hash(S)!=Expected)throw new Exception("Scene no longer matches accepted facade checkpoint");if(File.Exists(W+"/StreetPhoto_0_Front.asset"))throw new Exception("Single-use photo street pass already exists");
  var args=Environment.GetCommandLineArgs();int oi=Array.IndexOf(args,"-parkingPhotoStreetOutput");if(oi<0)throw new Exception("Missing output");output=Path.GetFullPath(args[oi+1]);Directory.CreateDirectory(output);
  EditorSceneManager.OpenScene(S);var roots=SceneManager.GetActiveScene().GetRootGameObjects();pilot=roots.Single(x=>x.name=="ParkingLot_CornerPilot_Root");if(roots.Single(x=>x.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
  var brick=MakeMaterial("KoreanBrickFacade_Generated","PhotoBrick",.75f,.1f);var plaster=MakeMaterial("KoreanPlasterFacade_Generated","PhotoPlaster",.6f,.1f);
  var brickGlass=new Material(brick){name="PhotoBrickWindows"};brickGlass.SetFloat("_Glossiness",.22f);AssetDatabase.CreateAsset(brickGlass,W+"/PhotoBrickWindows.mat");var plasterGlass=new Material(plaster){name="PhotoPlasterWindows"};plasterGlass.SetFloat("_Glossiness",.22f);AssetDatabase.CreateAsset(plasterGlass,W+"/PhotoPlasterWindows.mat");
  Building(0,brick,brickGlass,false);Building(1,plaster,plasterGlass,true);Building(2,brick,brickGlass,false);
  var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="PhotoStreet_Lighting";settings.autoGenerate=false;AssetDatabase.CreateAsset(settings,W+"/PhotoStreet.lighting");Lightmapping.lightingSettings=settings;
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),W+"/PhotoStreetBakeWork.unity");
  started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;if(!Lightmapping.BakeAsync())throw new Exception("Bake rejected");
 }catch(Exception e){Fail(e);}}
 public static void RebindAcceptedReflection(){try{
  Check();if(Hash(S)!="02cf100a93fae37176d844d7929e646dc5c985de6921409ec2a31f0a3eddd148")throw new Exception("Street candidate changed");
  output=File.ReadAllText(K+"/Inspection~/photo-street-current.txt").Trim();EditorSceneManager.OpenScene(S);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root");
  var cube=AssetDatabase.LoadAssetAtPath<Texture>(K+"/SunContrastLighting/DirectionalReflection.exr");if(!cube)throw new Exception("Accepted cubemap missing");
  foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>()){
   foreach(string name in new[]{"texture","bakedTexture","customBakedTexture"}){var prop=typeof(ReflectionProbe).GetProperty(name);if(prop!=null)notes.Add("Before "+name+"="+AssetDatabase.GetAssetPath(prop.GetValue(p) as UnityEngine.Object));}
   p.customBakedTexture=null;p.mode=ReflectionProbeMode.Baked;p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=cube;Persist(p);
   foreach(string name in new[]{"texture","bakedTexture","customBakedTexture"}){var prop=typeof(ReflectionProbe).GetProperty(name);if(prop!=null)notes.Add("After "+name+"="+AssetDatabase.GetAssetPath(prop.GetValue(p) as UnityEngine.Object));}
  }
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);AssetDatabase.SaveAssets();notes.Add("Explicit scene-probe rebind; accepted cubemap file unchanged. SHA256="+Hash(S));File.AppendAllText(output+"/photo-street-report.txt","\n"+string.Join("\n",notes));EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 public static void DisableContactArtifact(){try{
  Check();if(Hash(S)!="02cf100a93fae37176d844d7929e646dc5c985de6921409ec2a31f0a3eddd148")throw new Exception("Street candidate changed before confirmed AO fix");
  output=File.ReadAllText(K+"/Inspection~/photo-street-current.txt").Trim();EditorSceneManager.OpenScene(S);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root");
  var cam=pilot.GetComponentInChildren<Camera>();var contact=cam.GetComponents<MonoBehaviour>().Single(b=>b.GetType().FullName=="Kino.Obscurance");if(!contact.enabled)throw new Exception("Contact effect already disabled");contact.enabled=false;Persist(contact);
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);
  File.AppendAllText(output+"/photo-street-report.txt","\nCONFIRMED A/B: disabling Kino.Obscurance removes visible vehicle surface artifacts. Lightmap-index-only trial and same-reflection rebind did not remove them. Disable only this actual saved camera component; retain baked shadows, FXAA, cars, materials and project quality. SHA256="+Hash(S)+"\n");EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 static void Tick(){try{
  Check();double elapsed=EditorApplication.timeSinceStartup-started;if(elapsed>480)throw new Exception("Bake timeout");if(elapsed<5||Lightmapping.isRunning)return;EditorApplication.update-=Tick;
  if(LightmapSettings.lightmaps.Length==0)throw new Exception("Missing lightmaps");foreach(var r in pilot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("StreetPhoto_")))if(r.lightmapIndex<0||r.lightmapIndex>=LightmapSettings.lightmaps.Length)throw new Exception("Unmapped photo facade");
  // Reuse the already accepted scene-specific environment reflection. No reflection rebake feedback.
  foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>())if(p.mode!=ReflectionProbeMode.Custom||AssetDatabase.GetAssetPath(p.customBakedTexture)!=K+"/SunContrastLighting/DirectionalReflection.exr")throw new Exception("Accepted reflection changed unexpectedly");
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);AssetDatabase.SaveAssets();
  notes.Add("Generated diffuse photo-style textures with static window reflections; not real-place photography or reconstruction. Same cars/camera/global quality. Bake seconds="+elapsed+" SHA256="+Hash(S));File.WriteAllText(output+"/photo-street-report.txt",string.Join("\n",notes));EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 static void Fail(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
 public static void Inspect(){try{
  if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");
  EditorSceneManager.OpenScene(S);var pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root");var s=new StringBuilder();
  foreach(Transform t in pilot.transform){if(!t.name.StartsWith("Building_"))continue;s.AppendLine(t.name+" pos="+t.position+" rot="+t.eulerAngles+" scale="+t.lossyScale);foreach(var f in t.GetComponentsInChildren<MeshFilter>()){var r=f.GetComponent<MeshRenderer>();s.AppendLine("  Mesh="+AssetDatabase.GetAssetPath(f.sharedMesh)+" localBounds="+f.sharedMesh.bounds+" worldBounds="+r.bounds+" transform="+f.transform.position+" rot="+f.transform.eulerAngles+" scale="+f.transform.lossyScale+" mats="+string.Join(",",r.sharedMaterials.Select(m=>AssetDatabase.GetAssetPath(m))));}}
  File.WriteAllText(K+"/Inspection~/PHOTO-STREET-BEFORE.txt",s.ToString());EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
