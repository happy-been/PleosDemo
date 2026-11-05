using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ParkingLotEnvironment.CloudPilot {
public static class AntigravitySunSweep {
 const string Source="Assets/_Parkside/Scenes/ParklingLot.unity",W="Assets/ParkingLotEnvironment/CloudPilot/AntigravityCollab20261103",Trial=W+"/AntigravityCandidateV11.unity",Expected="a5262f8ab1d5f944693658f5ab21c89e68897d0974bb9d5432824106f2ccf9d7";
 static string output,trialHash;static Camera cam;static Light sun;static Transform tree;static Vector3 start;static int frame=-2;static Vector3[] directions={new Vector3(.42f,-.62f,.66f),new Vector3(.62f,-.62f,.48f),new Vector3(.72f,-.60f,.35f),new Vector3(.78f,-.56f,.29f)};
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP")||Hash(Source)!=Expected||Hash(Trial)!=trialHash)throw new Exception("STOP or source changed");}
 public static void Run(){try{var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-collabOutput");if(i<0)throw new Exception("Output required");output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);trialHash=Hash(Trial);Check();EditorSceneManager.OpenScene(Trial);var root=SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="ParkingLot_CornerPilot_Root");tree=root.transform.Find("Integrated_FrontCanopy/Integrated_FrontTree_2");if(!tree)throw new Exception("Tree missing");start=tree.position;sun=root.GetComponentsInChildren<Light>(true).First(l=>l.type==LightType.Directional);sun.lightmapBakeType=LightmapBakeType.Realtime;foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.lightmapIndex=-1;cam=root.GetComponentInChildren<Camera>();cam.enabled=false;cam.transform.SetPositionAndRotation(new Vector3(0,1.65f,-11.8f),Quaternion.Euler(2.5f,-18,0));var car=root.transform.Find("EXISTING_VEHICLES_UNAPPROVED/UNAPPROVED_hyundai-tucson_0");var log=new StringBuilder().AppendLine("tree="+start+" sun="+sun.transform.forward+" car="+car.position);foreach(var r in car.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterials.Any(m=>m&&m.name.Contains("VehiclePaint")))log.AppendLine(r.name+" bounds="+r.bounds+" mats="+string.Join(",",r.sharedMaterials.Where(m=>m).Select(m=>m.name)));File.WriteAllText(output+"/geometry.txt",log.ToString());EditorApplication.update+=Tick;}catch(Exception e){Fail(e);}}
 static void Tick(){try{Check();if(frame>=directions.Length){EditorApplication.update-=Tick;File.WriteAllText(output+"/complete.txt","No candidate scene or original saved. All positions keep sidewalk x unchanged.\n");EditorApplication.Exit(0);return;}tree.position=start;sun.transform.rotation=Quaternion.LookRotation(directions[Math.Max(0,frame)].normalized);Render(frame<0?null:"sun"+frame+".png");frame++;}catch(Exception e){Fail(e);}}
 static void Render(string name){var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);var pic=new Texture2D(1920,1080,TextureFormat.RGB24,false);var prev=RenderTexture.active;var target=cam.targetTexture;try{rt.Create();cam.targetTexture=rt;cam.Render();if(name!=null){RenderTexture.active=rt;pic.ReadPixels(new Rect(0,0,1920,1080),0,0);pic.Apply();File.WriteAllBytes(output+"/"+name,pic.EncodeToPNG());}}finally{cam.targetTexture=target;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pic);}}
 static void Fail(Exception e){EditorApplication.update-=Tick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
}}
