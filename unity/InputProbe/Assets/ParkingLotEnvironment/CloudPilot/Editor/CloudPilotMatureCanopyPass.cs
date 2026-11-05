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
public static class CloudPilotMatureCanopyPass {
 const string K="Assets/ParkingLotEnvironment/CloudPilot";
 const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string W=K+"/MatureCanopyPilot";
 const string Expected="94d9624b67ca060bb43ad0156958aae215934c0a6fd5c428536876df9f4d705a";
 static GameObject pilot;
 static string output;
 static double started;
 static readonly List<string> notes=new List<string>();
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");if(QualitySettings.shadows!=ShadowQuality.Disable||QualitySettings.antiAliasing!=0)throw new Exception("Unexpected project quality");}
 static void Persist(UnityEngine.Object o){EditorUtility.SetDirty(o);if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o);}
 static Transform[] Trees(){return pilot.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.IndexOf("leaves",StringComparison.OrdinalIgnoreCase)>=0).Select(f=>TreeRoot(f.transform,pilot.transform)).Distinct().ToArray();}
 static Bounds Foliage(Transform root){return root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.IndexOf("leaves",StringComparison.OrdinalIgnoreCase)>=0).bounds;}
 static void Grow(Transform root,float factor,float yaw,Vector3? position=null){
  notes.Add("BEFORE "+root.name+" position="+root.position+" scale="+root.lossyScale+" yaw="+root.eulerAngles.y+" foliage="+Foliage(root));
  root.localScale*=factor;root.rotation=Quaternion.Euler(0,yaw,0);if(position.HasValue)root.position=position.Value;Persist(root);
  notes.Add("AFTER "+root.name+" position="+root.position+" scale="+root.lossyScale+" yaw="+root.eulerAngles.y+" foliage="+Foliage(root));
 }
 public static void ApplyAndBake(){try{
  Check();if(Hash(S)!=Expected)throw new Exception("Scene changed since accepted photo-street checkpoint");if(Directory.Exists(W))throw new Exception("Single-use mature canopy candidate already exists");
  var args=Environment.GetCommandLineArgs();int oi=Array.IndexOf(args,"-parkingMatureCanopyOutput");if(oi<0)throw new Exception("Missing output");output=Path.GetFullPath(args[oi+1]);Directory.CreateDirectory(output);
  EditorSceneManager.OpenScene(S);var roots=SceneManager.GetActiveScene().GetRootGameObjects();pilot=roots.Single(x=>x.name=="ParkingLot_CornerPilot_Root");if(roots.Single(x=>x.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
  var contact=pilot.GetComponentInChildren<Camera>().GetComponents<MonoBehaviour>().Single(b=>b.GetType().FullName=="Kino.Obscurance");if(contact.enabled)throw new Exception("Known contact artifact effect re-enabled");
  var trees=Trees();if(trees.Length!=11)throw new Exception("Unexpected tree inventory");
  var left=trees.Where(t=>t.position.x< -9&&t.position.z<14).OrderBy(t=>t.position.z).ToArray();if(left.Length!=4)throw new Exception("Left tree inventory changed");
  float[] factors={1.45f,1.3f,1.2f,1.2f};float[] yaw={75,-55,35,-20};for(int i=0;i<left.Length;i++)Grow(left[i],factors[i],yaw[i]);
  var right0=trees.Single(t=>t.name=="Daylight_StreetTree_0");if(Vector3.Distance(right0.position,new Vector3(9.8f,.13f,-7))>.01f)throw new Exception("Near right tree moved before trial");
  Grow(right0,1.5f,70,new Vector3(9.8f,.13f,-12.2f));Grow(trees.Single(t=>t.name=="Daylight_StreetTree_1"),1.3f,73);Grow(trees.Single(t=>t.name=="Daylight_StreetTree_2"),1.15f,146);
  Directory.CreateDirectory(W);AssetDatabase.Refresh();var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="MatureCanopy_Lighting";settings.autoGenerate=false;AssetDatabase.CreateAsset(settings,W+"/MatureCanopy.lighting");Lightmapping.lightingSettings=settings;
  notes.Add("Whole-frame composition trial:7 existing near-boundary trees,larger varied crowns; one near-right root moved toz-12.2 inside plot boundary. Same meshes/materials/count; no added leaf copies,unchanged four background trees/cars/camera/roads/buildings/QualitySettings. Contact shading remains disabled.");
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),W+"/MatureCanopyBakeWork.unity");started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;if(!Lightmapping.BakeAsync())throw new Exception("Bake rejected");
 }catch(Exception e){Fail(e);}}
 static void Tick(){try{
  Check();double elapsed=EditorApplication.timeSinceStartup-started;if(elapsed>480)throw new Exception("Bake timeout");if(elapsed<5||Lightmapping.isRunning)return;EditorApplication.update-=Tick;
  if(LightmapSettings.lightmaps.Length==0)throw new Exception("No lightmaps");foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>())if(p.mode!=ReflectionProbeMode.Custom||AssetDatabase.GetAssetPath(p.customBakedTexture)!=K+"/SunContrastLighting/DirectionalReflection.exr")throw new Exception("Accepted reflection changed");
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);AssetDatabase.SaveAssets();notes.Add("Bake seconds="+elapsed+" SHA256="+Hash(S));File.WriteAllText(output+"/mature-canopy-report.txt",string.Join("\n",notes));EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 static void Fail(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
 static Transform TreeRoot(Transform leaf,Transform pilot){var t=leaf;while(t.parent!=null&&t.parent!=pilot&&t.parent.name!="DaylightPass_Context")t=t.parent;return t;}
 public static void Inspect(){try{
  if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");EditorSceneManager.OpenScene(S);var pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root").transform;var report=new StringBuilder();
  foreach(var f in pilot.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.IndexOf("leaves",StringComparison.OrdinalIgnoreCase)>=0)){var root=TreeRoot(f.transform,pilot);report.AppendLine("TREE root="+root.name+" position="+root.position+" rotation="+root.eulerAngles+" scale="+root.lossyScale+" foliageBounds="+f.GetComponent<Renderer>().bounds+" foliageLocal="+f.sharedMesh.bounds);}
  File.WriteAllText(K+"/Inspection~/MATURE-CANOPY-BEFORE.txt",report.ToString());EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
