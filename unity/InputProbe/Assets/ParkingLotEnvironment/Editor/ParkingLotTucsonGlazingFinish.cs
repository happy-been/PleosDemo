using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Splits the eight cabin panes of every Tucson glass renderer into their own tightly bounded renderers so transparent
// sorting works per pane. Only triangles are regrouped: every indexed corner keeps its exact position/normal/UV/tangent.
// Sources, prefabs and materials are read only; derivative meshes are shared by all six cars and the scene is not saved here.
public static class ParkingLotTucsonGlazingFinish {
    const string Root="Assets/ParkingLotEnvironment",Derived=Root+"/Meshes/TucsonGlazing",SceneTarget="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string AnchorName="Glazing_probe_anchor",PanePrefix="Sorted_cabin_glass_";
    const int PaneCount=8,ComponentCount=21,CarCount=6;
    static readonly string[] Keys={"hyundai-tucson","hyundai-tucson-lod1"},Tags={"high","low"};
    // High model uses cm-like local coordinates (lossyScale of its glass); the distant model is already in metres.
    static readonly float[] Units={.00999491848f,1f};
    static readonly string[] PaneNames=Enumerable.Range(0,PaneCount).Select(i=>PanePrefix+i.ToString("D2")).ToArray();

    sealed class Level{public Mesh Lens;public Mesh[] Panes;public int SourceTris,SourceVertices,LensTris,PaneTris;}
    sealed class Slot{public int Level;public MeshRenderer Glass;public MeshFilter Filter;public Material Material;public Transform Anchor;public bool Explicit,Fresh;public Transform[] Panes=new Transform[PaneCount];}
    sealed class Car{public Transform Root;public LODGroup Group;public Slot[] Slots=new Slot[2];public int[] Before=new int[2],Added=new int[2];}

    static string Source(int l){return Root+"/RealVehicles/"+Keys[l]+"/Meshes/part_8.asset";}
    static string LensPath(int l){return Derived+"/Tucson_"+Tags[l]+"_lenses.asset";}
    static string PanePath(int l,int i){return Derived+"/Tucson_"+Tags[l]+"_cabin_glass_"+i.ToString("D2")+".asset";}
    static InvalidOperationException Fail(string who,string why){return new InvalidOperationException("Tucson glazing ("+who+"): "+why);}

    public static void Build(Scene scene){
        var cars=Collect(scene);
        var settings=cars.ToDictionary(c=>c.Root,c=>Settings(c));long triangles=cars.Sum(c=>SceneTriangles(c));var hashes=Fingerprint(cars);
        EnsureFolder();
        var levels=new Level[2];for(int l=0;l<2;l++)levels[l]=Make(l);
        foreach(var car in cars)Apply(car,levels);
        EditorSceneManager.MarkSceneDirty(scene);
        var after=Collect(scene);string summary=Check(after);
        for(int i=0;i<after.Count;i++){
            if(Settings(after[i])!=settings[after[i].Root])throw new InvalidOperationException("Tucson glazing changed transforms, colliders or LOD settings on "+after[i].Root.name);
            for(int l=0;l<2;l++)if(after[i].Before[l]!=cars[i].Before[l]+cars[i].Added[l])throw new InvalidOperationException("Tucson glazing LOD renderer count mismatch on "+after[i].Root.name);
        }
        if(after.Sum(c=>SceneTriangles(c))!=triangles)throw new InvalidOperationException("Tucson glazing changed total vehicle triangles.");
        var rehash=Fingerprint(after);if(!hashes.SequenceEqual(rehash))throw new InvalidOperationException("Tucson glazing source asset hashes changed.");
        var sb=new StringBuilder();
        sb.AppendLine(DateTime.Now.ToString("s")+" Scene="+SceneTarget+" (not saved by this step)");
        sb.AppendLine(summary);sb.AppendLine("Triangles across all car renderers before="+triangles+" after="+after.Sum(c=>SceneTriangles(c)));
        sb.AppendLine("New pane renderers added this run="+cars.Sum(c=>c.Added.Sum())+"; source asset hashes unchanged ("+hashes.Count+" files)");
        for(int l=0;l<2;l++){var v=levels[l];sb.AppendLine(Tags[l]+": source "+Source(l)+" vertices="+v.SourceVertices+" triangles="+v.SourceTris+" -> lenses="+v.LensTris+" + panes="+v.PaneTris+" ("+(v.LensTris+v.PaneTris)+")");}
        sb.AppendLine("Derivative assets:");
        for(int l=0;l<2;l++){sb.AppendLine("  "+AssetDatabase.GetAssetPath(levels[l].Lens));foreach(var m in levels[l].Panes)sb.AppendLine("  "+AssetDatabase.GetAssetPath(m));}
        ParkingLotEditorPaths.EnsureOutputDirectories();
        File.WriteAllText(ParkingLotEditorPaths.Output+"/tucson-glazing-finish-audit.txt",sb.ToString());
    }

