using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Explicit, single-use map construction. No input/gameplay/global quality changes.
public static class CloudPilotMartPass {
 const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
 const string K="Assets/ParkingLotEnvironment/CloudPilot";
 const string W=K+"/MartPilot";
 const string Expected="96c67a7de38c7d332b1d451bc3de25c9ba68a3f60f82f7893b9bf16367195784";
 static GameObject root,old;
 static string output;
 static double started;
 static readonly List<Batch> batches=new List<Batch>();
 static readonly List<Bay> bays=new List<Bay>();
 static readonly StringBuilder notes=new StringBuilder();
 static Material asphalt,concrete,cladding,green,glass,metal,white,dark,yellow,soil;
 static Font font;
 static Camera camera;
 static ReflectionProbe reflection;
 static int cars,trees;
 [Serializable] class Bay {public string id,type;public Vector3 center;public float heading,width,length;public bool occupied;}
 [Serializable] class Manifest {public string target="Korean supermarket outdoor parking layout prototype; not photoreal approval or gameplay test";public int bayCount,parkedVehicles;public Bay[] bays;}
 static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static void Check(){if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");}
 static void Log(string s){notes.AppendLine(s);Debug.Log("MART "+s);if(output!=null)File.WriteAllText(output+"/mart-report.txt",notes.ToString());}
 static void Arg(){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-parkingMartOutput");if(i<0||i+1>=a.Length)throw new Exception("Output required");output=Path.GetFullPath(a[i+1]);Directory.CreateDirectory(output);}
 static Transform Group(string name){var g=new GameObject(name);g.transform.SetParent(root.transform,false);return g.transform;}
 static void Collider(string name,Vector3 p,Vector3 size,Quaternion q){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.SetPositionAndRotation(p,q);g.AddComponent<BoxCollider>().size=size;}
 static void Collider(string name,Vector3 p,Vector3 size){Collider(name,p,size,Quaternion.identity);}
 static Material Mat(string name,Color color,float gloss=0.15f,float metallic=0){var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Glossiness",gloss);m.SetFloat("_Metallic",metallic);AssetDatabase.CreateAsset(m,W+"/"+name+".mat");return m;}
 static Material Clone(string source,string name){var original=AssetDatabase.LoadAssetAtPath<Material>(source);if(!original)throw new Exception("Missing material "+source);var m=new Material(original){name=name};AssetDatabase.CreateAsset(m,W+"/"+name+".mat");return m;}
 sealed class Batch {
  public readonly string name;public readonly Material material;public float scale;public readonly List<Vector3> v=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> tr=new List<int>();
  public Batch(string n,Material m,float s=.35f){name=n;material=m;scale=s;batches.Add(this);}
  void Face(Vector3 c,Vector3 u,Vector3 w){int i=v.Count;v.Add(c-u*.5f-w*.5f);v.Add(c+u*.5f-w*.5f);v.Add(c+u*.5f+w*.5f);v.Add(c-u*.5f+w*.5f);uv.Add(new Vector2(0,0));uv.Add(new Vector2(u.magnitude,0));uv.Add(new Vector2(u.magnitude,w.magnitude));uv.Add(new Vector2(0,w.magnitude));tr.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
  public void Box(Vector3 c,Vector3 s){Box(c,s,Quaternion.identity);}
  public void Box(Vector3 c,Vector3 s,Quaternion q){Vector3 x=q*Vector3.right*s.x,y=q*Vector3.up*s.y,z=q*Vector3.forward*s.z;Face(c+z*.5f,x,y);Face(c-z*.5f,-x,y);Face(c+x*.5f,-z,y);Face(c-x*.5f,z,y);Face(c+y*.5f,x,-z);Face(c-y*.5f,x,z);}
  public void Ground(float x,float z,float width,float depth,float y){int first=v.Count;Face(new Vector3(x,y,z),Vector3.right*width,Vector3.back*depth);for(int i=first;i<v.Count;i++)uv[i]=new Vector2(v[i].x,v[i].z);}
  public void Beam(Vector3 a,Vector3 b,float width){Vector3 d=b-a;Box((a+b)*.5f,new Vector3(width,width,d.magnitude),Quaternion.LookRotation(d));}
  public void Save(){if(v.Count==0)return;var m=new Mesh{name=name,indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();if(scale>0){UnwrapParam u;UnwrapParam.SetDefaults(out u);u.packMargin=.025f;if(!Unwrapping.GenerateSecondaryUVSet(m,u))throw new Exception("UV2 "+name);}AssetDatabase.CreateAsset(m,W+"/Meshes/"+name+".asset");var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.AddComponent<MeshFilter>().sharedMesh=m;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveShadows=true;r.shadowCastingMode=ShadowCastingMode.TwoSided;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=scale;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.BatchingStatic);}
 }
 static void Text(string name,string text,Vector3 pos,float size,Color color,Quaternion rotation){var g=new GameObject(name);g.transform.SetParent(root.transform,false);g.transform.SetPositionAndRotation(pos,rotation);var t=g.AddComponent<TextMesh>();t.font=font;t.text=text;t.fontSize=100;t.characterSize=size/10f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;g.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
 static void PrepareMaterials(){
  asphalt=Mat("Mart_Asphalt_PBR",new Color(.7f,.7f,.69f),.08f);
  // All three textures are new owned byte-for-byte copies. Existing imports are untouched.
  foreach(string f in new[]{"clean_asphalt_diff_2k.jpg","clean_asphalt_nor_gl_2k.jpg","clean_asphalt_rough_2k.jpg"}){
   string p=W+"/Textures/"+f;var t=(TextureImporter)AssetImporter.GetAtPath(p);if(t==null)throw new Exception("Owned scan missing "+p);t.mipmapEnabled=true;t.anisoLevel=4;t.wrapMode=TextureWrapMode.Repeat;t.maxTextureSize=2048;t.sRGBTexture=f.Contains("_diff_");t.textureType=f.Contains("_nor_")?TextureImporterType.NormalMap:TextureImporterType.Default;t.isReadable=f.Contains("_rough_");t.SaveAndReimport();
  }
  asphalt.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(W+"/Textures/clean_asphalt_diff_2k.jpg");asphalt.mainTextureScale=Vector2.one/2.1f;asphalt.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(W+"/Textures/clean_asphalt_nor_gl_2k.jpg"));asphalt.EnableKeyword("_NORMALMAP");asphalt.SetFloat("_BumpScale",.6f);
  var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(W+"/Textures/clean_asphalt_rough_2k.jpg");var pixels=rough.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(0,0,0,(byte)(255-pixels[i].r));var packed=new Texture2D(rough.width,rough.height,TextureFormat.RGBA32,false,true);packed.SetPixels32(pixels);packed.Apply();string packedPath=W+"/Textures/Asphalt_MetallicSmoothness.png";File.WriteAllBytes(packedPath,packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(packed);AssetDatabase.ImportAsset(packedPath);var pi=(TextureImporter)AssetImporter.GetAtPath(packedPath);pi.sRGBTexture=false;pi.mipmapEnabled=true;pi.anisoLevel=4;pi.SaveAndReimport();asphalt.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));asphalt.SetFloat("_GlossMapScale",.6f);asphalt.EnableKeyword("_METALLICGLOSSMAP");
  concrete=Clone(K+"/Materials/Concrete.mat","Mart_Concrete");concrete.mainTextureScale=Vector2.one*.5f;
  cladding=Mat("Mart_Cladding",new Color(.68f,.66f,.61f),.25f);cladding.mainTexture=concrete.mainTexture;cladding.mainTextureScale=Vector2.one*.2f;
  green=Mat("Mart_DeepGreen",new Color(.045f,.16f,.115f),.35f,.15f);glass=Mat("Mart_ReflectiveGlass",new Color(.10f,.15f,.17f),.83f,.5f);
  metal=Mat("Mart_AnodisedMetal",new Color(.40f,.43f,.45f),.65f,.72f);white=Mat("Mart_OffwhitePaint",new Color(.78f,.76f,.68f),.08f);dark=Mat("Mart_Charcoal",new Color(.09f,.10f,.105f),.3f);
  yellow=Mat("Mart_Yellow",new Color(.72f,.45f,.055f),.15f);soil=Clone(K+"/Materials/Street_PlantingSoil.mat","Mart_PlantingSoil");
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ParkingLotEnvironment/Fonts/ParkingSigns.ttf");if(!font)throw new Exception("Korean font missing");
 }
 static void ParkingLayout(){
  // 48 perpendicular + 6 parallel + 6 angled. These are design dimensions, not a compliance certificate.
  for(int row=0;row<4;row++)for(int c=0;c<12;c++)bays.Add(new Bay{id=((char)('A'+row)).ToString()+(c+1).ToString("D2"),type="perpendicular",center=new Vector3(-14.85f+2.7f*c,0,new[]{-25f,-13f,-6.6f,5.4f}[row]),width=2.7f,length=5.2f,heading=row%2==0?0:180});
  for(int i=0;i<6;i++)bays.Add(new Bay{id="P"+(i+1).ToString("D2"),type="parallel",center=new Vector3(-26.2f,0,-25+7.5f*i),width=2.6f,length=6.5f,heading=0});
  for(int i=0;i<6;i++)bays.Add(new Bay{id="S"+(i+1).ToString("D2"),type="angled",center=new Vector3(25.2f,0,-24+5.5f*i),width=2.7f,length=5.2f,heading=-60});
  var paint=new Batch("Mart_ParkingMarkings",white,.7f);var curbs=new Batch("Mart_Curbs",concrete,.5f);var stop=new Batch("Mart_WheelStops",dark,.4f);var safety=new Batch("Mart_SafetyPaint",yellow,.4f);
  var markers=Group("ParkingSpaces_60_DesignDimensions");
  foreach(var b in bays){var q=Quaternion.Euler(0,b.heading,0);var t=new GameObject(b.id+"_"+b.type).transform;t.SetParent(markers,false);t.SetPositionAndRotation(b.center,q);
   foreach(float side in new[]{-1f,1f})paint.Box(b.center+q*new Vector3(side*b.width*.5f,.012f,0),new Vector3(.10f,.014f,b.length),q);
   paint.Box(b.center+q*new Vector3(0,.012f,-b.length*.5f),new Vector3(b.width,.014f,.10f),q);
   if(b.type=="perpendicular")foreach(float sx in new[]{-.65f,.65f}){var p=b.center+q*new Vector3(sx,.07f,-b.length*.5f+.65f);stop.Box(p,new Vector3(.52f,.14f,.15f),q);Collider("WheelStop_"+b.id,p,new Vector3(.52f,.14f,.15f),q);}
  }
  // End islands protect turning space; central median separates back-to-back rows B/C.
  foreach(float x in new[]{-18f,18f})foreach(float z in new[]{-25f,-9.8f,5.4f}){
   var p=new Vector3(x,.09f,z);curbs.Box(p,new Vector3(2.5f,.18f,5.4f));Collider("Island",p,new Vector3(2.5f,.18f,5.4f));
   new Batch("Mart_IslandSoil_"+x+"_"+z,soil,.25f).Box(new Vector3(x,.19f,z),new Vector3(2.2f,.07f,5.1f));
  }
  curbs.Box(new Vector3(0,.09f,-9.8f),new Vector3(32.4f,.18f,1.2f));Collider("CentralMedian",new Vector3(0,.09f,-9.8f),new Vector3(32.4f,.18f,1.2f));
  foreach(float x in new[]{-31f,31f}){curbs.Box(new Vector3(x,.12f,-5),new Vector3(.25f,.24f,59));Collider("PerimeterCurb",new Vector3(x,.12f,-5),new Vector3(.25f,.24f,59));}
  // Pedestrian route across the open store-front cross aisle.
  for(int i=0;i<11;i++)paint.Box(new Vector3(-3+i*.65f,.013f,18.1f),new Vector3(.35f,.015f,5.2f));
  foreach(float x in new[]{-20.5f,20.5f})foreach(float z in new[]{-19f,14f}){float dir=x<0?1:-1;var q=Quaternion.Euler(0,dir>0?0:180,0);paint.Box(new Vector3(x,.012f,z),new Vector3(.16f,.014f,1.6f),q);paint.Box(new Vector3(x-.32f,.012f,z+dir*.5f),new Vector3(.12f,.014f,.9f),Quaternion.Euler(0,-dir*45,0));paint.Box(new Vector3(x+.32f,.012f,z+dir*.5f),new Vector3(.12f,.014f,.9f),Quaternion.Euler(0,dir*45,0));}
  for(int i=0;i<12;i++)safety.Box(new Vector3(30.5f,.014f,17+i*.55f),new Vector3(5,.015f,.16f),Quaternion.Euler(0,-35,0));
 }
 static void Store(){
  var wall=new Batch("Mart_BuildingPanels",cladding,.35f);var trim=new Batch("Mart_BuildingDarkTrim",dark,.3f);var aluminium=new Batch("Mart_Aluminium",metal,.3f);var glazing=new Batch("Mart_Glass",glass,.25f);var fascia=new Batch("Mart_GreenFascia",green,.45f);var sidewalk=new Batch("Mart_Footway",concrete,.7f);
  // 56 x 18 m single-storey market. Front faces the south parking area.
  wall.Box(new Vector3(0,4.2f,37.5f),new Vector3(56,8.4f,18));Collider("MartBuildingExterior",new Vector3(0,4.2f,37.5f),new Vector3(56,8.4f,18));
  trim.Box(new Vector3(0,8.45f,37.5f),new Vector3(56.4f,.25f,18.4f));
  for(int i=0;i<=28;i++)trim.Box(new Vector3(-28+i*2,5.3f,28.46f),new Vector3(.025f,6.1f,.04f));
  foreach(float y in new[]{3.8f,5.8f,7.8f})trim.Box(new Vector3(0,y,28.43f),new Vector3(56,.025f,.05f));
  fascia.Box(new Vector3(0,5.6f,28.21f),new Vector3(28,2.6f,.35f));
  Text("MarketName","늘봄마트",new Vector3(-5.1f,5.85f,27.99f),1.65f,new Color(.94f,.94f,.86f),Quaternion.identity);
  Text("MarketTagline","신선한 일상  ·  FRESH MARKET",new Vector3(7.1f,5.65f,27.99f),.42f,new Color(.8f,.86f,.73f),Quaternion.identity);
  // Closed glass facade and recessed-looking entrance canopy. Store interior is outside map scope.
  glazing.Box(new Vector3(0,1.9f,28.37f),new Vector3(28,3.4f,.12f));
  for(int i=0;i<=14;i++)aluminium.Box(new Vector3(-14+i*2,1.9f,28.24f),new Vector3(.075f,3.4f,.13f));
  foreach(float y in new[]{.22f,2.55f,3.6f})aluminium.Box(new Vector3(0,y,28.24f),new Vector3(28,.06f,.13f));
  trim.Box(new Vector3(0,3.85f,25.9f),new Vector3(32,.28f,5.4f));
  fascia.Box(new Vector3(0,3.97f,23.23f),new Vector3(32,.28f,.14f));
  foreach(float x in new[]{-15f,-6f,6f,15f}){aluminium.Box(new Vector3(x,1.93f,24),new Vector3(.16f,3.86f,.16f));Collider("CanopyColumn",new Vector3(x,1.93f,24),new Vector3(.18f,3.86f,.18f));}
  Text("EntrySign","출입구  →",new Vector3(1.7f,3.08f,28.10f),.4f,Color.white,Quaternion.identity);
  sidewalk.Box(new Vector3(0,.11f,26),new Vector3(58,.22f,5));Collider("MarketFootway",new Vector3(0,.11f,26),new Vector3(58,.22f,5));
  // Flush crossing ramp instead of a curb across the pedestrian entrance.
  for(int i=0;i<11;i++)aluminium.Box(new Vector3(-12.5f+i*2.5f,.6f,23.3f),new Vector3(.11f,1.0f,.11f));
  // Loading door to the right and practical rooftop plant silhouettes.
  trim.Box(new Vector3(22,2.1f,28.35f),new Vector3(6,4.2f,.15f));
  for(int i=0;i<20;i++)aluminium.Box(new Vector3(22,.30f+i*.18f,28.22f),new Vector3(5.7f,.12f,.12f));
  Text("Loading","상품 하역",new Vector3(22,4.55f,28.20f),.48f,new Color(.15f,.18f,.16f),Quaternion.identity);
  foreach(float x in new[]{-18f,18f}){aluminium.Box(new Vector3(x,9.15f,38),new Vector3(3.6f,1.2f,2.4f));trim.Box(new Vector3(x,9.77f,38),new Vector3(3.8f,.13f,2.6f));}
  CartShelter(aluminium,trim,green,new Vector3(-24,.23f,24.8f));
 }
 static void CartShelter(Batch metalBatch,Batch roof,Material panel,Vector3 p){
  foreach(float x in new[]{-2.6f,2.6f})foreach(float z in new[]{-1.1f,1.1f}){metalBatch.Box(p+new Vector3(x,1.15f,z),new Vector3(.07f,2.3f,.07f));Collider("CartShelterPost",p+new Vector3(x,1.15f,z),new Vector3(.09f,2.3f,.09f));}
  roof.Box(p+new Vector3(0,2.35f,0),new Vector3(5.5f,.14f,2.6f));Text("CartSign","카트 보관소",p+new Vector3(0,2.08f,-1.18f),.34f,Color.white,Quaternion.identity);
  for(int i=0;i<5;i++){Vector3 c=p+new Vector3(-1.55f+i*.7f,.03f,0);foreach(float x in new[]{-.24f,.24f}){metalBatch.Beam(c+new Vector3(x,.13f,-.4f),c+new Vector3(x,.73f,.33f),.025f);metalBatch.Beam(c+new Vector3(x,.73f,.33f),c+new Vector3(x,.94f,.39f),.025f);roof.Box(c+new Vector3(x,.08f,-.32f),new Vector3(.075f,.12f,.12f));roof.Box(c+new Vector3(x,.08f,.33f),new Vector3(.075f,.12f,.12f));}
   metalBatch.Box(c+new Vector3(0,.94f,.39f),new Vector3(.58f,.035f,.035f));
   for(int j=0;j<6;j++){float z=-.35f+j*.12f;metalBatch.Box(c+new Vector3(0,.5f,z),new Vector3(.5f,.012f,.012f));foreach(float x in new[]{-.25f,.25f})metalBatch.Box(c+new Vector3(x,.66f,z),new Vector3(.012f,.32f,.012f));}
   foreach(float y in new[]{.5f,.66f,.82f})foreach(float x in new[]{-.25f,.25f})metalBatch.Box(c+new Vector3(x,y,-.04f),new Vector3(.014f,.014f,.68f));
  }
 }
 static GameObject CloneObject(GameObject source,string name,Vector3 position,Quaternion rotation,Vector3 scale){var g=UnityEngine.Object.Instantiate(source);g.name=name;g.transform.SetParent(root.transform,false);g.transform.SetPositionAndRotation(position,rotation);g.transform.localScale=scale;g.SetActive(true);if(PrefabUtility.IsPartOfPrefabInstance(g))PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);return g;}
 static void ProbeCaster(GameObject g){foreach(var r in g.GetComponentsInChildren<MeshRenderer>(true)){r.lightmapIndex=-1;r.lightmapScaleOffset=new Vector4(1,1,0,0);r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=0;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.shadowCastingMode=ShadowCastingMode.TwoSided;GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.ReflectionProbeStatic);}foreach(var c in g.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);}
 static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>();if(rs.Length==0)throw new Exception("No visible source "+g.name);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
 static void PlaceVehicles(){
  var source=old.transform.Find("EXISTING_VEHICLES_UNAPPROVED");if(!source||source.childCount!=7)throw new Exception("Vehicle source changed");
  string[] filled={"A01","A03","A04","A07","A10","A12","B01","B02","B05","B08","B11","C02","C04","C07","C10","C12","D01","D04","D08","D11","P01","P04","P06","S01","S03","S06"};
  foreach(string id in filled){var b=bays.Single(x=>x.id==id);int index=cars%7;var src=source.GetChild(index);var g=CloneObject(src.gameObject,"Mart_Parked_"+id+"_"+src.name,b.center,Quaternion.Euler(0,b.heading+180,0),src.lossyScale);ProbeCaster(g);
   var bounds=BoundsOf(g);g.transform.position+=new Vector3(b.center.x-bounds.center.x,-bounds.min.y+.015f,b.center.z-bounds.center.z);bounds=BoundsOf(g);
   // Collision body stays within the complete source silhouette. No runtime vehicle behaviours are added.
   var c=g.AddComponent<BoxCollider>();c.center=g.transform.InverseTransformPoint(bounds.center);var local=new Bounds();bool init=false;foreach(var mf in g.GetComponentsInChildren<MeshFilter>()){if(!mf.sharedMesh)continue;var mb=mf.sharedMesh.bounds;for(int j=0;j<8;j++){Vector3 p=mb.center+Vector3.Scale(mb.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1));p=g.transform.InverseTransformPoint(mf.transform.TransformPoint(p));if(!init){local=new Bounds(p,Vector3.zero);init=true;}else local.Encapsulate(p);}}c.center=local.center;c.size=local.size;
   b.occupied=true;cars++;Log("Vehicle "+id+" source="+src.name+" bounds="+bounds.size);
  }
  var truck=old.transform.Find("DaylightPass_Context/Daylight_ProvisionalTruck");if(!truck)throw new Exception("Truck source missing");var copy=CloneObject(truck.gameObject,"Mart_Loading_GenericBoxTruck",new Vector3(26,0,19),truck.rotation,truck.lossyScale);ProbeCaster(copy);var tb=BoundsOf(copy);copy.transform.position+=new Vector3(26-tb.center.x,-tb.min.y+.015f,19-tb.center.z);tb=BoundsOf(copy);Collider("LoadingTruckCollision",tb.center,tb.size);cars++;
 }
 static void Landscape(){
  var tree=old.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Daylight_StreetTree_0");if(!tree)throw new Exception("Tree source missing");
  Vector3[] positions={new Vector3(-30,.22f,-29),new Vector3(-30,.22f,-14),new Vector3(-30,.22f,3),new Vector3(-18,.23f,-9.8f),new Vector3(18,.23f,-9.8f),new Vector3(30,.22f,-27),new Vector3(30,.22f,-10),new Vector3(30,.22f,7),new Vector3(-29,.22f,19)};
  foreach(var p in positions){var g=CloneObject(tree.gameObject,"Mart_Tree_"+trees,p,Quaternion.Euler(0,trees*71,0),tree.lossyScale*(.80f+(trees%3)*.08f));ProbeCaster(g);trees++;}
  var greenbed=new Batch("Mart_PerimeterPlanting",soil,.25f);foreach(float x in new[]{-33f,33f})greenbed.Box(new Vector3(x,.025f,-5),new Vector3(3.6f,.08f,61));
  // Keep background context distant, leaving a visible open connection to the surrounding city.
  var context=old.transform.Find("DaylightPass_Context");int n=0;foreach(string name in new[]{"ContextBuilding_14_48","ContextBuilding_-1_54","ContextBuilding_4_27"}){var src=context.Find(name);if(!src)throw new Exception("Context source missing "+name);var g=CloneObject(src.gameObject,"Mart_DistantContext_"+n,new Vector3(-47+n*44,0,79+n*6),Quaternion.identity,src.lossyScale);foreach(var r in g.GetComponentsInChildren<MeshRenderer>()){r.lightmapIndex=-1;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=0;GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);}n++;}
  var road=new Batch("Mart_OutsideAccessRoad",asphalt,.3f);road.Box(new Vector3(0,-.10f,-39),new Vector3(110,.15f,13));Collider("AccessRoad",new Vector3(0,-.10f,-39),new Vector3(110,.15f,13));
  var lamp=new Batch("Mart_ParkingLights",dark,.3f);foreach(float x in new[]{-19f,19f})foreach(float z in new[]{-29f,12f}){lamp.Box(new Vector3(x,3.4f,z),new Vector3(.13f,6.8f,.13f));lamp.Box(new Vector3(x,6.85f,z),new Vector3(2.3f,.13f,.4f));Collider("ParkingLamp",new Vector3(x,3.4f,z),new Vector3(.15f,6.8f,.15f));}
 }
 static void PracticeAnchors(){var group=Group("PracticeTargets_MapOnly_NoVehicleController");string[] ids={"B03","D06","P02","S04"};string[] types={"Forward","Reverse","Parallel","Angled"};for(int i=0;i<ids.Length;i++){var b=bays.Single(x=>x.id==ids[i]);if(b.occupied)throw new Exception("Practice target occupied");var t=new GameObject(types[i]+"_Target_"+b.id).transform;t.SetParent(group,false);t.SetPositionAndRotation(b.center+Vector3.up*.02f,Quaternion.Euler(0,b.heading,0));}var start=new GameObject("PlayerVehicleSpawn_MapOnly").transform;start.SetParent(group,false);start.SetPositionAndRotation(new Vector3(-21.9f,.05f,-31),Quaternion.identity);}
 public static void BuildAndBake(){try{
  Check();Arg();if(Hash(S)!=Expected)throw new Exception("Accepted scene changed before mart trial");if(File.Exists(W+"/MartBakeWork.unity"))throw new Exception("Single-use trial already exists; do not rebuild user edits");
  Directory.CreateDirectory(W+"/Meshes");AssetDatabase.Refresh();EditorSceneManager.OpenScene(S);var scene=SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();old=roots.Single(g=>g.name=="ParkingLot_CornerPilot_Root");if(roots.Single(g=>g.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
  Log("Before="+Expected+"; conventional 3D mart layout and representative material/lighting pass. Source models and project settings untouched.");
  var sourceCamera=old.GetComponentInChildren<Camera>();var sourceSun=old.GetComponentInChildren<Light>();if(!sourceCamera||!sourceSun)throw new Exception("Camera/sun missing");
  root=new GameObject("ParkingLot_CornerPilot_Root");old.name="ParkingLot_Neighborhood_Preserved_20261103";
  camera=CloneObject(sourceCamera.gameObject,"Mart_EyeCamera",new Vector3(-21.7f,1.65f,-25.5f),Quaternion.LookRotation(new Vector3(11,.0f,46)),Vector3.one).GetComponent<Camera>();camera.fieldOfView=58;camera.farClipPlane=240;
  var sun=CloneObject(sourceSun.gameObject,"Mart_DaylightSun",sourceSun.transform.position,sourceSun.transform.rotation,Vector3.one).GetComponent<Light>();RenderSettings.sun=sun;
  PrepareMaterials();
  for(int x=0;x<4;x++)for(int z=0;z<4;z++){var b=new Batch("Mart_AsphaltTile_"+x+"_"+z,asphalt,1);b.Ground(-24+x*16,-25+z*16,16,16,0);}
  Collider("Mart_DrivableGround",new Vector3(0,-.15f,-1),new Vector3(64,.3f,64));ParkingLayout();Store();PlaceVehicles();Landscape();PracticeAnchors();
  foreach(var b in batches)b.Save();
  old.SetActive(false);
  var probes=Group("Mart_LightProbes").gameObject.AddComponent<LightProbeGroup>();var pts=new List<Vector3>();foreach(float y in new[]{.4f,1.5f,3.5f,7f})for(int x=-30;x<=30;x+=10)for(int z=-30;z<=30;z+=10)pts.Add(new Vector3(x,y,z));probes.probePositions=pts.ToArray();
  var rp=Group("Mart_Reflection");rp.position=new Vector3(-5,2,0);reflection=rp.gameObject.AddComponent<ReflectionProbe>();reflection.mode=ReflectionProbeMode.Custom;reflection.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(K+"/SunContrastLighting/DirectionalReflection.exr");reflection.size=new Vector3(78,22,78);reflection.center=Vector3.zero;reflection.boxProjection=true;reflection.resolution=256;reflection.hdr=true;reflection.intensity=1;reflection.enabled=false;
  var settings=UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);settings.name="Mart_OnePassCPU";settings.autoGenerate=false;settings.lightmapResolution=8;settings.lightmapMaxSize=1024;settings.directSampleCount=24;settings.indirectSampleCount=24;settings.environmentSampleCount=24;settings.maxBounces=2;AssetDatabase.CreateAsset(settings,W+"/Mart.lighting");Lightmapping.lightingSettings=settings;
  File.WriteAllText(W+"/layout.json",JsonUtility.ToJson(new Manifest{bayCount=bays.Count,parkedVehicles=cars,bays=bays.ToArray()},true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,W+"/MartBakeWork.unity");
  Log("Constructed bays="+bays.Count+" occupiedBays="+bays.Count(b=>b.occupied)+" vehiclesIncludingTruck="+cars+" trees="+trees+" meshBatches="+batches.Count+". Main aisles6.8m, bay2.7x5.2m; design only. Existing neighbourhood inactive and retained.");
  started=EditorApplication.timeSinceStartup;EditorApplication.update+=BakeTick;if(!Lightmapping.BakeAsync())throw new Exception("Bake rejected");
 }catch(Exception e){Fail(e);}}
 static void BakeTick(){try{Check();double elapsed=EditorApplication.timeSinceStartup-started;if(elapsed>600){Lightmapping.Cancel();throw new Exception("One-pass bake timeout; production scene remains unchanged");}if(elapsed<5||Lightmapping.isRunning)return;EditorApplication.update-=BakeTick;if(LightmapSettings.lightmaps.Length==0)throw new Exception("Missing lightmaps");
  reflection.enabled=true;reflection.mode=ReflectionProbeMode.Baked;reflection.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;string p=W+"/MartReflection.exr";if(!Lightmapping.BakeReflectionProbe(reflection,p))throw new Exception("Reflection bake failed");AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);reflection.mode=ReflectionProbeMode.Custom;reflection.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(p);if(!reflection.customBakedTexture)throw new Exception("No baked reflection");
  EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Log("TRIAL_SAVED bakeSeconds="+elapsed+" maps="+LightmapSettings.lightmaps.Length+" quality="+QualitySettings.shadows+"/"+QualitySettings.activeColorSpace+" productionUnchanged="+(Hash(S)==Expected));if(Hash(S)!=Expected)throw new Exception("Production scene mutated");EditorApplication.Exit(0);
 }catch(Exception e){Fail(e);}}
 public static void AcceptReviewed(){try{Check();Arg();if(Hash(S)!=Expected)throw new Exception("Production changed before reviewed adoption");string work=W+"/MartBakeWork.unity";if(!File.Exists(output+"/preview/capture-complete.txt"))throw new Exception("Actual preview required");EditorSceneManager.OpenScene(work);if(LightmapSettings.lightmaps.Length==0)throw new Exception("Trial lighting missing");var roots=SceneManager.GetActiveScene().GetRootGameObjects();if(!roots.Single(g=>g.name=="ParkingLot_CornerPilot_Root").activeSelf||roots.Single(g=>g.name=="ParkingLot_Neighborhood_Preserved_20261103").activeSelf)throw new Exception("Root state invalid");EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);Log("REVIEWED_LAYOUT_ADOPTED SHA256="+Hash(S)+"; visual quality remains below photographic benchmark, gameplay and mobile FPS untested.");File.WriteAllText(output+"/adopted-sha256.txt",Hash(S));EditorApplication.Exit(0);}catch(Exception e){Fail(e);}}
 static void Fail(Exception e){EditorApplication.update-=BakeTick;Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
}
}
