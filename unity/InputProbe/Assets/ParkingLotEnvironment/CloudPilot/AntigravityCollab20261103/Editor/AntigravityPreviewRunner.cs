using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ParkingLotEnvironment.CloudPilot.Integrated;
namespace ParkingLotEnvironment.CloudPilot {
public static class AntigravityPreviewRunner {
 const string Source="Assets/_Parkside/Scenes/ParklingLot.unity", W="Assets/ParkingLotEnvironment/CloudPilot/AntigravityCollab20261103", Trial=W+"/AntigravityCandidate.unity", Expected="a5262f8ab1d5f944693658f5ab21c89e68897d0974bb9d5432824106f2ccf9d7";
 static string output,trialHash;static GameObject root;static Camera cam;static Light sun;static int frame=-2;static Vector3 position;static Quaternion rotation;
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP")||Hash(Source)!=Expected)throw new Exception("STOP or source scene changed");}
 public static void FastPreview(){try{
  Check();var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-collabOutput");if(i<0)throw new Exception("Output required");output=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(output);
  if(File.Exists(Trial))throw new Exception("Candidate already exists");
  foreach(string s in new[]{"AntigravityGlass.shader","AntigravityFoliage.shader","AntigravityPhotoFinish.shader"}){var sh=AssetDatabase.LoadAssetAtPath<Shader>(W+"/"+s);if(!sh||!sh.isSupported||ShaderUtil.ShaderHasError(sh))throw new Exception("Shader unsupported or compile error: "+s);}
  EditorSceneManager.OpenScene(Source);var scene=SceneManager.GetActiveScene();root=scene.GetRootGameObjects().Single(g=>g.name=="ParkingLot_CornerPilot_Root");if(scene.GetRootGameObjects().Single(g=>g.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
  AntigravityCollabPass1.ApplyVisualFixesToOpenScene(root);
  var renderers=root.GetComponentsInChildren<Renderer>(true);int glass=renderers.SelectMany(x=>x.sharedMaterials).Count(x=>x&&x.shader&&x.shader.name=="ParkingLot/AntigravityGlass"),leaves=renderers.SelectMany(x=>x.sharedMaterials).Count(x=>x&&x.shader&&x.shader.name=="ParkingLot/AntigravityFoliage");
  cam=root.GetComponentInChildren<Camera>(true);sun=root.GetComponentsInChildren<Light>(true).First(l=>l.type==LightType.Directional);var fx=cam.GetComponent<ParkingLotIntegratedPhotoFinish>();
  if(glass<7||leaves<1||!fx||!fx.shader||fx.shader.name!="Hidden/ParkingLot/AntigravityPhotoFinish")throw new Exception("Missing applied shader assignments: glass="+glass+" leaves="+leaves);
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene,Trial))throw new Exception("Candidate save failed");trialHash=Hash(Trial);Check();
  // The fast preview deliberately removes baked renderer lightmaps in memory only and makes the sun realtime.
  foreach(var r in renderers)r.lightmapIndex=-1;sun.lightmapBakeType=LightmapBakeType.Realtime;position=cam.transform.position;rotation=cam.transform.rotation;cam.enabled=false;
  File.WriteAllText(output+"/fast-info.txt","Fast preview: realtime sun, renderer lightmaps disabled in memory; no bake. Glass slots="+glass+", foliage slots="+leaves+", sun="+sun.transform.forward+", source SHA="+Hash(Source)+", candidate SHA="+trialHash+"\n");
  EditorApplication.update+=Tick;
 }catch(Exception e){Fail(e);}}
 static void Tick(){try{Check();if(frame<0)Render(null,position,rotation,960,540);else if(frame==0)Render("eye.png",position,rotation,1920,1080);else if(frame==1)Render("close-car.png",new Vector3(0,1.65f,-11.8f),Quaternion.Euler(2.5f,-18,0),1920,1080);else if(frame==2){sun.shadows=LightShadows.None;Render("close-car-no-sun-shadow.png",new Vector3(0,1.65f,-11.8f),Quaternion.Euler(2.5f,-18,0),1920,1080);}else{EditorApplication.update-=Tick;if(Hash(Trial)!=trialHash)throw new Exception("Candidate changed after save");File.WriteAllText(output+"/fast-complete.txt","Rendered preview before bake; realtime sun with renderer lightmaps disabled in memory.\n");EditorApplication.Exit(0);return;}frame++;}catch(Exception e){Fail(e);}}
 static void Render(string name,Vector3 p,Quaternion q,int w,int h){var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);var pic=new Texture2D(w,h,TextureFormat.RGB24,false);var prev=RenderTexture.active;var target=cam.targetTexture;try{rt.Create();cam.targetTexture=rt;cam.transform.SetPositionAndRotation(p,q);cam.Render();if(name!=null){RenderTexture.active=rt;pic.ReadPixels(new Rect(0,0,w,h),0,0);pic.Apply();File.WriteAllBytes(output+"/"+name,pic.EncodeToPNG());}}finally{cam.targetTexture=target;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pic);}}
 static void Fail(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED-fast.txt",e.ToString());EditorApplication.Exit(1);}
}}