    // Read-only check of a finished scene; throws when anything is missing or inconsistent.
    public static string Validate(Scene scene){return Check(Collect(scene));}

    static string Check(List<Car> cars){
        int panes=0,groups=0;
        foreach(var car in cars){
            var lods=car.Group.GetLODs();
            foreach(var s in car.Slots){
                string who=car.Root.name;int l=s.Level;groups++;
                if(s.Fresh||s.Panes.Any(p=>p==null))throw Fail(who,"glazing is not finished for LOD "+l);
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(Source(l));if(source==null)throw Fail(who,"missing source mesh");
                if(AssetDatabase.GetAssetPath(s.Filter.sharedMesh)!=LensPath(l))throw Fail(who,"lens renderer does not use the derivative lenses");
                if(s.Glass.probeAnchor==null||(!s.Explicit&&s.Glass.probeAnchor!=s.Anchor))throw Fail(who,"lens probe anchor is wrong");
                int tris=Tris(s.Filter.sharedMesh);
                for(int i=0;i<PaneCount;i++){
                    var r=s.Panes[i].GetComponent<MeshRenderer>();var f=s.Panes[i].GetComponent<MeshFilter>();
                    if(AssetDatabase.GetAssetPath(f.sharedMesh)!=PanePath(l,i))throw Fail(who,PaneNames[i]+" uses the wrong mesh");
                    if(!r.enabled||r.sharedMaterials.Length!=1||r.sharedMaterial!=s.Material||r.probeAnchor!=s.Glass.probeAnchor||GameObjectUtility.GetStaticEditorFlags(r.gameObject)!=0)throw Fail(who,PaneNames[i]+" settings differ from the glass renderer");
                    if(Count(lods[l],r)!=1||Count(lods[1-l],r)!=0)throw Fail(who,PaneNames[i]+" must be in exactly its own LOD");
                    tris+=Tris(f.sharedMesh);panes++;
                }
                if(tris!=Tris(source))throw Fail(who,"derivative triangles "+tris+" differ from source "+Tris(source));
            }
        }
        return "Cars="+cars.Count+" glass groups="+groups+" panes="+panes+" (one LOD each)";
    }

