using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ParkingLotEnvironment.CloudPilot {
public static class PromoteKRRealism {
 const string W="Assets/ParkingLotEnvironment/CloudPilot/KRRealism20261104", Trial=W+"/KRRealism.unity", Source="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string Original="a4e250fc18278710651bebc9b0df8dd75cdceb5bc15a922bbf42963a2f5e0297", Candidate="7939ab3c939f8769be4ed81e4d4ab0d2ed3691051592e0c0897b2c8a69075478";
 static string output, saved; static Camera cam; static int frame=-1;
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 public static void Run(){try{
  var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-promotionOutput"); if(i<0)throw new Exception("Missing output"); output=Path.GetFullPath(args[i+1]);
  if(File.Exists("/workspaces/parkside-setup/STOP")||Hash(Source)!=Original||Hash(Trial)!=Candidate)throw new Exception("STOP or source/candidate changed");
  var meta=Hash(Source+".meta"); EditorSceneManager.OpenScene(Trial);
  if(!Lightmapping.lightingDataAsset||LightmapSettings.lightmaps.Length<1)throw new Exception("Missing baked lighting");
  if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Source))throw new Exception("Save failed"); saved=Hash(Source);
  EditorSceneManager.OpenScene(Source); var root=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="ParkingLot_CornerPilot_Root"); cam=root.GetComponentInChildren<Camera>();
  var probe=UnityEngine.Object.FindObjectsOfType<ReflectionProbe>().FirstOrDefault(p=>p.boxProjection);
  if(!cam||!probe||!Lightmapping.lightingDataAsset||LightmapSettings.lightmaps.Length<1||Hash(Source+".meta")!=meta)throw new Exception("Scene validation failed");
  File.WriteAllLines(output+"/dependencies.txt",AssetDatabase.GetDependencies(Source,true).Where(p=>p.StartsWith("Assets/ParkingLotEnvironment/")));
  File.WriteAllText(output+"/scene-info.txt","Scene SHA256: "+saved+"\nLighting: "+AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)+"\nLightmaps: "+LightmapSettings.lightmaps.Length+"\nProbe: "+probe.name+" boxProjection="+probe.boxProjection+"\n");
  cam.enabled=false; EditorApplication.update+=Tick;
 }catch(Exception e){Fail(e);}}
 static void Tick(){try{
  if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");
  var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf); var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false); var old=cam.targetTexture; var prev=RenderTexture.active;
  try{rt.Create();cam.targetTexture=rt;cam.Render();if(frame>=0){RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(output+"/promoted-eye.png",tex.EncodeToPNG());}}
  finally{cam.targetTexture=old;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
  if(frame++>=0){EditorApplication.update-=Tick;if(Hash(Source)!=saved)throw new Exception("Scene changed after saving");File.WriteAllText(output+"/success.txt","Reopened and rendered promoted scene.\n");EditorApplication.Exit(0);}
 }catch(Exception e){Fail(e);}}
 static void Fail(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
}}
