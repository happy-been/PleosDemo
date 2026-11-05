using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Explicit local refinement only. Never runs on import or in another scene.
public static class CloudPilotStreetPass {
 const string Kit="Assets/ParkingLotEnvironment/CloudPilot";
 const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
 static int serial;
 static readonly List<string> notes=new List<string>();
 static Material Make(string name,Color c,float gloss=.2f) {
  var m=new Material(Shader.Find("Standard")){name="Street_"+name,color=c};m.SetFloat("_Glossiness",gloss);
  AssetDatabase.CreateAsset(m,Kit+"/Materials/"+m.name+".mat");return m;
 }
 static Material Clone(Material src,string name) {
  var m=new Material(src){name="Street_"+name};AssetDatabase.CreateAsset(m,Kit+"/Materials/"+m.name+".mat");return m;
 }
 static void Save(Material m){EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}
 static GameObject Box(Transform parent,string name,Vector3 p,Vector3 s,Material m) {
  var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name="Street_"+name;o.transform.SetParent(parent,false);o.transform.localPosition=p;o.transform.localScale=s;
  UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=m;return o;
 }
 static void Cylinder(Transform p,string name,Vector3 pos,float radius,float height,Material m) {
  var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);o.name="Street_"+name;o.transform.SetParent(p,false);o.transform.localPosition=pos;o.transform.localScale=new Vector3(radius*2,height*.5f,radius*2);o.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
 }
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new OperationCanceledException("STOP exists");}
 public static void ImproveAndCapture() {
  try {
   Check();if(!Application.isBatchMode)throw new InvalidOperationException("Explicit batch only");
   EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
   var roots=SceneManager.GetActiveScene().GetRootGameObjects();var pilot=roots.Single(x=>x.name=="ParkingLot_CornerPilot_Root");
   if(roots.Single(x=>x.name=="ParkingLot_V35_Preserved").activeSelf)throw new InvalidOperationException("Preserved hierarchy must remain inactive");
   if(pilot.transform.Find("StreetPass_Refinement")!=null)throw new InvalidOperationException("Already refined; review before rerunning");
   var extra=new GameObject("StreetPass_Refinement").transform;extra.SetParent(pilot.transform,false);
   var context=pilot.transform.Find("DaylightPass_Context");
   // Keep camera and normalized vehicle scale unchanged for a same-frame comparison.
   var cam=pilot.GetComponentInChildren<Camera>();notes.Add("Camera unchanged: "+cam.transform.position+" VFOV="+cam.fieldOfView);
   Vehicles(pilot.transform.Find("EXISTING_VEHICLES_UNAPPROVED"));
   var dark=Make("Charcoal",new Color(.12f,.14f,.14f));
   var aluminium=Make("Aluminium",new Color(.55f,.57f,.56f),.42f);
   var ac=Make("ACEnamel",new Color(.67f,.65f,.58f),.23f);
   var blinds=new[]{Make("BlindWarm",new Color(.40f,.38f,.32f)),Make("BlindCool",new Color(.26f,.32f,.32f)),Make("InteriorDark",new Color(.07f,.10f,.11f))};
   var tile=MakeTile();
   int b=0;
   foreach(Transform building in context) {
    if(!building.name.StartsWith("ContextBuilding_"))continue;
    var structure=building.Find("SolidStructure");Vector3 size=structure.localScale;float z=-size.z*.5f;
    var wall=structure.GetComponent<Renderer>();if(wall.sharedMaterial.name.Contains("Ivory")){wall.sharedMaterial=tile;notes.Add("Tiled facade: "+building.name);}
    int w=0;
    foreach(Transform window in building.Cast<Transform>().Where(t=>t.name=="RecessedGlazing").ToArray()) {
     var p=window.localPosition;int row=Mathf.RoundToInt((p.y-1.8f)/3.1f);
     // Different interior depth/tone, partially lowered roller blinds, and mullions.
     window.GetComponent<Renderer>().sharedMaterial=blinds[(w+b)%3];
     if(row>0 && (w+b)%3!=0) {
      float h=(w+b)%2==0?.56f:.95f;
      Box(building,"RollerBlind",new Vector3(p.x,p.y+.74f-h*.5f,z-.143f),new Vector3(1.23f,h,.018f),blinds[(w+b+1)%2]);
      for(int n=0;n<4;n++)Box(building,"BlindSlat",new Vector3(p.x,p.y+.66f-n*.12f,z-.158f),new Vector3(1.23f,.018f,.012f),aluminium);
     }
     if(row==0 && size.y<15) {
      // Larger shopfronts replace only the newly generated ground-level window group.
      window.gameObject.SetActive(false);
      Box(building,"ShopFrame",new Vector3(p.x,1.3f,z-.18f),new Vector3(2.0f,2.45f,.16f),dark);
      Box(building,"ShopGlazing",new Vector3(p.x,1.32f,z-.28f),new Vector3(1.82f,2.22f,.025f),blinds[2]);
      Box(building,"ShopMullion",new Vector3(p.x,1.3f,z-.31f),new Vector3(.055f,2.25f,.05f),aluminium);
      Box(building,"ShopTransom",new Vector3(p.x,2.12f,z-.31f),new Vector3(1.85f,.05f,.05f),aluminium);
     }
     w++;
    }
    if(size.y<15) {
     for(int floor=1;floor<Mathf.FloorToInt(size.y/3.1f);floor++) {
      float y=1.8f+floor*3.1f;
      if((floor+b)%2==0)AddAC(building,new Vector3(size.x*.29f,y-1.05f,z-.34f),ac,dark,aluminium);
     }
     Cylinder(building,"RainwaterPipe",new Vector3(-size.x*.5f+.22f,size.y*.49f,z-.15f),.045f,size.y*.96f,aluminium);
     for(int f=1;f<size.y/3.1f;f++)Box(building,"FloorJoint",new Vector3(0,f*3.1f-.05f,z-.025f),new Vector3(size.x,.035f,.055f),aluminium);
     var sign=Box(building,"ShopFascia",new Vector3(0,2.62f,z-.34f),new Vector3(size.x*.87f,.40f,.14f),b%2==0?dark:ac);
     var text=new GameObject("Street_ShopText").AddComponent<TextMesh>();text.transform.SetParent(sign.transform.parent,false);text.transform.localPosition=new Vector3(0,2.62f,z-.42f);text.anchor=TextAnchor.MiddleCenter;text.characterSize=.035f;text.fontSize=64;text.color=b%2==0?new Color(.73f,.72f,.65f):new Color(.15f,.18f,.18f);text.text=b%2==0?"동네 세탁소":"마을 부동산";
     text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ParkingLotEnvironment/Fonts/ParkingSigns.ttf");text.GetComponent<Renderer>().sharedMaterial=text.font.material;
    }
    b++;
   }
   var boundary=context.Find("RightBoundaryWall");boundary.localPosition=new Vector3(10.2f,.13f,1);boundary.localScale=new Vector3(.25f,.26f,28);
   // Actual foliage mesh instances, fitted into a low planting strip; no image cards.
   var leaf=pilot.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.Contains("leaves"));var filter=leaf.GetComponent<MeshFilter>();
   var shrubMat=Clone(leaf.sharedMaterial,"ShrubLeaves");shrubMat.color=new Color(.75f,.87f,.66f);Save(shrubMat);
   var soil=Make("PlantingSoil",new Color(.17f,.15f,.11f));Box(extra,"PlantingStrip",new Vector3(10.63f,.13f,1),new Vector3(.62f,.12f,28),soil);
   for(int i=0;i<20;i++) {
    var shrub=new GameObject("Street_RealMeshShrub_"+i);shrub.transform.SetParent(extra,false);shrub.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;shrub.AddComponent<MeshRenderer>().sharedMaterial=shrubMat;
    var bounds=filter.sharedMesh.bounds;var scale=new Vector3(.95f/bounds.size.x,.72f/bounds.size.y,1.6f/bounds.size.z);shrub.transform.localScale=scale;shrub.transform.localPosition=new Vector3(10.65f,.18f,-12.1f+i*1.37f)-Vector3.Scale(bounds.min,scale)-new Vector3(.475f,0,.8f);
   }
   var paintSrc=pilot.transform.Find("Zone_ParkingPaint").GetComponentInChildren<Renderer>().sharedMaterial;
   var paint=Clone(paintSrc,"ParkingLine");paint.color=new Color(.70f,.69f,.64f);Save(paint);
   foreach(var r in pilot.transform.Find("Zone_ParkingPaint").GetComponentsInChildren<Renderer>())r.sharedMaterial=paint;
   for(int i=0;i<10;i++)Box(extra,"RightWheelStop",new Vector3(8.05f,.075f,-11.25f+i*2.5f),new Vector3(.16f,.15f,1.55f),dark);
   // Raise sign contrast with a real border in the actual panel bounds.
   var sr=pilot.transform.Find("Zone_SignPanel").GetComponentInChildren<Renderer>();var sb=sr.bounds;float front=sb.min.z-.012f;
   var white=Make("SignBorder",new Color(.84f,.86f,.82f));
   Box(extra,"SignTop",new Vector3(sb.center.x,sb.max.y-.035f,front),new Vector3(sb.size.x-.08f,.035f,.02f),white);
   Box(extra,"SignBottom",new Vector3(sb.center.x,sb.min.y+.035f,front),new Vector3(sb.size.x-.08f,.035f,.02f),white);
   foreach(float x in new[]{sb.min.x+.035f,sb.max.x-.035f})Box(extra,"SignSide",new Vector3(x,sb.center.y,front),new Vector3(.035f,sb.size.y-.08f,.02f),white);
   foreach(var probe in pilot.GetComponentsInChildren<ReflectionProbe>()){notes.Add("Probe before: "+probe.name+" size="+probe.size+" position="+probe.transform.position+" resolution="+probe.resolution);probe.size=new Vector3(28,18,44);probe.resolution=256;probe.intensity=1;}
   Check();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath))throw new IOException("Save failed");
   File.WriteAllText(Kit+"/Inspection~/street-implementation-checkpoint.txt",string.Join("\n",notes)+"\nSaved scene. Native quality remains unchanged; real stills pending.\n");
   CloudPilotInspection.VerifyReopen();
   CloudPilotDaylightPass.CaptureSavedPilotStills();
  }catch(Exception e){Debug.LogException(e);File.WriteAllText(Kit+"/Inspection~/street-pass-failed.txt",e.ToString());EditorApplication.Exit(1);}
 }
 static void AddAC(Transform p,Vector3 pos,Material ac,Material dark,Material metal) {
  Box(p,"ACUnit",pos,new Vector3(.83f,.53f,.36f),ac);
  Box(p,"ACFanRecess",pos+new Vector3(-.14f,0,-.19f),new Vector3(.38f,.38f,.018f),dark);
  for(int i=0;i<7;i++)Box(p,"ACGrille",pos+new Vector3(-.14f,-.16f+i*.052f,-.208f),new Vector3(.39f,.014f,.018f),metal);
  Box(p,"ACBracket",pos+new Vector3(0,-.32f,.02f),new Vector3(.94f,.07f,.50f),metal);
 }
 static Material MakeTile() {
  // Authored tile pattern, explicitly not a photographed facade or generated photo.
  const int n=512;var t=new Texture2D(n,n,TextureFormat.RGB24,false);var pixels=new Color[n*n];
  for(int y=0;y<n;y++)for(int x=0;x<n;x++) {
   int tx=x/64,ty=y/32;float variation=Mathf.Sin(tx*7.13f+ty*4.19f)*.017f;float grain=Mathf.PerlinNoise(x*.45f,y*.45f)*.016f;
   bool joint=x%64<2||y%32<2;float v=joint?.49f:.77f+variation+grain;
   pixels[y*n+x]=new Color(v,v*.982f,v*.92f);
  }
  t.SetPixels(pixels);t.Apply();Directory.CreateDirectory(Kit+"/Textures");string path=Kit+"/Textures/Street_CeramicTile_Authored.png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
  var m=Make("CeramicTile",Color.white,.2f);m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);m.mainTextureScale=new Vector2(5,4);Save(m);notes.Add("Authored ceramic tile texture: "+path);return m;
 }
 static void Vehicles(Transform group) {
  int carIndex=0;
  foreach(Transform car in group) {
   var clones=new Dictionary<Material,Material>();int changed=0;
   foreach(var r in car.GetComponentsInChildren<Renderer>()) {
    var materials=r.sharedMaterials;
    for(int i=0;i<materials.Length;i++) {
     var src=materials[i];if(src==null)continue;string name=src.name.ToLowerInvariant();
     bool glass=src.shader.name=="ParkingLot/AutomotiveGlass";
     bool rubber=name.Contains("tyre")||name.Contains("tire")||name.Contains("rubber")||name.Contains("black_plastic");
     bool body=name.Contains("varnish")||name.EndsWith("_paint")||name=="000_body";
     // Azera has individual body-panel materials with identical known enamel color.
     if(car.name.Contains("azera") && Mathf.Abs(src.color.r-.8803049f)<.001f && Mathf.Abs(src.color.g-.8803049f)<.001f)body=true;
     if(!glass&&!rubber&&!body)continue;
     Material m;if(!clones.TryGetValue(src,out m)) {
      m=Clone(src,"Vehicle_"+carIndex+"_"+(serial++));
      if(glass){m.color=new Color(.075f,.115f,.13f,.78f);m.SetFloat("_Glossiness",.96f);}
      if(rubber){m.color=new Color(.035f,.039f,.04f);m.SetFloat("_Glossiness",.19f);}
      if(body){Color[] palette={new Color(.70f,.71f,.69f),new Color(.47f,.50f,.52f),new Color(.46f,.48f,.47f),new Color(.68f,.69f,.67f),new Color(.62f,.64f,.65f),new Color(.19f,.22f,.24f),new Color(.62f,.64f,.63f)};m.color=palette[carIndex%palette.Length];m.SetFloat("_Glossiness",.76f);m.SetFloat("_Metallic",.22f);}
      Save(m);clones.Add(src,m);
     }
     materials[i]=m;changed++;
    }
    r.sharedMaterials=materials;
   }
   notes.Add(car.name+": cloned material slots="+changed+". Original mesh/normals unchanged.");carIndex++;
  }
 }
}
}
