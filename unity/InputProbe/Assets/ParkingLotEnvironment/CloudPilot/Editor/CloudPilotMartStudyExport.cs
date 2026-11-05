using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ParkingLotEnvironment.CloudPilot {
public static class CloudPilotMartStudyExport {
 const string K="Assets/ParkingLotEnvironment/CloudPilot",W=K+"/MartPilot",Work=W+"/MartBakeWork.unity";
 const string Destination="/workspaces/parkside-mart-render-study";
 static string Hash(string p){using(var h=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 public static void Export(){try{if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");if(Directory.Exists(Destination))throw new Exception("Isolated study already exists");string before=Hash(Work),production=Hash("Assets/_Parkside/Scenes/ParklingLot.unity");EditorSceneManager.OpenScene(Work);var scene=SceneManager.GetActiveScene();foreach(var go in scene.GetRootGameObjects().Where(g=>g.name!="ParkingLot_CornerPilot_Root").ToArray())UnityEngine.Object.DestroyImmediate(go);foreach(var b in scene.GetRootGameObjects()[0].GetComponentsInChildren<MonoBehaviour>(true).Where(x=>x&&x.GetType().FullName=="Kino.Obscurance").ToArray())UnityEngine.Object.DestroyImmediate(b);
  Lightmapping.lightingDataAsset=null;LightmapSettings.lightmaps=new LightmapData[0];string study=W+"/RendererStudy.unity";EditorSceneManager.SaveScene(scene,study);
  var deps=new HashSet<string>(AssetDatabase.GetDependencies(study,true).Where(p=>p.StartsWith("Assets/")&&File.Exists(p)&&(!p.EndsWith(".unity")||p==study)));
  foreach(string dir in new[]{K+"/DaylightGrade",K+"/Canopy"})foreach(string p in Directory.GetFiles(dir))if(!p.EndsWith(".meta")&&(p.Contains("MapDaylightGrade")||p.Contains("ACESFitted")||p.Contains("MapCameraFXAA")))deps.Add(p.Replace('\\','/'));
  foreach(string p in new[]{"Assets/ParkingLotEnvironment/CREDITS.md","Assets/ParkingLotEnvironment/Fonts/OFL.txt",K+"/DaylightGrade/LICENSE-ACES.txt"})if(File.Exists(p))deps.Add(p);
  long bytes=0;foreach(string p in deps){string target=Destination+"/"+p;Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(p,target);bytes+=new FileInfo(p).Length;if(File.Exists(p+".meta"))File.Copy(p+".meta",target+".meta",true);}
  foreach(string dir in new[]{"Packages","ProjectSettings"})foreach(string p in Directory.GetFiles(dir)){Directory.CreateDirectory(Destination+"/"+dir);File.Copy(p,Destination+"/"+p);}
  File.WriteAllText(Destination+"/source-dependencies.txt",string.Join("\n",deps.OrderBy(p=>p)));File.WriteAllText(Destination+"/SOURCE.txt","Disposable real Unity renderer comparison. Original project unchanged. Same MartPilot geometry/materials/camera; only isolated color space and native shadow configuration will differ. Re-bake required. Assets retain original licenses.\nCopied bytes="+bytes+"\nSource trial SHA="+before);
  if(Hash(Work)!=before||Hash("Assets/_Parkside/Scenes/ParklingLot.unity")!=production)throw new Exception("Production or trial altered by export");Debug.Log("MART_STUDY_EXPORTED files="+deps.Count+" bytes="+bytes);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
}
