using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Single-building experiment. Never calls any previous scene generator or global quality setter.
public static class CloudPilotArchitecturePass {
 const string K="Assets/ParkingLotEnvironment/CloudPilot";
 const string A=K+"/ArchitectureCandidate/Assets";
 const string W=K+"/ArchitectureCandidate/BuildingPilot";
 const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
 static string Out=>File.ReadAllText(K+"/Inspection~/architecture-current.txt").Trim();
 static StringBuilder notes=new StringBuilder();
 static void Stop(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");}
 static GameObject model;
 static Dictionary<string,Material> mats=new Dictionary<string,Material>();
 static void Prepare(){
  Stop();Directory.CreateDirectory(W);AssetDatabase.Refresh();
  var importer=(ModelImporter)AssetImporter.GetAtPath(A+"/FacadeModules.fbx");
  importer.isReadable=true;importer.generateSecondaryUV=true;importer.importCameras=false;importer.importLights=false;
  importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
  foreach(string type in new[]{"objects","trim_01","plaster"}){
   foreach(string kind in new[]{"diff","nor_gl","arm"}){
    string p=A+"/"+type+"_"+kind+".jpg";var ti=(TextureImporter)AssetImporter.GetAtPath(p);
    ti.sRGBTexture=kind=="diff";ti.isReadable=kind=="arm";ti.textureType=kind=="nor_gl"?TextureImporterType.NormalMap:TextureImporterType.Default;
    ti.maxTextureSize=2048;ti.textureCompression=kind=="arm"?TextureImporterCompression.Uncompressed:TextureImporterCompression.Compressed;ti.SaveAndReimport();
   }
   var arm=AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/"+type+"_arm.jpg");var pixels=arm.GetPixels32();var ao=new Color32[pixels.Length];
   for(int i=0;i<pixels.Length;i++){var p=pixels[i];ao[i]=new Color32(p.r,p.r,p.r,255);pixels[i]=new Color32(p.b,0,0,(byte)(255-p.g));}
   WriteTexture(type+"_MetalSmooth",arm.width,arm.height,pixels);WriteTexture(type+"_AO",arm.width,arm.height,ao);
   var m=new Material(Shader.Find("Standard")){name="PH_"+type};m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/"+type+"_diff.jpg");
   m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(A+"/"+type+"_nor_gl.jpg"));m.EnableKeyword("_NORMALMAP");m.SetFloat("_BumpScale",.55f);
   m.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(W+"/"+type+"_MetalSmooth.png"));m.EnableKeyword("_METALLICGLOSSMAP");m.SetFloat("_GlossMapScale",.8f);
   m.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(W+"/"+type+"_AO.png"));m.SetFloat("_OcclusionStrength",.7f);
   SaveMat(m,type);mats[type]=Mat(type);
  }
  // Actual transparent glazing, atlas tint/reflections plus physically separate room backing.
  var glass=new Material(mats["objects"]){name="PH_Glazing"};glass.color=new Color(.57f,.66f,.69f,.32f);
  glass.SetTexture("_MetallicGlossMap",null);glass.DisableKeyword("_METALLICGLOSSMAP");glass.SetFloat("_Metallic",.15f);glass.SetFloat("_Glossiness",.84f);
  glass.SetFloat("_Mode",3);glass.SetInt("_SrcBlend",(int)BlendMode.One);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");glass.renderQueue=3000;SaveMat(glass,"glass");mats["glass"]=glass;
  var brick=new Material(AssetDatabase.LoadAssetAtPath<Material>(K+"/Materials/Brick.mat")){name="Pilot_MetreBrick"};brick.mainTextureScale=Vector2.one;brick.color=new Color(.85f,.79f,.72f);SaveMat(brick,"brick");mats["brick"]=brick;
  var back=new Material(Shader.Find("Standard")){name="RoomBacking",color=new Color(.22f,.25f,.25f)};back.SetFloat("_Glossiness",.08f);SaveMat(back,"back");mats["back"]=back;
  model=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/FacadeModules.fbx");if(model==null)throw new Exception("FBX import missing");
 }
 static void WriteTexture(string n,int w,int h,Color32[] p){string path=W+"/"+n+".png";if(!File.Exists(path)){var t=new Texture2D(w,h,TextureFormat.RGBA32,false,true);t.SetPixels32(p);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);}AssetDatabase.ImportAsset(path);var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.sRGBTexture=false;ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.SaveAndReimport();}
 static void SaveMat(Material m,string n){string path=W+"/"+n+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(m,old);UnityEngine.Object.DestroyImmediate(m);}else AssetDatabase.CreateAsset(m,path);}
 static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(W+"/"+n+".mat");
 static Transform Node(string n)=>model.GetComponentsInChildren<Transform>(true).Single(t=>t.name==n);
 static GameObject Module(string n,Transform parent,Vector3 p,Quaternion q,Vector3 scale,bool wall){
  var src=Node(n);var o=UnityEngine.Object.Instantiate(src.gameObject,parent);o.name=n;o.transform.localPosition=p;o.transform.localRotation=q;o.transform.localScale=scale;
  foreach(var r in o.GetComponentsInChildren<MeshRenderer>()){
   r.sharedMaterials=r.sharedMaterials.Select(m=>Mat(m.name.Contains("glass")?"glass":m.name.Contains("trim_01")?"trim_01":m.name.Contains("trim_02")?"trim_01":m.name.Contains("plaster")?"brick":"objects")).ToArray();
   var mf=r.GetComponent<MeshFilter>();string path=W+"/"+n+"_Canonical.asset";var canonical=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(!canonical){canonical=UnityEngine.Object.Instantiate(mf.sharedMesh);canonical.name=n+"_CanonicalMetres";var matrix=Matrix4x4.TRS(Vector3.zero,src.localRotation,src.localScale);canonical.vertices=canonical.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();canonical.normals=canonical.normals.Select(v=>matrix.MultiplyVector(v).normalized).ToArray();if(wall)canonical.uv=canonical.vertices.Select(v=>new Vector2(v.x*.66667f,v.y*.66667f)).ToArray();canonical.RecalculateTangents();canonical.RecalculateBounds();AssetDatabase.CreateAsset(canonical,path);}mf.sharedMesh=canonical;
   GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.BatchingStatic);
   r.receiveGI=wall?ReceiveGI.Lightmaps:ReceiveGI.LightProbes;r.scaleInLightmap=wall?.65f:0;r.shadowCastingMode=ShadowCastingMode.TwoSided;
  }return o;
 }
 static void Screenshot(Camera cam,string path){var rt=new RenderTexture(1280,960,24){antiAliasing=1};var tex=new Texture2D(1280,960,TextureFormat.RGB24,false);var prev=RenderTexture.active;cam.targetTexture=rt;rt.Create();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,960),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}
 static GameObject Box(Transform parent,string n,Vector3 p,Vector3 size,Material m,bool receive){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=n;o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.localScale=size;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());var r=o.GetComponent<MeshRenderer>();r.sharedMaterial=m;
  if(n=="Closed_BackWall") {var mf=o.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=Vector3.Scale(vertices[i],size);mesh.vertices=vertices;mesh.uv=vertices.Select((v,i)=>Mathf.Abs(mesh.normals[i].z)>.5f?new Vector2(v.x/1.4f,v.y/1.4f):Mathf.Abs(mesh.normals[i].x)>.5f?new Vector2(v.z/1.4f,v.y/1.4f):new Vector2(v.x/1.4f,v.z/1.4f)).ToArray();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,W+"/Closed_BackWall_MetreUV.asset");mf.sharedMesh=mesh;o.transform.localScale=Vector3.one;}
  GameObjectUtility.SetStaticEditorFlags(o,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.BatchingStatic);r.receiveGI=receive?ReceiveGI.Lightmaps:ReceiveGI.LightProbes;r.scaleInLightmap=receive?.5f:0;return o;}
 static GameObject pilot;
 static double started;
 public static void ApplySingleBuilding(){try{
  Stop();EditorSceneManager.OpenScene(S);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root");
  if(pilot.transform.Find("ArchitecturePass_BuildingPilot")!=null)throw new Exception("Single-use replacement already applied");
  if(QualitySettings.shadows!=ShadowQuality.Disable||QualitySettings.antiAliasing!=0)throw new Exception("Native quality mismatch");
  model=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/FacadeModules.fbx");
  var old=pilot.GetComponentsInChildren<Transform>().Single(t=>t.name=="ContextBuilding_-6_24");var shell=old.Find("SolidStructure");
  if(Vector3.Distance(shell.localScale,new Vector3(8,9.6f,7))>.001f)throw new Exception("Unexpected footprint");
  // Adapt plain kit frames to painted aluminium typical of refurbished Korean low-rise exteriors.
  var frame=new Material(Mat("objects")){name="Pilot_PaintedAluminium"};frame.mainTexture=null;frame.SetTexture("_MetallicGlossMap",null);frame.DisableKeyword("_METALLICGLOSSMAP");frame.color=new Color(.55f,.58f,.57f);frame.SetFloat("_Metallic",.35f);frame.SetFloat("_Glossiness",.38f);frame.SetFloat("_BumpScale",.25f);SaveMat(frame,"paintedFrame");
  var root=new GameObject("ArchitecturePass_BuildingPilot").transform;root.SetParent(pilot.transform,false);root.SetPositionAndRotation(old.position,old.rotation);
  // Keep this building's Korean shop sign, AC, pipe, awning and roof. Remove only obsolete shell/window components.
  string[] obsolete={"SolidStructure","WindowReveal","RecessedGlazing","Mullion","Sill","SideReveal","SideGlazing","Street_RollerBlind","Street_BlindSlat","Street_ShopFrame","Street_ShopGlazing","Street_ShopMullion","Street_ShopTransom","Street_FloorJoint"};
  foreach(Transform child in old){if(!obsolete.Contains(child.name))continue;foreach(var r in child.GetComponentsInChildren<Renderer>(true)){r.enabled=false;r.shadowCastingMode=ShadowCastingMode.Off;GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);EditorUtility.SetDirty(r);}child.gameObject.SetActive(false);notes.AppendLine("Disabled old pilot only: "+child.name);}
  // Front: 3 bays x 3 storeys, exact existing 8m x 9.6m. Source display placements are discarded.
  for(int floor=0;floor<3;floor++)for(int col=0;col<3;col++){
   var anchor=new Vector3(4-col*(8f/3),floor*3.2f,-3.5f);var scale=new Vector3(8f/9,3.2f/3,1);var q=Quaternion.Euler(0,180,0);
   string suffix=floor==0?"door_centered_large_01":"window_centered_large_03";
   Module("wall_"+suffix,root,anchor,q,scale,true);var win=Module(suffix,root,anchor,q,scale,false);
   // Separate enclosed room depth, never a flat black decal over a solid facade.
   float x=anchor.x-4f/3;Box(root,"FrontRoom_BackWall",new Vector3(x,floor*3.2f+1.55f,-2.65f),new Vector3(2.62f,3.08f,.12f),Mat("back"),true);
   Box(root,"FrontRoom_Ceiling",new Vector3(x,floor*3.2f+3.05f,-3.05f),new Vector3(2.66f,.1f,.85f),Mat("plaster"),true);
   if(floor>0&&col!=1){var blind=Box(root,"Inset_RollerBlind",new Vector3(x,floor*3.2f+2.03f,-3.14f),new Vector3(1.46f,.48f,.025f),Mat("plaster"),false);}
   notes.AppendLine("PAIRED "+suffix+" origin="+anchor+" scale="+scale+" windowWorldBounds="+win.GetComponent<Renderer>().bounds);
  }
  // Both sides have the same real window/reveal pairs. Back and floors close the complete volume.
  for(int side=0;side<2;side++)for(int floor=0;floor<3;floor++)for(int bay=0;bay<2;bay++){
   float yaw=side==0?-90:90;var q=Quaternion.Euler(0,yaw,0);var p=new Vector3(side==0?-4:4,floor*3.2f,side==0?-3.5f+bay*3.5f:3.5f-bay*3.5f);var scale=new Vector3(7f/6,3.2f/3,1);
   Module("wall_window_centered_large_03",root,p,q,scale,true);Module("window_centered_large_03",root,p,q,scale,false);
  }
  Box(root,"Closed_BackWall",new Vector3(0,4.8f,3.4f),new Vector3(8,9.6f,.2f),Mat("brick"),true);
  Box(root,"SideInterior_Left",new Vector3(-3.15f,4.8f,.45f),new Vector3(.14f,9.6f,6.1f),Mat("back"),true);Box(root,"SideInterior_Right",new Vector3(3.15f,4.8f,.45f),new Vector3(.14f,9.6f,6.1f),Mat("back"),true);
  for(int f=0;f<=3;f++)Box(root,"Closed_FloorOrRoof_"+f,new Vector3(0,f*3.2f+.035f,0),new Vector3(8,.07f,7),Mat("plaster"),true);
  foreach(var r in root.GetComponentsInChildren<Renderer>()){
   if(r.name=="window_centered_large_03")r.sharedMaterials=r.sharedMaterials.Select(m=>m==Mat("objects")?Mat("paintedFrame"):m).ToArray();
   notes.AppendLine("NEW "+r.name+" bounds="+r.bounds);
  }
  File.WriteAllText(Out+"/building-assembly.txt",notes.ToString());
  var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="ArchitectureBalancedCPU";settings.autoGenerate=false;AssetDatabase.CreateAsset(settings,W+"/ArchitectureBalancedCPU.lighting");Lightmapping.lightingSettings=settings;
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
  // Reviewable checkpoint before baking, confined to owned folder. Main scene saved only after successful bake.
  EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),W+"/ArchitectureBakeWork.unity");
  started=EditorApplication.timeSinceStartup;EditorApplication.update+=BakeTick;if(!Lightmapping.BakeAsync())throw new Exception("Bake rejected");
 }catch(Exception e){EditorApplication.update-=BakeTick;Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void RefineOwnedReceivers(){try{
  Stop();EditorSceneManager.OpenScene(S);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root");var building=pilot.transform.Find("ArchitecturePass_BuildingPilot");if(!building)throw new Exception("Missing single building");
  if(AssetDatabase.LoadAssetAtPath<Mesh>(W+"/OwnedReceiver_0.asset"))throw new Exception("Receiver refinement already applied");int index=0;
  foreach(var mf in building.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("FrontRoom_")||f.name.StartsWith("SideInterior_")||f.name.StartsWith("Closed_"))){
   if(mf.name.StartsWith("Closed_FloorOrRoof_"))mf.transform.localScale=new Vector3(7.72f,.07f,6.72f);
   var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);mesh.name="ArchitectureReceiver_"+index;var scale=mf.transform.localScale;mesh.vertices=mesh.vertices.Select(v=>Vector3.Scale(v,scale)).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
   UnwrapParam unwrap;UnwrapParam.SetDefaults(out unwrap);unwrap.packMargin=.04f;if(!Unwrapping.GenerateSecondaryUVSet(mesh,unwrap))throw new Exception("New box unwrap failed");
   AssetDatabase.CreateAsset(mesh,W+"/OwnedReceiver_"+index+".asset");mf.sharedMesh=mesh;mf.transform.localScale=Vector3.one;var r=mf.GetComponent<MeshRenderer>();
   if(mf.name=="FrontRoom_Ceiling"){r.receiveGI=ReceiveGI.LightProbes;r.scaleInLightmap=0;}EditorUtility.SetDirty(mf);EditorUtility.SetDirty(r);notes.AppendLine("Owned box receiver UV and inset floors: "+mf.name);index++;
  }
  File.WriteAllText(Out+"/receiver-refinement.txt",notes.ToString());AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),W+"/ArchitectureBakeWork.unity");
  started=EditorApplication.timeSinceStartup;EditorApplication.update+=BakeTick;if(!Lightmapping.BakeAsync())throw new Exception("Refinement bake rejected");
 }catch(Exception e){EditorApplication.update-=BakeTick;Debug.LogException(e);EditorApplication.Exit(1);}}
 static void BakeTick(){try{
  Stop();double elapsed=EditorApplication.timeSinceStartup-started;if(elapsed>420)throw new Exception("Bake timed out");if(elapsed<5||Lightmapping.isRunning)return;EditorApplication.update-=BakeTick;
  if(LightmapSettings.lightmaps.Length==0)throw new Exception("No lightmaps");
  foreach(var r in pilot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="RearStreet"||r.name=="RightStreetFootway"))if(r.lightmapIndex<0||r.lightmapIndex>=LightmapSettings.lightmaps.Length)throw new Exception("Repaired road/footway receiver lost");
  foreach(var probe in pilot.GetComponentsInChildren<ReflectionProbe>()){string p=W+"/ArchitectureReflection.exr";if(!Lightmapping.BakeReflectionProbe(probe,p))throw new Exception("Reflection bake failed");AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(p);EditorUtility.SetDirty(probe);}
  var building=pilot.transform.Find("ArchitecturePass_BuildingPilot");foreach(var r in building.GetComponentsInChildren<MeshRenderer>().Where(r=>r.receiveGI==ReceiveGI.Lightmaps))if(r.lightmapIndex<0||r.lightmapIndex>=LightmapSettings.lightmaps.Length)throw new Exception("Unmapped new wall: "+r.name);
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);AssetDatabase.SaveAssets();
  File.WriteAllText(Out+"/building-bake.txt","Balanced CPU seconds="+elapsed+" lightmaps="+LightmapSettings.lightmaps.Length+" newRenderers="+building.GetComponentsInChildren<Renderer>().Length+" shadows="+QualitySettings.shadows+" AA="+QualitySettings.antiAliasing+"\n");EditorApplication.Exit(0);
 }catch(Exception e){EditorApplication.update-=BakeTick;Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void InspectAluminium(){try{
  Stop();model=AssetDatabase.LoadAssetAtPath<GameObject>(A+"/FacadeModules.fbx");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("PairedModule_Aluminium").transform;
  Module("wall_window_centered_large_02",root,Vector3.zero,Quaternion.identity,Vector3.one,true);var window=Module("window_centered_large_02",root,Vector3.zero,Quaternion.identity,Vector3.one,false);
  Box(root,"RoomBacking",new Vector3(1.5f,1.5f,-.65f),new Vector3(3,3,.1f),Mat("back"),true);
  var light=new GameObject("InspectionOnlyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(35,150,0);RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
  var cam=new GameObject("InspectionOnlyCamera").AddComponent<Camera>();cam.transform.position=new Vector3(4,2.4f,5);cam.transform.LookAt(new Vector3(1.5f,1.5f,0));cam.fieldOfView=44;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.35f,.4f,.45f);Screenshot(cam,Out+"/module-aluminium-preview.png");File.WriteAllText(Out+"/module-aluminium-bounds.txt","Wall 3m x 3m; window="+window.GetComponent<Renderer>().bounds);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 public static void Inspect(){try{
  Prepare();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  foreach(string n in new[]{"wall_window_centered_large_03","window_centered_large_03","wall_door_centered_large_01","door_centered_large_01"}){
   var t=Node(n);notes.AppendLine(n+" display="+t.localPosition+" rotation="+t.localEulerAngles+" scale="+t.localScale);
   foreach(var f in t.GetComponentsInChildren<MeshFilter>()){notes.AppendLine("MESH "+f.sharedMesh.bounds+" firstNormal="+f.sharedMesh.normals[0]+" mats="+string.Join(",",f.GetComponent<Renderer>().sharedMaterials.Select(m=>m.name)));}
  }
  var root=new GameObject("PairedModule_MetreCheck").transform;
  Module("wall_window_centered_large_03",root,Vector3.zero,Quaternion.identity,Vector3.one,true);Module("window_centered_large_03",root,Vector3.zero,Quaternion.identity,Vector3.one,false);
  var light=new GameObject("InspectionOnlyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(35,150,0);RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
  Box(root,"RoomBacking",new Vector3(1.5f,1.5f,-.5f),new Vector3(3,3,.1f),Mat("back"),true);
  var cam=new GameObject("InspectionOnlyCamera").AddComponent<Camera>();cam.transform.position=new Vector3(4,2.4f,5);cam.transform.LookAt(new Vector3(1.5f,1.5f,0));cam.fieldOfView=44;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.35f,.4f,.45f);
  Screenshot(cam,Out+"/module-preview.png");File.WriteAllText(Out+"/module-inspection.txt",notes.ToString());AssetDatabase.SaveAssets();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
