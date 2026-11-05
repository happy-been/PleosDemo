using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
public static class CloudPilotContactPass {
 const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string Kit="Assets/ParkingLotEnvironment/CloudPilot";
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 public static void Apply(){
  try{
   if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");
   if(Hash(ScenePath)!="5bcf4dae774a789ee2d8115647cdfa4096779ccb8aba821b22f2ee76ac120350")throw new Exception("Scene changed; refusing overwrite");
   EditorSceneManager.OpenScene(ScenePath);
   var roots=SceneManager.GetActiveScene().GetRootGameObjects();
   var pilot=roots.Single(g=>g.name=="ParkingLot_CornerPilot_Root");
   if(roots.Single(g=>g.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root must stay inactive");
   var camera=pilot.GetComponentInChildren<Camera>();
   if(camera.GetComponent<Kino.Obscurance>()!=null)throw new Exception("Contact pass already exists");
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(Kit+"/ContactShading/Shader/Obscurance.shader");
   if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Contact shader unavailable or failed compilation");
   var effect=camera.gameObject.AddComponent<Kino.Obscurance>();
   effect.intensity=1.5f;effect.radius=.75f;effect.downsampling=true;
   effect.sampleCount=Kino.Obscurance.SampleCount.Medium;
   effect.occlusionSource=Kino.Obscurance.OcclusionSource.DepthNormalsTexture;
   effect.ambientOnly=false;
   var serialized=new SerializedObject(effect);serialized.FindProperty("_aoShader").objectReferenceValue=shader;serialized.ApplyModifiedPropertiesWithoutUndo();
   camera.depthTextureMode|=DepthTextureMode.DepthNormals;
   EditorUtility.SetDirty(camera);EditorUtility.SetDirty(effect);
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
   EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
   Debug.Log("PARKSIDE_CONTACT_APPLIED Camera="+camera.name+" samples=12 halfResolution=true radius=.75 intensity=1.5 SHA256="+Hash(ScenePath));
   EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
}
