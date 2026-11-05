using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Small residential blocks, not a second playable map. All geometry belongs to ParklingLot.
public static class ParkingLotNeighborhoodInfill {
    const string Root="Assets/ParkingLotEnvironment";
    const string RootName="12_Korean_residential_infill";
    public struct Building {
        public readonly Vector3 position,scale;
        public readonly float yaw;
        public readonly int finish;
        public Building(float x,float z,float rotation,float width=1,float height=1,float depth=1,int color=0){position=new Vector3(x,0,z);yaw=rotation;scale=new Vector3(width,height,depth);finish=color;}
        public Rect Footprint(float margin=0){bool turn=Mathf.Abs(Mathf.Sin(yaw*Mathf.Deg2Rad))>.5f;float w=(turn?11*scale.z:13*scale.x)+margin*2,d=(turn?13*scale.x:11*scale.z)+margin*2;return new Rect(position.x-w/2,position.z-d/2,w,d);}
    }
    public static readonly Building[] Buildings={
        new Building(-44,12,-90,1,.98f,1,0),new Building(-44,27,-90,.9f,1.05f,1,1),
        new Building(-44,49,-90,.94f,1,1,2),new Building(-44,91,-90,1,1.06f,1,0),
        new Building(-69,-14,90,1,1,1,1),new Building(-69,6,90,.95f,.94f,1,2),
        new Building(-69,69,90,1,1.08f,1,0),new Building(-69,89,90,1,.98f,1,1),
        new Building(-18,48,0,1,1,1,1),new Building(24,49,0,.94f,1.05f,1,2),
        new Building(44,-15,90,.95f,1.03f,1,2),new Building(44,0,90,.92f,.96f,1,0),
        new Building(44,47,90,1,1,1,1),new Building(44,68,90,1,1.07f,1,2),
        new Building(64,81,90,1,1,1,0),
        new Building(-68,117,0,1,.96f,1,2),new Building(-46,117,0,1,1,1,1),
        new Building(-19,117,0,1,1.07f,1,0),new Building(4,117,0,1,1,1,1),
        new Building(50,117,0,1,.94f,1,2),new Building(74,117,0,.95f,1.04f,1,0),
        new Building(-54,-47,180,1,1.04f,1,0),new Building(-29,-47,180,1,.96f,1,1),
        new Building(-5,-47,180,1,1,1,2),new Building(44,-47,180,1,1.04f,1,1)
    };
    static readonly Rect[] ExistingBuildings={
        new Rect(-50,66,24,12),new Rect(12,86,24,12),new Rect(-71,26,12,24),new Rect(59,-10,12,24),
        new Rect(-47.5f,-13,9,12),new Rect(37.5f,12,9,12),new Rect(-1,40.5f,12,9)
    };
    public static bool Occupied(Vector3 p,float margin){
        var v=new Vector2(p.x,p.z);
        foreach(var b in Buildings)if(b.Footprint(margin).Contains(v))return true;
        foreach(var r in ExistingBuildings)if(new Rect(r.x-margin,r.y-margin,r.width+2*margin,r.height+2*margin).Contains(v))return true;
        return false;
    }
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Residential infill is restricted to ParklingLot.");
        for(int i=0;i<Buildings.Length;i++){
            var a=Buildings[i].Footprint();
            foreach(var r in ExistingBuildings)if(a.Overlaps(r))throw new InvalidOperationException("New building "+i+" overlaps an existing building.");
            for(int j=0;j<i;j++)if(a.Overlaps(Buildings[j].Footprint()))throw new InvalidOperationException("Infill buildings overlap: "+i+" and "+j);
        }
        var facade=ParkingLotKoreanContext.LoadFacade(Root+"/KoreanAssets/villa_facade_gray.png");
        var brickFacade=ParkingLotKoreanContext.LoadFacade(Root+"/KoreanAssets/villa_facade_brick.png");
        var wall=ParkingLotKoreanContext.Mat("Villa_concrete_trim",new Color(.53f,.52f,.48f),.07f,0,null,true);
        var roof=ParkingLotKoreanContext.Mat("Villa_roof_waterproofing",new Color(.20f,.29f,.25f),.04f,0,null,true);
        ParkingLotArchitecturalSurfaces.Roof(roof);ParkingLotArchitecturalSurfaces.Concrete(wall);
        var metal=ParkingLotKoreanContext.Mat("Villa_roof_metal",new Color(.49f,.52f,.52f),.4f,.6f);
        var ac=ParkingLotKoreanContext.Mat("Villa_AC_housing",new Color(.69f,.68f,.61f),.1f);
        var dark=ParkingLotKoreanContext.Mat("Villa_recess_shadow",new Color(.10f,.12f,.12f),.08f);
        var finishes=new[]{
            ParkingLotKoreanContext.Mat("Villa_gray_facade",new Color(.83f,.84f,.82f),.06f,0,facade,true),
            ParkingLotKoreanContext.Mat("Villa_warm_facade",new Color(.87f,.86f,.84f),.06f,0,brickFacade,true),
            ParkingLotKoreanContext.Mat("Villa_cool_facade",new Color(.70f,.75f,.77f),.06f,0,facade,true)
        };
        foreach(var old in scene.GetRootGameObjects())if(old.name==RootName)UnityEngine.Object.DestroyImmediate(old);
        var parent=new GameObject(RootName);SceneManager.MoveGameObjectToScene(parent,scene);
        // Three reusable meshes; 25 instances share all textures and mesh data.
        var templates=new GameObject[3];
        for(int n=0;n<3;n++){
            var g=new ParkingLotKoreanContext.Geo();const float hw=6.5f,hd=5.5f,h=12.8f;
            g.Quad(finishes[n],new Vector3(-hw,0,-hd),new Vector3(hw,0,-hd),new Vector3(hw,h,-hd),new Vector3(-hw,h,-hd),Vector3.back);
            g.Quad(finishes[n],new Vector3(hw,0,hd),new Vector3(-hw,0,hd),new Vector3(-hw,h,hd),new Vector3(hw,h,hd),Vector3.forward);
            // Side elevations use the residential window strips, excluding the front entrance.
            for(int s=-1;s<=1;s+=2)for(int k=0;k<2;k++){
                float z0=k==0?-hd:0,z1=k==0?0:hd;
                float u0=k==0?.02f:.55f,u1=k==0?.47f:.98f;
                g.Quad(finishes[n],new Vector3(s*hw,0,z0),new Vector3(s*hw,0,z1),new Vector3(s*hw,h,z1),new Vector3(s*hw,h,z0),new Vector3(s,0,0),
                    new Vector2(u0,0),new Vector2(u1,0),new Vector2(u1,1),new Vector2(u0,1));
            }
            g.Flat(roof,-hw,hw,-hd,hd,h);
            ParkingLotKoreanContext.Parapet(g,wall,wall,hw,hd,h,.45f,.18f);
            // Window ledges give the photographic wall a small real silhouette and contact shadow.
            var sillHeights=n==1?new[]{1.05f,3.86f,6.93f,10.34f}:new[]{1.1f,4.05f,7.1f,10.2f};
            foreach(float y in sillHeights)foreach(float x in new[]{-3.65f,3.65f})foreach(int s in new[]{-1,1})g.Box(wall,new Vector3(x,y,s*(hd+.07f)),new Vector3(3.65f,.09f,.17f));
            for(int s=-1;s<=1;s+=2){
                g.Cone(metal,new Vector3(s*(hw-.22f),0,-hd-.035f),.035f,.035f,h,8);
                g.Box(wall,new Vector3(0,.09f,s*(hd+.38f)),new Vector3(2.5f,.18f,.8f));
                g.Box(wall,new Vector3(0,.22f,s*(hd+.16f)),new Vector3(2.3f,.26f,.4f));
                g.Box(wall,new Vector3(0,2.94f,s*(hd+.30f)),new Vector3(3.1f,.13f,.62f));
            }
            float hutX=n==1?3.8f:-3.8f;
            g.Box(wall,new Vector3(hutX,h+1.15f,2.2f),new Vector3(3,2.3f,3.2f));
            g.Box(wall,new Vector3(hutX,h+2.35f,2.2f),new Vector3(3.25f,.12f,3.45f));
            g.Box(dark,new Vector3(hutX,h+.95f,.59f),new Vector3(.9f,1.9f,.025f));
            g.Box(metal,new Vector3(hutX+.34f,h+.95f,.565f),new Vector3(.025f,.18f,.035f));
            foreach(float dx in new[]{-.38f,.38f})foreach(float dz in new[]{-.32f,.32f})
                g.Box(metal,new Vector3(-hutX+dx,h+.10f,2.8f+dz),new Vector3(.09f,.2f,.09f));
            g.Cone(metal,new Vector3(-hutX,h+.2f,2.8f),.56f,.56f,1.3f,16);
            g.Cone(metal,new Vector3(-hutX,h+1.5f,2.8f),.56f,.38f,.12f,16);
            foreach(float bandY in new[]{.35f,.85f,1.35f})g.Cone(metal,new Vector3(-hutX,h+bandY,2.8f),.572f,.572f,.025f,16);
            var fanFacing=Quaternion.FromToRotation(Vector3.up,Vector3.back);
            for(int k=0;k<3;k++){
                float x=-2+k*1.25f;
                foreach(float dx in new[]{-.35f,.35f})foreach(float dz in new[]{-.15f,.15f})
                    g.Box(metal,new Vector3(x+dx,h+.035f,-2.8f+dz),new Vector3(.08f,.07f,.08f));
                g.Box(ac,new Vector3(x,h+.42f,-2.8f),new Vector3(1,.74f,.42f));
                g.Cone(metal,new Vector3(x-.12f,h+.42f,-3.011f),.27f,.27f,.012f,16,fanFacing);
                g.Cone(dark,new Vector3(x-.12f,h+.42f,-3.025f),.246f,.246f,.008f,16,fanFacing);
                g.Cone(ac,new Vector3(x-.12f,h+.42f,-3.036f),.035f,.035f,.006f,8,fanFacing);
                for(int grille=-2;grille<=2;grille++){
                    float dy=grille*.085f,halfWidth=Mathf.Sqrt(.245f*.245f-dy*dy);
                    g.Box(metal,new Vector3(x-.12f,h+.42f+dy,-3.037f),new Vector3(halfWidth*2,.009f,.009f));
                }
                for(int vent=0;vent<5;vent++)g.Box(dark,new Vector3(x+.33f,h+.27f+vent*.075f,-3.012f),new Vector3(.16f,.012f,.008f));
            }
            g.Cone(metal,new Vector3(3,h,-1),.035f,.025f,3.3f,8);
            g.Box(metal,new Vector3(3,h+2.9f,-1),new Vector3(1.3f,.035f,.035f));
            var go=ParkingLotKoreanContext.Emit(g,"Korean_villa_type_"+n,parent.transform,Vector3.zero,Quaternion.identity,true);
            var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,h/2,0);collider.size=new Vector3(13,h,11);
            templates[n]=go;
        }
        for(int i=0;i<Buildings.Length;i++){
            var b=Buildings[i];var instance=UnityEngine.Object.Instantiate(templates[b.finish],parent.transform);
            instance.name="Residential_villa_"+(i+1).ToString("D2");instance.transform.localPosition=b.position;instance.transform.localRotation=Quaternion.Euler(0,b.yaw,0);instance.transform.localScale=b.scale;
        }
        foreach(var template in templates)UnityEngine.Object.DestroyImmediate(template);
        Debug.Log("PARKINGLOT_INFILL: "+Buildings.Length+" shared-mesh four-storey villa blocks; original buildings and playable lot preserved.");
    }
}
}
