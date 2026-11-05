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
// Explicit editor-only experiment. Never changes QualitySettings or installs camera callbacks.
public static class CloudPilotBakedPass {
 const string Kit="Assets/ParkingLotEnvironment/CloudPilot";
 const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string Owned=Kit+"/BakedLighting";
 static string output; static string work; static GameObject pilot; static double start,last; static bool completing; static int frame;
 static bool capturing; static StringBuilder log=new StringBuilder();
 static Camera Eye {get{return pilot.GetComponentInChildren<Camera>();}}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new OperationCanceledException("STOP");}
 static void Note(string s){log.AppendLine(s);Debug.Log("BAKED_PASS "+s);if(output!=null)File.WriteAllText(output+(capturing?"/capture-report.txt":"/bake-report.txt"),log.ToString());}
 static void Open(string path){EditorSceneManager.OpenScene(path,OpenSceneMode.Single);pilot=SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_CornerPilot_Root");if(SceneManager.GetActiveScene().GetRootGameObjects().Single(x=>x.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");}
 public static void Begin(){try {
  Check();output=File.ReadAllText(Kit+"/Inspection~/native-bake-current.txt").Trim();Open(Target);
  if(QualitySettings.shadows!=ShadowQuality.Disable)throw new Exception("Expected native shadows Disable");
  Directory.CreateDirectory(Owned);Directory.CreateDirectory(Owned+"/Meshes");AssetDatabase.Refresh();
  work=Owned+"/NativeBakeWork.unity";
  if(File.Exists(work))throw new Exception("Experiment already exists; review before repeat");
  EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),work);Open(work);
  var settings=new LightingSettings();settings.name="ParklingLot_StaticCPU";settings.autoGenerate=false;settings.realtimeGI=false;settings.bakedGI=true;
  settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;settings.lightmapResolution=8;settings.lightmapMaxSize=1024;settings.directionalityMode=LightmapsMode.NonDirectional;
  settings.directSampleCount=24;settings.indirectSampleCount=48;settings.environmentSampleCount=32;settings.maxBounces=2;
  settings.ao=true;settings.aoMaxDistance=1.4f;settings.aoExponentDirect=.35f;settings.aoExponentIndirect=.7f;
  AssetDatabase.CreateAsset(settings,Owned+"/ParklingLot_StaticCPU.lighting");Lightmapping.lightingSettings=settings;
  foreach(var l in pilot.GetComponentsInChildren<Light>())l.lightmapBakeType=LightmapBakeType.Baked;
  var meshes=new Dictionary<Mesh,Mesh>();int receiver=0,caster=0,excluded=0;float area=0;
  var coverage=new StringBuilder("path,mode,scale,uv2,vertices\n");
  foreach(var r in pilot.GetComponentsInChildren<MeshRenderer>()) {
   var mf=r.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
   string path=PathOf(r.transform);bool outer=path.Contains("Daylight_FinalGround");
   bool organic=System.Text.RegularExpressions.Regex.Replace(path,"street","",System.Text.RegularExpressions.RegexOptions.IgnoreCase).IndexOf("tree",StringComparison.OrdinalIgnoreCase)>=0||path.Contains("Shrub")||path.IndexOf("vehicle",StringComparison.OrdinalIgnoreCase)>=0||path.Contains("Truck");
   if(outer||r.GetComponent<TextMesh>()!=null) {GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)&~StaticEditorFlags.ContributeGI);excluded++;continue;}
   GameObjectUtility.SetStaticEditorFlags(r.gameObject,GameObjectUtility.GetStaticEditorFlags(r.gameObject)|StaticEditorFlags.ContributeGI);
   r.receiveShadows=true;r.shadowCastingMode=ShadowCastingMode.TwoSided;r.lightProbeUsage=LightProbeUsage.BlendProbes;
   if(organic){r.receiveGI=ReceiveGI.LightProbes;r.scaleInLightmap=0;caster++;}
   else {
    var src=mf.sharedMesh;Mesh clone;
    if(!meshes.TryGetValue(src,out clone)) {
     clone=UnityEngine.Object.Instantiate(src);clone.name="BakedUV_"+meshes.Count+"_"+src.name;
     // Always unwrap a clone; presence of a UV2 array alone does not prove usable charts.
     if(!Unwrapping.GenerateSecondaryUVSet(clone))throw new Exception("UV2 failed "+src.name);
     AssetDatabase.CreateAsset(clone,Owned+"/Meshes/Mesh_"+meshes.Count+".asset");meshes.Add(src,clone);
    }
    mf.sharedMesh=clone;r.receiveGI=ReceiveGI.Lightmaps;
    bool pavement=path.Contains("Zone_Asphalt")||path.Contains("Sidewalk")||path.Contains("Curb");
    r.scaleInLightmap=pavement?1f:.25f;receiver++;var b=r.bounds.size;area+=(b.x*b.y+b.y*b.z+b.x*b.z)*2*r.scaleInLightmap*r.scaleInLightmap;
   }
   coverage.AppendLine(path.Replace(",","_")+","+(organic?"caster_probe":"lightmap")+","+r.scaleInLightmap+","+mf.sharedMesh.uv2.Length+","+mf.sharedMesh.vertexCount);
  }
  File.WriteAllText(output+"/coverage-setup.csv",coverage.ToString());
  FixLeaves();
  var group=new GameObject("BakedPass_SceneLightProbes");group.transform.SetParent(pilot.transform,false);var probes=group.AddComponent<LightProbeGroup>();var pts=new List<Vector3>();
  foreach(float y in new[]{.45f,1.6f,4f,8f})for(int x=-12;x<=12;x+=6)for(int z=-15;z<=27;z+=7)pts.Add(new Vector3(x,y,z));probes.probePositions=pts.ToArray();
  // Realtime probes are excluded from the GI bake. A separately baked owned cubemap is assigned afterwards.
  foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>())p.enabled=false;
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  Note("SETUP receivers="+receiver+" casterProbe="+caster+" excluded="+excluded+" clonedMeshes="+meshes.Count+" estimatedScaledBoundsArea="+area+" probePoints="+pts.Count+" CPU=8tex/m max1024 samples24/48/32 bounces2 nonDirectional nativeShadows="+QualitySettings.shadows);
  start=EditorApplication.timeSinceStartup;last=start;EditorApplication.update+=BakeTick;
  bool accepted=Lightmapping.BakeAsync();Note("BakeAsync accepted="+accepted);if(!accepted)CompleteBake(false,"BakeAsync rejected");
 }catch(Exception e){Fail(e);}}
 public static void ReviseMixed(){try{
  Check();output=File.ReadAllText(Kit+"/Inspection~/native-bake-current.txt").Trim();Open(Owned+"/NativeBakeWork.unity");work=Owned+"/NativeBakeWork.unity";
  var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="ParklingLot_FinalCPU";AssetDatabase.CreateAsset(settings,Owned+"/ParklingLot_FinalCPU.lighting");Lightmapping.lightingSettings=settings;settings.realtimeEnvironmentLighting=false;RenderSettings.ambientMode=AmbientMode.Skybox;RenderSettings.ambientIntensity=1.0f;settings.mixedBakeMode=MixedLightingMode.Subtractive;EditorUtility.SetDirty(settings);
  foreach(var l in pilot.GetComponentsInChildren<Light>()){l.lightmapBakeType=LightmapBakeType.Mixed;l.transform.rotation=Quaternion.Euler(68,145,0);Persist(l);Persist(l.transform);}
  var revisedMeshes=new Dictionary<Mesh,Mesh>();Directory.CreateDirectory(Owned+"/PaddedMeshes");AssetDatabase.Refresh();
  foreach(var r in pilot.GetComponentsInChildren<MeshRenderer>()){
   if(r.scaleInLightmap==0 && (GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0)r.receiveGI=ReceiveGI.Lightmaps;
   var mf=r.GetComponent<MeshFilter>();
   if(mf!=null && mf.sharedMesh!=null && r.scaleInLightmap>0 && r.receiveGI==ReceiveGI.Lightmaps && AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith(Owned+"/Meshes/")){
    Mesh m;if(!revisedMeshes.TryGetValue(mf.sharedMesh,out m)){m=UnityEngine.Object.Instantiate(mf.sharedMesh);UnwrapParam param;UnwrapParam.SetDefaults(out param);param.packMargin=.04f;if(!Unwrapping.GenerateSecondaryUVSet(m,param))throw new Exception("Padded unwrap failed");AssetDatabase.CreateAsset(m,Owned+"/PaddedMeshes/Mesh_"+revisedMeshes.Count+".asset");revisedMeshes.Add(mf.sharedMesh,m);}mf.sharedMesh=m;
   }
   Persist(r);if(mf!=null)Persist(mf);Persist(r.gameObject);
  }
  foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>()){p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(Owned+"/ParklingLot_LocalReflection.exr");p.enabled=true;Persist(p);}
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  Note("REVISION Mixed Subtractive sun68,145. Environment baked from existing skybox. Caster receiveGI Lightmaps with scale0; silhouette verification pending final images. Static receives baked direct+indirect; probe receivers retain realtime direct. Native shadows="+QualitySettings.shadows+". Future moving cars cannot change baked parked-car shadows.");
  start=EditorApplication.timeSinceStartup;last=start;EditorApplication.update+=BakeTick;bool accepted=Lightmapping.BakeAsync();Note("BakeAsync accepted="+accepted);if(!accepted)CompleteBake(false,"Mixed bake rejected");
 }catch(Exception e){Fail(e);}}
 static void Persist(UnityEngine.Object o){EditorUtility.SetDirty(o);if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o);}
 static string PathOf(Transform t){string s=t.name;while(t.parent!=null){t=t.parent;s=t.name+"/"+s;}return s;}
 static Texture2D ReadTexture(Texture source){var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var prev=RenderTexture.active;Graphics.Blit(source,rt);RenderTexture.active=rt;var t=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);t.ReadPixels(new Rect(0,0,t.width,t.height),0,0);t.Apply();RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);return t;}
 static void FixLeaves(){var replacements=new Dictionary<Material,Material>();foreach(var r in pilot.GetComponentsInChildren<Renderer>()){var ms=r.sharedMaterials;bool changed=false;for(int i=0;i<ms.Length;i++){
  var src=ms[i];if(src==null||!src.HasProperty("_AlphaTex")||src.GetTexture("_AlphaTex")==null)continue;Material m;
  if(!replacements.TryGetValue(src,out m)){
   var rgb=ReadTexture(src.mainTexture);var alpha=ReadTexture(src.GetTexture("_AlphaTex"));var colors=rgb.GetPixels();float amin=1,amax=0;for(int j=0;j<colors.Length;j++){float a=alpha.GetPixelBilinear((j%rgb.width)/(float)rgb.width,(j/rgb.width)/(float)rgb.height).r;colors[j].a=a;amin=Mathf.Min(amin,a);amax=Mathf.Max(amax,a);}rgb.SetPixels(colors);rgb.Apply();string path=Owned+"/LeafRGBA_"+replacements.Count+".png";File.WriteAllBytes(path,rgb.EncodeToPNG());UnityEngine.Object.DestroyImmediate(rgb);UnityEngine.Object.DestroyImmediate(alpha);AssetDatabase.ImportAsset(path);
   var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=true;ti.mipmapEnabled=true;ti.SaveAndReimport();
   m=new Material(Shader.Find("Standard"));m.name="BakedLeaf_"+replacements.Count;m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);m.color=src.HasProperty("_Color")?src.color:Color.white;m.SetFloat("_Mode",1);m.SetFloat("_Cutoff",.32f);m.SetOverrideTag("RenderType","TransparentCutout");m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;m.doubleSidedGI=true;m.SetFloat("_Glossiness",.12f);AssetDatabase.CreateAsset(m,Owned+"/"+m.name+".mat");replacements.Add(src,m);
   Note("Leaf clone source="+AssetDatabase.GetAssetPath(src)+" color="+AssetDatabase.GetAssetPath(src.mainTexture)+" alpha="+AssetDatabase.GetAssetPath(src.GetTexture("_AlphaTex"))+" combinedAlphaRange="+amin+".."+amax+" StandardCutout MainTex alpha; visual silhouette still requires image inspection");
  }ms[i]=m;changed=true;
 }if(changed)r.sharedMaterials=ms;} }
 static void BakeTick(){try{Check();double elapsed=EditorApplication.timeSinceStartup-start;if(elapsed>480){Lightmapping.Cancel();CompleteBake(false,"480 second bake limit");return;}if(EditorApplication.timeSinceStartup-last>15){last=EditorApplication.timeSinceStartup;Note("PROGRESS seconds="+elapsed+" running="+Lightmapping.isRunning+" progress="+Lightmapping.buildProgress+" maps="+LightmapSettings.lightmaps.Length);}if(elapsed>5&&!Lightmapping.isRunning)CompleteBake(LightmapSettings.lightmaps.Length>0,"baker stopped");}catch(Exception e){Fail(e);}}
 static void CompleteBake(bool success,string reason){if(completing)return;completing=true;EditorApplication.update-=BakeTick;try{
  Note("BAKE_RESULT success="+success+" reason="+reason+" maps="+LightmapSettings.lightmaps.Length+" data="+AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset));
  if(success){EditorSceneManager.SaveScene(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),Target);Open(Target);Note("SAVED_REOPEN maps="+LightmapSettings.lightmaps.Length+" data="+AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset));if(LightmapSettings.lightmaps.Length==0)throw new Exception("Lightmaps lost on reopening");}
  else {Open(Target);Note("FAILED BAKE scene experiment discarded; original saved scene reopened. Only persistent reflection will be applied.");}
  BakeReflection();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  File.WriteAllText(output+"/implementation-saved.txt","BakeSuccess="+success+"\n"+DateTime.UtcNow.ToString("o"));Note("IMPLEMENTATION_SAVED. Cold editor capture is next.");EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 static void BakeReflection(){foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>()) {Check();p.enabled=true;p.mode=ReflectionProbeMode.Baked;p.resolution=256;p.hdr=true;p.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;
  string path=Owned+"/ParklingLot_LocalReflection.exr";bool ok=Lightmapping.BakeReflectionProbe(p,path);Note("BakeReflectionProbe="+ok+" path="+path);if(!ok)throw new Exception("Reflection bake failed");AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);p.mode=ReflectionProbeMode.Custom;p.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);if(p.customBakedTexture==null)throw new Exception("Missing cubemap");Persist(p);Note("CUSTOM_REFLECTION="+AssetDatabase.GetAssetPath(p.customBakedTexture)+" dimension="+p.customBakedTexture.dimension);}}
 public static void CaptureSaved(){try{capturing=true;Check();output=File.ReadAllText(Kit+"/Inspection~/native-bake-current.txt").Trim();Open(Target);log.Clear();Note("COLD_OPEN nativeShadows="+QualitySettings.shadows+" nativeAA="+QualitySettings.antiAliasing+" GPU="+SystemInfo.graphicsDeviceName+" noQualityWrite noRenderProbe noRuntimeCallback");Note("LightingData="+AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)+" Lightmaps="+LightmapSettings.lightmaps.Length+" probes="+(LightmapSettings.lightProbes==null?0:LightmapSettings.lightProbes.count));
 foreach(var p in pilot.GetComponentsInChildren<ReflectionProbe>())Note("Probe mode="+p.mode+" custom="+AssetDatabase.GetAssetPath(p.customBakedTexture));
 foreach(var l in pilot.GetComponentsInChildren<Light>())Note("Sun mode="+l.lightmapBakeType+" intensity="+l.intensity+" rotation="+l.transform.eulerAngles+" shadows="+l.shadows+" isBaked="+l.bakingOutput.isBaked);
 var coverage=new StringBuilder("path,index,scaleOffset\n");int mapped=0;foreach(var r in pilot.GetComponentsInChildren<MeshRenderer>()){if(r.lightmapIndex>=0&&r.lightmapIndex<LightmapSettings.lightmaps.Length)mapped++;coverage.AppendLine(PathOf(r.transform).Replace(",","_")+","+r.lightmapIndex+",\""+r.lightmapScaleOffset+"\"");}File.WriteAllText(output+"/coverage-cold.csv",coverage.ToString());Note("MappedRenderers="+mapped);Note("StaticCasterScaleZero="+pilot.GetComponentsInChildren<MeshRenderer>().Count(r=>r.scaleInLightmap==0 && r.receiveGI==ReceiveGI.Lightmaps && (GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.ContributeGI)!=0));foreach(var lm in LightmapSettings.lightmaps){if(lm.lightmapColor!=null)Note("LightmapTexture="+AssetDatabase.GetAssetPath(lm.lightmapColor)+" size="+lm.lightmapColor.width+"x"+lm.lightmapColor.height);}if(Lightmapping.TryGetLightingSettings(out var ls))Note("LightingSettings="+AssetDatabase.GetAssetPath(ls)+" mixedMode="+ls.mixedBakeMode+" environmentRealtime="+ls.realtimeEnvironmentLighting+" texelsPerM="+ls.lightmapResolution+" sampleCounts="+ls.directSampleCount+"/"+ls.indirectSampleCount+"/"+ls.environmentSampleCount);foreach(var lm in LightmapSettings.lightmaps){if(lm.lightmapColor!=null){var debug=ReadTexture(lm.lightmapColor);File.WriteAllBytes(output+"/lightmap-debug.png",debug.EncodeToPNG());UnityEngine.Object.DestroyImmediate(debug);}}File.WriteAllText(output+"/cold-open-metadata.txt",log.ToString());frame=-2;EditorApplication.update+=CaptureTick;
 }catch(Exception e){Fail(e);}}
 static void CaptureTick(){try{Check();var pos=Eye.transform.position;var rot=Eye.transform.rotation;if(frame<0)Capture("native-warmup-"+(frame+2),pos,rot,44,960,540);else if(frame==0)Capture("after-eye-native",pos,rot,44,1920,1080);else if(frame==1)Capture("after-side3m-native",pos+Vector3.right*3,rot,44,1920,1080);else if(frame==2)Capture("after-aerial-native",new Vector3(23,26,-28),Quaternion.LookRotation(new Vector3(-23,-25,30)),48,1920,1080);else if(frame<11)Capture("movement-"+(frame-3).ToString("D2"),pos+Vector3.forward*((frame-3)*.45f),rot,44,960,540);else if(frame==11)Capture("after-eye-repeat-native",pos,rot,44,1920,1080);else{EditorApplication.update-=CaptureTick;File.WriteAllText(output+"/NATIVE_CAPTURE_COMPLETE.txt","Cold-open actual native settings renders; 3 stills plus 8 sequential movement frames. Offline software render; no FPS claim.\n");EditorApplication.Exit(0);}frame++;}catch(Exception e){Fail(e);}}
 static void Capture(string name,Vector3 pos,Quaternion rot,float fov,int w,int h){var go=new GameObject("Temporary_Native_Capture"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();c.CopyFrom(Eye);c.enabled=false;c.transform.SetPositionAndRotation(pos,rot);c.fieldOfView=fov;var rt=new RenderTexture(w,h,24){antiAliasing=1};var t=new Texture2D(w,h,TextureFormat.RGB24,false);var prev=RenderTexture.active;try{rt.Create();c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();File.WriteAllBytes(output+"/"+name+".png",t.EncodeToPNG());}finally{RenderTexture.active=prev;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(go);}Debug.Log("NATIVE_CAPTURE "+name+" shadows="+QualitySettings.shadows);}
 static void Fail(Exception e){EditorApplication.update-=BakeTick;EditorApplication.update-=CaptureTick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
}
}