    // Finds and validates every target without modifying anything.
    static List<Car> Collect(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!=SceneTarget)throw new InvalidOperationException("Open the saved map "+SceneTarget+" first.");
        var roots=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("SUV_bay_",StringComparison.Ordinal)&&t.name.EndsWith("_hyundai-tucson",StringComparison.Ordinal)).ToArray();
        if(roots.Length!=CarCount)throw new InvalidOperationException("Expected "+CarCount+" Tucson cars, found "+roots.Length);
        return roots.Select(InspectCar).ToList();
    }
    static Car InspectCar(Transform root){
        string who=root.name;var group=root.GetComponent<LODGroup>();
        if(group==null||group.lodCount!=2)throw Fail(who,"needs exactly two LODs");
        var lods=group.GetLODs();var distant=root.Find("Distant_model");if(distant==null)throw Fail(who,"Distant_model missing");
        var glasses=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="glass").ToArray();
        if(glasses.Length!=2)throw Fail(who,"expected two glass renderers, found "+glasses.Length);
        var car=new Car{Root=root,Group=group};
        foreach(var g in glasses){
            int m0=Count(lods[0],g),m1=Count(lods[1],g);if(m0+m1!=1)throw Fail(who,"each glass must belong to exactly one LOD");
            int l=m1;if(car.Slots[l]!=null)throw Fail(who,"two glass renderers in LOD "+l);
            if(g.transform.IsChildOf(distant)!=(l==1))throw Fail(who,"glass is in the wrong LOD for its model");
            car.Slots[l]=InspectGlass(who,g,l,lods);
        }
        for(int l=0;l<2;l++)car.Before[l]=lods[l].renderers.Length;
        return car;
    }
    static Slot InspectGlass(string who,MeshRenderer g,int l,LOD[] lods){
        var s=new Slot{Level=l,Glass=g,Filter=g.GetComponent<MeshFilter>()};
        if(EditorUtility.IsPersistent(g)||!g.gameObject.scene.IsValid())throw Fail(who,"glass is not a scene object");
        if(!g.enabled||g.GetComponent<Collider>()!=null||s.Filter==null||s.Filter.sharedMesh==null)throw Fail(who,"glass must be an enabled collider-free mesh renderer");
        var mats=g.sharedMaterials;if(mats.Length!=1||mats[0]==null)throw Fail(who,"glass needs exactly one material");s.Material=mats[0];
        var sc=g.transform.lossyScale;float a=Mathf.Abs(sc.x);
        if(float.IsNaN(a)||float.IsInfinity(a)||float.IsNaN(sc.y)||float.IsInfinity(sc.y)||float.IsNaN(sc.z)||float.IsInfinity(sc.z)||a<=0||Mathf.Abs(Mathf.Abs(sc.y)-a)>a*1e-3f||Mathf.Abs(Mathf.Abs(sc.z)-a)>a*1e-3f||Mathf.Abs(a-Units[l])>Units[l]*1e-3f)throw Fail(who,"glass lossyScale "+sc+" is not the expected uniform "+Units[l]);
        string path=AssetDatabase.GetAssetPath(s.Filter.sharedMesh);s.Fresh=path==Source(l);
        if(!s.Fresh&&path!=LensPath(l))throw Fail(who,"glass uses unexpected mesh '"+path+"'");
        foreach(Transform child in g.transform){
            if(child.name==AnchorName){if(s.Anchor!=null)throw Fail(who,"duplicate "+AnchorName);Plain(who,child,false);s.Anchor=child;}
            else if(child.name.StartsWith(PanePrefix,StringComparison.Ordinal)){
                int i=Array.IndexOf(PaneNames,child.name);if(i<0||s.Panes[i]!=null)throw Fail(who,"unexpected or duplicate child "+child.name);
                Plain(who,child,true);s.Panes[i]=child;
                var r=child.GetComponent<MeshRenderer>();if(Count(lods[l],r)>1||Count(lods[1-l],r)>0)throw Fail(who,child.name+" is registered in the wrong LOD");
            }
        }
        var probe=g.probeAnchor;s.Explicit=probe!=null&&probe!=s.Anchor;
        if(s.Explicit&&s.Anchor!=null)throw Fail(who,"explicit probe anchor conflicts with "+AnchorName);
        if(!s.Fresh&&!s.Explicit&&s.Anchor==null)throw Fail(who,"original glass bounds are lost and "+AnchorName+" is missing");
        return s;
    }
    static void Plain(string who,Transform t,bool pane){
        var cs=t.GetComponents<Component>();
        bool ok=cs.All(c=>c!=null)&&t.childCount==0&&(pane?cs.Length==3&&cs.OfType<MeshFilter>().Count()==1&&cs.OfType<MeshRenderer>().Count()==1:cs.Length==1);
        if(!ok)throw Fail(who,t.name+" has unexpected components or children");
        if(pane&&(t.localPosition!=Vector3.zero||t.localRotation!=Quaternion.identity||t.localScale!=Vector3.one))throw Fail(who,t.name+" must keep an identity local transform");
    }

    static void Apply(Car car,Level[] levels){
        var lods=car.Group.GetLODs();var size=car.Group.size;var reference=car.Group.localReferencePoint;var fade=car.Group.fadeMode;var animate=car.Group.animateCrossFading;
        foreach(var s in car.Slots){
            var g=s.Glass;var level=levels[s.Level];
            // The anchor preserves the probe sampling point of the original whole-glass bounds.
            if(!s.Explicit){
                if(s.Anchor==null){var a=new GameObject(AnchorName);a.transform.SetParent(g.transform,false);s.Anchor=a.transform;}
                if(s.Fresh)s.Anchor.position=g.bounds.center;
            }
            var probe=s.Explicit?g.probeAnchor:s.Anchor;
            var list=lods[s.Level].renderers.ToList();
            for(int i=0;i<PaneCount;i++){
                if(s.Panes[i]==null){var go=new GameObject(PaneNames[i]);go.transform.SetParent(g.transform,false);go.AddComponent<MeshFilter>();go.AddComponent<MeshRenderer>();s.Panes[i]=go.transform;}
                var o=s.Panes[i].gameObject;o.layer=g.gameObject.layer;GameObjectUtility.SetStaticEditorFlags(o,0);
                o.GetComponent<MeshFilter>().sharedMesh=level.Panes[i];
                var r=o.GetComponent<MeshRenderer>();r.sharedMaterials=new[]{s.Material};Copy(g,r);r.probeAnchor=probe;
                if(!list.Contains(r)){list.Add(r);car.Added[s.Level]++;}
            }
            g.probeAnchor=probe;s.Filter.sharedMesh=level.Lens;
            lods[s.Level].renderers=list.ToArray();
        }
        if(car.Added[0]+car.Added[1]>0){
            car.Group.SetLODs(lods);car.Group.size=size;car.Group.localReferencePoint=reference;car.Group.fadeMode=fade;car.Group.animateCrossFading=animate;
        }
    }
    static void Copy(MeshRenderer from,MeshRenderer to){
        to.enabled=from.enabled;to.shadowCastingMode=from.shadowCastingMode;to.receiveShadows=from.receiveShadows;to.reflectionProbeUsage=from.reflectionProbeUsage;to.lightProbeUsage=from.lightProbeUsage;
        to.lightProbeProxyVolumeOverride=from.lightProbeProxyVolumeOverride;to.motionVectorGenerationMode=from.motionVectorGenerationMode;to.allowOcclusionWhenDynamic=from.allowOcclusionWhenDynamic;
        to.renderingLayerMask=from.renderingLayerMask;to.sortingLayerID=from.sortingLayerID;to.sortingOrder=from.sortingOrder;to.rayTracingMode=from.rayTracingMode;
    }

    // Builds, verifies and persists the nine derivative meshes of one LOD. Temporary meshes never leak.
    static Level Make(int l){
        string path=Source(l);var src=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(src==null||AssetDatabase.GetAssetPath(src)!=path)throw new InvalidOperationException("Missing source glass mesh "+path);
        var p=src.vertices;var n=src.normals;var uv=src.uv;var t=src.tangents;var ids=src.triangles;
        if(!src.isReadable||src.subMeshCount!=1||src.GetTopology(0)!=MeshTopology.Triangles||src.blendShapeCount!=0||ids.Length==0||ids.Length%3!=0||ids.Any(i=>i<0||i>=p.Length))throw new InvalidOperationException("Unsupported source glass mesh layout: "+path);
        if(n.Length!=p.Length||uv.Length!=p.Length||(t.Length!=0&&t.Length!=p.Length)||src.colors.Length!=0||src.uv2.Length!=0||src.uv3.Length!=0||src.uv4.Length!=0)throw new InvalidOperationException("Source glass needs matching normals/UV/tangents and no other channels: "+path);
        bool tangents=t.Length!=0;
        var parent=Enumerable.Range(0,p.Length).ToArray();var welded=new Dictionary<Vector3Int,int>();
        for(int i=0;i<p.Length;i++){
            var key=new Vector3Int(Mathf.RoundToInt(p[i].x*10000),Mathf.RoundToInt(p[i].y*10000),Mathf.RoundToInt(p[i].z*10000));
            if(welded.TryGetValue(key,out int first))Join(parent,i,first);else welded.Add(key,i);
        }
        for(int i=0;i<ids.Length;i+=3){Join(parent,ids[i],ids[i+1]);Join(parent,ids[i],ids[i+2]);}
        var groups=new SortedDictionary<int,List<int>>();
        for(int i=0;i<ids.Length;i+=3){int root=Find(parent,ids[i]);if(!groups.TryGetValue(root,out var list))groups[root]=list=new List<int>();list.Add(ids[i]);list.Add(ids[i+1]);list.Add(ids[i+2]);}
        if(groups.Count!=ComponentCount)throw new InvalidOperationException("Expected "+ComponentCount+" glass components in "+path+", got "+groups.Count);
        var windows=new List<List<int>>();var lenses=new List<int>();
        foreach(var group in groups.Values){if(IsWindow(group,p,Units[l]))windows.Add(group);else lenses.AddRange(group);}
        if(windows.Count!=PaneCount)throw new InvalidOperationException("Expected exactly "+PaneCount+" cabin panes in "+path+", got "+windows.Count);
        var temps=new List<Mesh>();
        try{
            for(int i=0;i<PaneCount;i++)temps.Add(Piece("Tucson_"+Tags[l]+"_cabin_glass_"+i.ToString("D2"),windows[i],p,n,uv,t));
            temps.Add(Piece("Tucson_"+Tags[l]+"_lenses",lenses,p,n,uv,t));
            var expected=Sig(src,tangents);var sum=new Dictionary<string,int>();foreach(var m in temps)Merge(sum,Sig(m,tangents));
            if(!Same(expected,sum)||temps.Sum(m=>Tris(m))!=ids.Length/3)throw new InvalidOperationException("Glazing split changed corners or triangle count in "+path);
            var saved=new Mesh[temps.Count];
            for(int i=0;i<temps.Count;i++)saved[i]=Persist(temps[i],i<PaneCount?PanePath(l,i):LensPath(l),tangents);
            var check=new Dictionary<string,int>();foreach(var m in saved)Merge(check,Sig(m,tangents));
            if(!Same(expected,check)||saved.Sum(m=>Tris(m))!=ids.Length/3)throw new InvalidOperationException("Saved glazing assets do not match "+path);
            return new Level{Panes=saved.Take(PaneCount).ToArray(),Lens=saved[PaneCount],SourceTris=ids.Length/3,SourceVertices=p.Length,PaneTris=saved.Take(PaneCount).Sum(m=>Tris(m)),LensTris=Tris(saved[PaneCount])};
        }finally{foreach(var m in temps)if(m!=null&&!AssetDatabase.Contains(m))UnityEngine.Object.DestroyImmediate(m);}
    }
    static Mesh Persist(Mesh temp,string path,bool tangents){
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null&&existing.name==temp.name&&existing.vertexCount==temp.vertexCount&&existing.subMeshCount==1&&existing.triangles.SequenceEqual(temp.triangles)&&Same(Sig(existing,tangents),Sig(temp,tangents)))return existing;
        temp.hideFlags=HideFlags.None;
        return ParkingLotMeshAsset.Save(temp,path);
    }
    static Mesh Piece(string name,List<int> tris,Vector3[] p,Vector3[] n,Vector2[] uv,Vector4[] t){
        var used=tris.Distinct().ToArray();var remap=new Dictionary<int,int>(used.Length);for(int i=0;i<used.Length;i++)remap.Add(used[i],i);
        var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};
        try{
            mesh.vertices=used.Select(i=>p[i]).ToArray();mesh.normals=used.Select(i=>n[i]).ToArray();mesh.uv=used.Select(i=>uv[i]).ToArray();
            if(t.Length==p.Length)mesh.tangents=used.Select(i=>t[i]).ToArray();
            mesh.triangles=tris.Select(i=>remap[i]).ToArray();mesh.RecalculateBounds();
        }catch{UnityEngine.Object.DestroyImmediate(mesh);throw;}
        return mesh;
    }
    static bool IsWindow(List<int> tris,Vector3[] p,float unit){
        Vector3 min=p[tris[0]],max=min;foreach(int i in tris){min=Vector3.Min(min,p[i]);max=Vector3.Max(max,p[i]);}
        var b=new Bounds();b.SetMinMax(min,max);
        return b.center.y*unit>1.25f&&b.size.y*unit>.15f&&Mathf.Max(b.size.x,b.size.z)*unit>.35f;
    }

    // Exact multiset of ordered triangles (float bit patterns): preserve connectivity and winding too.
    static Dictionary<string,int> Sig(Mesh m,bool tangents){
        var p=m.vertices;var n=m.normals;var uv=m.uv;var t=m.tangents;var sb=new StringBuilder();var result=new Dictionary<string,int>();
        var ids=m.triangles;for(int triangle=0;triangle<ids.Length;triangle+=3){
            sb.Length=0;
            for(int corner=0;corner<3;corner++){int i=ids[triangle+corner];Bits(sb,p[i].x,p[i].y,p[i].z,n[i].x,n[i].y,n[i].z,uv[i].x,uv[i].y);if(tangents)Bits(sb,t[i].x,t[i].y,t[i].z,t[i].w);sb.Append('|');}
            string key=sb.ToString();result.TryGetValue(key,out int c);result[key]=c+1;
        }
        return result;
    }
    static void Bits(StringBuilder sb,params float[] values){foreach(float v in values)sb.Append(BitConverter.ToInt32(BitConverter.GetBytes(v),0)).Append(',');}
    static void Merge(Dictionary<string,int> total,Dictionary<string,int> part){foreach(var kv in part){total.TryGetValue(kv.Key,out int c);total[kv.Key]=c+kv.Value;}}
    static bool Same(Dictionary<string,int> a,Dictionary<string,int> b){return a.Count==b.Count&&a.All(kv=>b.TryGetValue(kv.Key,out int v)&&v==kv.Value);}
    static int Tris(Mesh m){long n=0;for(int i=0;i<m.subMeshCount;i++)n+=m.GetIndexCount(i);return (int)(n/3);}
    static long SceneTriangles(Car c){return c.Root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null).Sum(f=>(long)Tris(f.sharedMesh));}
    static int Count(LOD lod,Renderer r){return lod.renderers==null?0:lod.renderers.Count(x=>x==r);}
    static int Find(int[] p,int i){while(p[i]!=i){p[i]=p[p[i]];i=p[i];}return i;}
    static void Join(int[] p,int a,int b){a=Find(p,a);b=Find(p,b);if(a!=b)p[Math.Max(a,b)]=Math.Min(a,b);}

    static void EnsureFolder(){
        if(!AssetDatabase.IsValidFolder(Root+"/Meshes"))AssetDatabase.CreateFolder(Root,"Meshes");
        if(!AssetDatabase.IsValidFolder(Derived))AssetDatabase.CreateFolder(Root+"/Meshes","TucsonGlazing");
    }
    // Everything the finish must leave alone: transforms, collider count and LOD settings (renderer lists are checked separately).
    static string Settings(Car c){
        var g=c.Group;var sb=new StringBuilder();
        sb.Append(c.Root.localToWorldMatrix.ToString("R")).Append(g.size.ToString("R")).Append(g.localReferencePoint.ToString("R")).Append(g.fadeMode).Append(g.animateCrossFading).Append(g.lodCount);
        foreach(var lod in g.GetLODs())sb.Append(lod.screenRelativeTransitionHeight.ToString("R")).Append(lod.fadeTransitionWidth.ToString("R"));
        sb.Append(c.Root.GetComponentsInChildren<Collider>(true).Length);
        foreach(var s in c.Slots)sb.Append(s.Glass.transform.localToWorldMatrix.ToString("R"));
        return sb.ToString();
    }
    static SortedDictionary<string,string> Fingerprint(List<Car> cars){
        var paths=new HashSet<string>{Source(0),Source(1),Root+"/RealVehicles/"+Keys[0]+"/"+Keys[0]+".prefab",Root+"/RealVehicles/"+Keys[1]+"/"+Keys[1]+".prefab"};
        foreach(var c in cars)foreach(var s in c.Slots){string m=AssetDatabase.GetAssetPath(s.Material);if(!string.IsNullOrEmpty(m))paths.Add(m);}
        var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
        using(var sha=SHA256.Create())foreach(string path in paths)foreach(string file in new[]{path,path+".meta"}){
            string full=ParkingLotEditorPaths.ProjectRoot+"/"+file;result[file]=File.Exists(full)?BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(full))):"missing";
        }
        return result;
    }
}
}
