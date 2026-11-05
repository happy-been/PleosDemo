using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
/// <summary>
/// Korean neighbourhood massing around the public lot: four apartment slabs, three shop blocks, the access-street
/// crossing, paving near the entry, a utility pole line, a notice board and an entry barrier.
/// Scene-local; creates assets only under Materials/KoreanContext and Meshes/KoreanContext.
/// </summary>
public static class ParkingLotKoreanContext {
    const string Root="Assets/ParkingLotEnvironment";
    const string SceneTarget="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string RootName="09_Korean_neighborhood_context";
    const string MaterialDir=Root+"/Materials/KoreanContext";
    const string MeshDir=Root+"/Meshes/KoreanContext";
    const string ApartmentTexture=Root+"/KoreanAssets/apartment_facade.png";
    const string ShopTexture=Root+"/KoreanAssets/shop_facade.png";
    const int MaxPaverBoxes=300;
    const float NumberY=38f;

    static Font font;
    static Transform parent;
    static System.Random random;
    static int paverBoxes;
    static Material aptFacade,aptWall,aptSlab,aptGlass,aptLouver,roofGreen;
    static Material shopFacade,shopWall,shopCap,shopGlass,shopFrame,awningCream,signBoard,steel,tankSteel,acUnit,acGrille;
    static Material paintWhite,paintYellow,apronConcrete,sidewalkConcrete,granite,tactile;
    static Material poleConcrete,insulator,cable,transformer,lampHead;
    static Material signBlue,signWhite,noticeGreen,paper,ink,barrierGray,railWhite,railRed;
    static Material[] pavers;

    public static void Build(Scene scene,Font signFont) {
        if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("The target scene is not loaded.");
        if(scene.path!=SceneTarget)throw new InvalidOperationException("Korean context may only be built in "+SceneTarget+" (got '"+scene.path+"').");
        if(signFont==null)throw new ArgumentNullException(nameof(signFont));
        font=signFont;
        if(!CanRender("0123456789"))throw new InvalidOperationException("The sign font cannot render digits; the tower numbers would be invisible.");
        EnsureFolder(MaterialDir);EnsureFolder(MeshDir);

        // Everything that can fail is resolved before the previous root is removed.
        var apartmentMap=LoadFacade(ApartmentTexture);
        var shopMap=LoadFacade(ShopTexture);
        CreateMaterials(apartmentMap,shopMap);
        random=new System.Random(90417);paverBoxes=0;

        foreach(var existing in scene.GetRootGameObjects())if(existing.name==RootName)UnityEngine.Object.DestroyImmediate(existing);
        var rootObject=new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootObject,scene);
        parent=rootObject.transform;
        try {
            Apartments();Shops();Street();Sidewalks();UtilityPoles();NoticeBoard();ParkingBarrier();
        } catch {
            UnityEngine.Object.DestroyImmediate(rootObject);
            throw;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("PARKINGLOT_KOREAN_CONTEXT: 4 apartment towers, 3 shop blocks, "+paverBoxes+" paving/tactile boxes. New assets confined to "+MaterialDir+" and "+MeshDir+".");
    }

    // ---------------------------------------------------------------- apartments
    static void Apartments() {
        var group=Child(parent,"Apartment_towers");
        // Rear pair faces the lot across the shops; flank pair faces the lot from beyond the side fences.
        Tower(group,101,new Vector3(-38,0,72),0);
        Tower(group,102,new Vector3(24,0,92),0);
        Tower(group,103,new Vector3(-65,0,38),-90);
        Tower(group,104,new Vector3(65,0,2),90);
    }

    static void Tower(Transform group,int number,Vector3 position,float yaw) {
        const float W=24,D=12,H=56,Floors=20;
        float hw=W/2,hd=D/2,fh=H/Floors;
        var g=new Geo();
        // Photograph maps once, edge to edge. The back is mirrored in x so it also reads left to right.
        g.Quad(aptFacade,new Vector3(-hw,0,-hd),new Vector3(hw,0,-hd),new Vector3(hw,H,-hd),new Vector3(-hw,H,-hd),Vector3.back);
        g.Quad(aptFacade,new Vector3(hw,0,hd),new Vector3(-hw,0,hd),new Vector3(-hw,H,hd),new Vector3(hw,H,hd),Vector3.forward);
        for(int s=-1;s<=1;s+=2)g.Quad(aptWall,new Vector3(s*hw,0,-hd),new Vector3(s*hw,0,hd),new Vector3(s*hw,H,hd),new Vector3(s*hw,H,-hd),new Vector3(s,0,0));
        g.Quad(roofGreen,new Vector3(-hw,H,-hd),new Vector3(hw,H,-hd),new Vector3(hw,H,hd),new Vector3(-hw,H,hd),Vector3.up);
        Parapet(g,aptWall,aptSlab,hw,hd,H,1f,.25f);

        // Floor slab nosings read as balcony strips; they wrap the end walls so the slab edge is continuous.
        for(int i=1;i<(int)Floors;i++) {
            float y=i*fh;
            g.Box(aptSlab,new Vector3(0,y,-hd-.11f),new Vector3(W,.16f,.22f));
            g.Box(aptSlab,new Vector3(0,y,hd+.11f),new Vector3(W,.16f,.22f));
            for(int s=-1;s<=1;s+=2)g.Box(aptSlab,new Vector3(s*(hw+.04f),y,0),new Vector3(.08f,.16f,D));
        }
        for(int s=-1;s<=1;s+=2) {
            g.Box(aptSlab,new Vector3(0,H-.12f,s*(hd+.14f)),new Vector3(W+.56f,.24f,.28f));
            g.Box(aptSlab,new Vector3(s*(hw+.14f),H-.12f,0),new Vector3(.28f,.24f,D));
            g.Box(aptSlab,new Vector3(0,.15f,s*(hd+.05f)),new Vector3(W+.1f,.3f,.1f));
            g.Box(aptSlab,new Vector3(s*(hw+.05f),.15f,0),new Vector3(.1f,.3f,D));
        }

        // Stair-core windows and ground-level utility louvers on the blank end walls.
        for(int s=-1;s<=1;s+=2) {
            for(int i=1;i<(int)Floors;i++) {
                float y=(i+.5f)*fh;
                if(Mathf.Abs(y-NumberY)<2.9f)continue;
                g.Box(aptSlab,new Vector3(s*(hw+.03f),y,0),new Vector3(.06f,1.3f,1.05f));
                g.Box(aptGlass,new Vector3(s*(hw+.07f),y,0),new Vector3(.02f,1.12f,.87f));
            }
            foreach(float z in new[]{-2.6f,2.6f})Louver(g,new Vector3(s*hw,1.25f,z),new Vector3(s,0,0),1.4f,1f,5);
        }

        // Roof: elevator machine room with louvers, stair head and an antenna mast.
        float roofY=H;
        g.Box(aptWall,new Vector3(-3.6f,roofY+1.7f,-1.4f),new Vector3(6.5f,3.4f,3.6f));
        g.Box(aptSlab,new Vector3(-3.6f,roofY+3.47f,-1.4f),new Vector3(6.8f,.14f,3.9f));
        Louver(g,new Vector3(-5f,roofY+2.35f,-3.2f),Vector3.back,2.1f,1.5f,6);
        Louver(g,new Vector3(-2.2f,roofY+2.35f,-3.2f),Vector3.back,2.1f,1.5f,6);
        g.Box(aptWall,new Vector3(4.8f,roofY+1.5f,-1.9f),new Vector3(4.2f,3f,3f));
        g.Box(aptSlab,new Vector3(4.8f,roofY+3.07f,-1.9f),new Vector3(4.5f,.14f,3.3f));
        g.Box(aptGlass,new Vector3(4.8f,roofY+1.05f,-3.42f),new Vector3(1f,2.1f,.05f));
        g.Cone(aptLouver,new Vector3(6.6f,roofY+3.14f,-1f),.05f,.03f,5.5f,8);
        g.Box(aptLouver,new Vector3(6.6f,roofY+7f,-1f),new Vector3(1.4f,.03f,.03f));
        g.Box(aptLouver,new Vector3(6.6f,roofY+6.2f,-1f),new Vector3(1f,.03f,.03f));

        // Ground-floor entrance depth, canopy and a small paved landing anchor the facade to its courtyard.
        foreach(float x in new[]{-6f,6f}){
            g.Box(aptSlab,new Vector3(x,1.35f,-hd-.06f),new Vector3(2.35f,2.7f,.13f));
            g.Box(aptGlass,new Vector3(x,1.35f,-hd-.14f),new Vector3(2.1f,2.48f,.04f));
            g.Box(steel,new Vector3(x,1.35f,-hd-.17f),new Vector3(.045f,2.48f,.025f));
            g.Box(aptSlab,new Vector3(x,2.86f,-hd-.8f),new Vector3(3.8f,.18f,1.8f));
            g.Box(apronConcrete,new Vector3(x,.045f,-hd-1.1f),new Vector3(3.9f,.09f,2.25f));
            for(int side=-1;side<=1;side+=2)g.Box(steel,new Vector3(x+side*1.65f,1.42f,-hd-1.5f),new Vector3(.065f,2.8f,.065f));
        }

        var tower=Emit(g,"Apartment_tower_"+number,group,position,Quaternion.Euler(0,yaw,0),true);
        var collider=tower.AddComponent<BoxCollider>();
        collider.center=new Vector3(0,H/2,0);collider.size=new Vector3(W,H,D);

        string label=number.ToString()+(CanRender("동")?"동":"");
        for(int s=-1;s<=1;s+=2)
            Text(tower.transform,label,new Vector3(s*(hw+.16f),NumberY,0),Quaternion.Euler(0,-90*s,0),2.6f,new Color(.16f,.22f,.34f));
    }

    // ---------------------------------------------------------------- shop blocks
    static void Shops() {
        var group=Child(parent,"Neighborhood_shop_blocks");
        // Front faces: +x for the west block, -x for the east block, -z for the rear block.
        Shop(group,1,"green","Shop_block_1_Dongne_Mart",new Vector3(-43,0,-7),-90,"동네마트",new Color(.13f,.42f,.24f),new Color(.07f,.34f,.16f));
        Shop(group,2,"navy","Shop_block_2_Hanbit_Realty",new Vector3(42,0,18),90,"한빛부동산",new Color(.14f,.22f,.40f),new Color(.12f,.20f,.38f));
        Shop(group,3,"red","Shop_block_3_Hangil_Snack",new Vector3(5,0,45),0,"한길분식",new Color(.60f,.18f,.14f),new Color(.55f,.12f,.09f));
    }

    static void Shop(Transform group,int index,string tag,string objectName,Vector3 position,float yaw,string signText,Color awningColor,Color signColor) {
        const float W=12,D=9,H=10.8f;
        float hw=W/2,hd=D/2;
        var awning=Mat("Korean_shop_awning_"+tag,awningColor,.12f,0,null,true);
        var g=new Geo();
        g.Quad(shopFacade,new Vector3(-hw,0,-hd),new Vector3(hw,0,-hd),new Vector3(hw,H,-hd),new Vector3(-hw,H,-hd),Vector3.back);
        g.Quad(shopWall,new Vector3(hw,0,hd),new Vector3(-hw,0,hd),new Vector3(-hw,H,hd),new Vector3(hw,H,hd),Vector3.forward);
        for(int s=-1;s<=1;s+=2)g.Quad(shopWall,new Vector3(s*hw,0,-hd),new Vector3(s*hw,0,hd),new Vector3(s*hw,H,hd),new Vector3(s*hw,H,-hd),new Vector3(s,0,0));
        g.Quad(roofGreen,new Vector3(-hw,H,-hd),new Vector3(hw,H,-hd),new Vector3(hw,H,hd),new Vector3(-hw,H,hd),Vector3.up);
        Parapet(g,shopWall,shopCap,hw,hd,H,.9f,.22f);

        // Floor bands and windows on side and rear walls.
        foreach(float y in new[]{3.6f,7.2f}) {
            g.Box(shopCap,new Vector3(0,y,hd+.03f),new Vector3(W,.14f,.06f));
            for(int s=-1;s<=1;s+=2)g.Box(shopCap,new Vector3(s*(hw+.03f),y,0),new Vector3(.06f,.14f,D));
        }
        for(int s=-1;s<=1;s+=2)foreach(float y in new[]{1.9f,5.5f,9.1f})foreach(float z in new[]{-2f,2f}) {
            g.Box(shopFrame,new Vector3(s*(hw+.03f),y,z),new Vector3(.06f,1.5f,1.3f));
            g.Box(shopGlass,new Vector3(s*(hw+.07f),y,z),new Vector3(.02f,1.3f,1.1f));
        }
        foreach(float s in new[]{-1f,1f})g.Cone(steel,new Vector3(s*5.85f,0,-hd-.06f),.045f,.045f,H-.2f,8);

        // Striped ground-floor awning with valance, arms and end triangles.
        const float attachY=2.95f,edgeY=2.5f,reach=1.3f;
        var slope=new Vector3(0,attachY-edgeY,reach);
        var slopeRot=Quaternion.LookRotation(slope);
        for(int i=0;i<11;i++) {
            var m=i%2==0?awning:awningCream;
            float x=-5f+i;
            g.Box(m,new Vector3(x,(attachY+edgeY)/2,-hd-reach/2),new Vector3(.99f,.02f,slope.magnitude),slopeRot);
            g.Box(m,new Vector3(x,edgeY-.11f,-hd-reach),new Vector3(.99f,.22f,.02f));
        }
        foreach(float x in new[]{-5.4f,0f,5.4f})g.Box(steel,new Vector3(x,(attachY+edgeY)/2-.02f,-hd-reach/2),new Vector3(.03f,.03f,slope.magnitude),slopeRot);
        for(int s=-1;s<=1;s+=2) {
            var a=new Vector3(s*5.5f,attachY,-hd);var b=new Vector3(s*5.5f,edgeY,-hd-reach);var c=new Vector3(s*5.5f,edgeY,-hd);
            g.Triangle(awning,a,b,c,new Vector3(s,0,0));
            g.Triangle(awning,a,b,c,new Vector3(-s,0,0));
        }

        // Nameplate above the awning. Skipped (not substituted) if the font lacks the glyphs.
        bool nameplate=CanRender(signText);
        if(nameplate) {
            float bw=signText.Length*.52f+1.2f;
            g.Box(steel,new Vector3(0,3.3f,-hd-.05f),new Vector3(bw+.08f,.68f,.1f));
            g.Box(signBoard,new Vector3(0,3.3f,-hd-.06f),new Vector3(bw,.6f,.12f));
        } else Debug.LogWarning("Sign font cannot render '"+signText+"'; nameplate for "+objectName+" omitted.");

        // Rooftop: water tank on a stand, AC outdoor units, stair hut, vent pipes. Layout varies per block.
        float roofY=H;
        float tx=new[]{3.0f,-3.4f,2.4f}[index-1],tz=new[]{1.4f,1.8f,1.2f}[index-1];
        foreach(float lx in new[]{-.55f,.55f})foreach(float lz in new[]{-.55f,.55f})g.Box(steel,new Vector3(tx+lx,roofY+.3f,tz+lz),new Vector3(.07f,.6f,.07f));
        g.Box(steel,new Vector3(tx,roofY+.63f,tz),new Vector3(1.4f,.06f,1.4f));
        g.Cone(tankSteel,new Vector3(tx,roofY+.66f,tz),.65f,.65f,1.5f,20);
        g.Cone(tankSteel,new Vector3(tx,roofY+2.16f,tz),.65f,.4f,.14f,20);
        float ux=new[]{-3.4f,3.0f,-3.0f}[index-1],uz=3.0f;
        g.Box(steel,new Vector3(ux+1.15f,roofY+.1f,uz),new Vector3(3.6f,.08f,.55f));
        var faceFront=Quaternion.FromToRotation(Vector3.up,Vector3.back);
        for(int k=0;k<3;k++) {
            float x=ux+k*1.15f;
            g.Box(acUnit,new Vector3(x,roofY+.52f,uz),new Vector3(.95f,.75f,.38f));
            g.Cone(acGrille,new Vector3(x,roofY+.52f,uz-.19f),.27f,.27f,.012f,16,faceFront);
        }
        g.Box(shopWall,new Vector3(index==2?3.8f:-4.2f,roofY+1.15f,-.2f),new Vector3(2.6f,2.3f,2.2f));
        g.Box(shopCap,new Vector3(index==2?3.8f:-4.2f,roofY+2.33f,-.2f),new Vector3(2.9f,.1f,2.5f));
        g.Box(shopFrame,new Vector3(index==2?3.8f:-4.2f,roofY+.95f,-1.32f),new Vector3(.9f,1.9f,.05f));
        foreach(float vx in new[]{.9f,1.4f}) {
            g.Cone(steel,new Vector3(vx,roofY,2.6f),.05f,.05f,1.3f,8);
            g.Cone(steel,new Vector3(vx,roofY+1.3f,2.6f),.09f,.03f,.1f,8);
        }

        var shop=Emit(g,objectName,group,position,Quaternion.Euler(0,yaw,0),true);
        var collider=shop.AddComponent<BoxCollider>();
        collider.center=new Vector3(0,H/2,0);collider.size=new Vector3(W,H,D);
        if(nameplate)Text(shop.transform,signText,new Vector3(0,3.3f,-hd-.132f),Quaternion.identity,.46f,signColor);
    }

    // ---------------------------------------------------------------- street
    static void Street() {
        var group=Child(parent,"Street_context");
        const float y=.0125f;
        var g=new Geo();
        // Solid double yellow centre line beside the existing dashed line (dashes occupy z=-29+-0.06).
        g.Flat(paintYellow,-68f,68f,-29.215f,-29.125f,y);
        g.Flat(paintYellow,-68f,68f,-28.875f,-28.785f,y);
        // Zebra crossing x 19.5..22.5 across the 10m street: 45cm bars, 45cm gaps, centre gap left free for the lines.
        for(int k=0;k<5;k++)foreach(int s in new[]{-1,1}) {
            float c=-29+s*(.45f+.9f*k);
            g.Flat(paintWhite,19.5f,22.5f,c-.225f,c+.225f,y);
        }
        // Stop lines one lane each side of the crossing: +x traffic uses the south lane, -x traffic the north lane.
        g.Flat(paintWhite,17.6f,18f,-33.7f,-29.4f,y);
        g.Flat(paintWhite,24f,24.4f,-28.6f,-24.3f,y);
        Emit(g,"Korean_street_markings",group,Vector3.zero,Quaternion.identity,false);

        // Concrete drive apron closing the 1m gap between road and lot across the entry (x16..26). Visual only.
        var apron=new Geo();
        apron.Box(apronConcrete,new Vector3(21,-.047f,-23.5f),new Vector3(10,.106f,1));
        for(int i=1;i<5;i++)apron.Box(sidewalkConcrete,new Vector3(16+i*2f,.0065f,-23.5f),new Vector3(.03f,.001f,1));
        Emit(apron,"Driveway_apron_concrete",group,Vector3.zero,Quaternion.identity,false);

        // Pedestrian crossing sign beside the landing, angled toward oncoming traffic and the lot.
        var sign=new Geo();
        sign.Cone(steel,Vector3.zero,.045f,.045f,2.9f,10);
        sign.Box(signBlue,new Vector3(0,2.7f,-.07f),new Vector3(.65f,.65f,.03f));
        sign.Box(signBlue,new Vector3(0,2.2f,-.07f),new Vector3(.65f,.22f,.03f));
        sign.Triangle(signWhite,new Vector3(-.23f,2.46f,-.088f),new Vector3(.23f,2.46f,-.088f),new Vector3(0,2.93f,-.088f),Vector3.back);
        var faceBack=Quaternion.FromToRotation(Vector3.up,Vector3.back);
        sign.Cone(signBlue,new Vector3(0,2.78f,-.092f),.04f,.04f,.01f,10,faceBack);
        sign.Box(signBlue,new Vector3(0,2.65f,-.092f),new Vector3(.07f,.17f,.01f));
        sign.Box(signBlue,new Vector3(-.03f,2.53f,-.092f),new Vector3(.03f,.15f,.01f),Quaternion.Euler(0,0,8));
        sign.Box(signBlue,new Vector3(.03f,2.53f,-.092f),new Vector3(.03f,.15f,.01f),Quaternion.Euler(0,0,-8));
        var signObject=Emit(sign,"Pedestrian_crossing_sign",group,new Vector3(23.3f,.12f,-34.7f),Quaternion.Euler(0,150,0),true);
        if(CanRender("횡단보도"))Text(signObject.transform,"횡단보도",new Vector3(0,2.2f,-.095f),Quaternion.identity,.1f,Color.white);
    }

    // ---------------------------------------------------------------- paving
    static void Sidewalks() {
        var group=Child(parent,"Entry_sidewalk_paving");
        const float ramp0=19.5f,ramp1=22.5f,rampDepth=.9f,tactileDepth=.3f;

        // South pavement across the street: slab with a dropped kerb at the crossing, granite kerb stones.
        var slab=new Geo();
        slab.Box(sidewalkConcrete,new Vector3(-13.25f,-.06f,-35.8f),new Vector3(65.5f,.36f,3.6f));
        slab.Box(sidewalkConcrete,new Vector3(48.25f,-.06f,-35.8f),new Vector3(51.5f,.36f,3.6f));
        slab.Box(sidewalkConcrete,new Vector3(21f,-.06f,-36.25f),new Vector3(3f,.36f,2.7f));
        Curb(slab,-46f,ramp0);Curb(slab,ramp1,74f);
        Emit(slab,"Sidewalk_slab_and_kerb_south",group,Vector3.zero,Quaternion.identity,false);

        var rampGeo=new Geo();
        float rz=-34-rampDepth;
        rampGeo.Quad(sidewalkConcrete,new Vector3(ramp0,.005f,-34),new Vector3(ramp1,.005f,-34),new Vector3(ramp1,.15f,rz),new Vector3(ramp0,.15f,rz),new Vector3(0,.9f,.145f));
        for(int s=0;s<2;s++) {
            float x=s==0?ramp0:ramp1;float n=s==0?-1:1;
            rampGeo.Triangle(sidewalkConcrete,new Vector3(x,0,-34),new Vector3(x,.15f,rz),new Vector3(x,0,rz),new Vector3(n,0,0));
        }
        Emit(rampGeo,"Dropped_kerb_ramp_crossing",group,Vector3.zero,Quaternion.identity,false);

        var south=new Geo();
        for(int j=0;j<28;j++) {
            float c=12.3f+.65f*j,x0=c-.3f,x1=c+.3f;
            bool landing=x1>ramp0&&x0<ramp1;
            for(int k=0;k<4;k++) {
                float d0=.325f+.65f*k,d1=d0+.6f;
                if(landing&&k==0)continue;
                if(landing&&k==1)d0=rampDepth+tactileDepth+.05f;
                AddPaver(south,x0,x1,-34-d1,-34-d0,.12f,k==0||k==3);
            }
        }
        Emit(south,"Sidewalk_pavers_south_individual",group,Vector3.zero,Quaternion.identity,false);

        // North side: pavers laid over the existing x26..28 footpath (top y=0.15), starting after the tactile row.
        var north=new Geo();
        for(int i=0;i<4;i++)for(int k=0;k<22;k++) {
            float cx=26.25f+.5f*i,cz=-23.4f+.5f*k;
            AddPaver(north,cx-.23f,cx+.23f,cz-.23f,cz+.23f,.15f,i==0||i==3);
        }
        Emit(north,"Sidewalk_pavers_north_footpath",group,Vector3.zero,Quaternion.identity,false);

        // Yellow warning (dotted) tactile blocks at both landings.
        var tiles=new Geo();
        for(int i=0;i<6;i++)AddTactile(tiles,26.25f+.3f*i,-23.85f,.15f);
        for(int i=0;i<10;i++)AddTactile(tiles,19.65f+.3f*i,-34-rampDepth-tactileDepth/2,.12f);
        Emit(tiles,"Tactile_paving_yellow",group,Vector3.zero,Quaternion.identity,false);
    }

    static void Curb(Geo g,float from,float to) {
        for(float x=from;x<to-.01f;x+=2f) {
            float w=Mathf.Min(2f,to-x);
            g.Box(granite,new Vector3(x+w/2,.075f,-34.09f),new Vector3(w-.04f,.15f,.18f));
        }
    }

    static void AddPaver(Geo g,float x0,float x1,float z0,float z1,float baseY,bool border) {
        CountPaving(1);
        float top=baseY+.03f+(float)(random.NextDouble()*.004-.002);
        g.Box(PickPaver(border),new Vector3((x0+x1)/2,(baseY+top)/2,(z0+z1)/2),new Vector3(x1-x0,top-baseY,z1-z0));
    }

    static void AddTactile(Geo g,float cx,float cz,float baseY) {
        CountPaving(5);
        g.Box(tactile,new Vector3(cx,baseY+.015f,cz),new Vector3(.29f,.03f,.29f));
        foreach(float dx in new[]{-.075f,.075f})foreach(float dz in new[]{-.075f,.075f})
            g.Box(tactile,new Vector3(cx+dx,baseY+.034f,cz+dz),new Vector3(.055f,.008f,.055f));
    }

    static void CountPaving(int boxes) {
        paverBoxes+=boxes;
        if(paverBoxes>MaxPaverBoxes)throw new InvalidOperationException("Entry paving exceeded the "+MaxPaverBoxes+" box budget.");
    }

    // pavers: 0 light grey, 1 mid grey, 2 dark grey, 3 brick red, 4 terracotta
    static Material PickPaver(bool border) {
        double r=random.NextDouble();
        if(border)return r<.35?pavers[3]:r<.7?pavers[4]:r<.85?pavers[0]:pavers[1];
        if(r<.1)return pavers[3];
        if(r<.16)return pavers[4];
        return r<.46?pavers[0]:r<.8?pavers[1]:pavers[2];
    }

    // ---------------------------------------------------------------- utility poles
    static void UtilityPoles() {
        var group=Child(parent,"Utility_pole_line");
        float[] xs={-40f,-4f,32f,68f};
        const float z=-37.1f,baseY=.12f;
        var poles=new Geo();
        for(int i=0;i<xs.Length;i++) {
            float x=xs[i];
            poles.Cone(poleConcrete,new Vector3(x,baseY,z),.17f,.1f,9.6f,14);
            poles.Box(steel,new Vector3(x,9.1f,z),new Vector3(.09f,.09f,1.8f));
            foreach(float dz in new[]{-.75f,.75f})poles.Cone(insulator,new Vector3(x,9.15f,z+dz),.045f,.035f,.2f,8);
            if(i==1) {
                poles.Box(steel,new Vector3(x,7.9f,z-.17f),new Vector3(.08f,.08f,.34f));
                poles.Cone(transformer,new Vector3(x,7.2f,z-.36f),.2f,.2f,.8f,14);
                poles.Cone(transformer,new Vector3(x,8f,z-.36f),.2f,.08f,.1f,14);
            }
            if(i==2) {
                poles.Box(steel,new Vector3(x,8.2f,z+.9f),new Vector3(.07f,.07f,1.8f));
                poles.Box(lampHead,new Vector3(x,8.1f,z+1.7f),new Vector3(.22f,.12f,.5f));
            }
        }
        Emit(poles,"Utility_poles",group,Vector3.zero,Quaternion.identity,true);

        // Two thin cables per span, hanging in a shallow sag.
        var cables=new Geo();
        for(int i=0;i<xs.Length-1;i++)foreach(float dz in new[]{-.75f,.75f}) {
            var points=new List<Vector3>();
            for(int n=0;n<=18;n++) {
                float t=n/18f;
                points.Add(new Vector3(Mathf.Lerp(xs[i],xs[i+1],t),9.35f-.7f*4*t*(1-t),z+dz));
            }
            cables.Tube(cable,points,.011f,5);
        }
        Emit(cables,"Utility_overhead_cables",group,Vector3.zero,Quaternion.identity,false);
    }

    // ---------------------------------------------------------------- notice board
    static void NoticeBoard() {
        var board=new Geo();
        const string title="공영주차장 이용안내";
        foreach(float x in new[]{-1.3f,1.3f}) {
            board.Box(steel,new Vector3(x,1.25f,0),new Vector3(.1f,2.5f,.1f));
            board.Box(apronConcrete,new Vector3(x,.04f,0),new Vector3(.3f,.08f,.3f));
        }
        board.Box(steel,new Vector3(0,1.7f,.02f),new Vector3(2.92f,1.52f,.08f));
        board.Box(noticeGreen,new Vector3(0,1.7f,0),new Vector3(2.8f,1.4f,.08f));
        board.Box(signBlue,new Vector3(0,2.2f,-.045f),new Vector3(2.6f,.3f,.01f));
        board.Box(paper,new Vector3(0,1.55f,-.045f),new Vector3(2.6f,.9f,.01f));
        for(int k=0;k<6;k++) {
            float w=1.1f+(float)random.NextDouble()*1.1f;
            board.Box(ink,new Vector3(-1.25f+w/2,1.9f-k*.14f,-.053f),new Vector3(w,.025f,.006f));
        }
        board.Box(steel,new Vector3(0,2.58f,-.12f),new Vector3(3.1f,.06f,.36f));
        var pos=new Vector3(-24,0,21);
        var go=Emit(board,"Public_notice_board",parent,pos,Quaternion.Euler(0,Yaw(pos,new Vector3(21,0,-23)),0),true);
        if(CanRender(title))Text(go.transform,title,new Vector3(0,2.2f,-.058f),Quaternion.identity,.17f,Color.white);
        else Debug.LogWarning("Sign font cannot render '"+title+"'; notice board title omitted.");
    }

    // ---------------------------------------------------------------- barrier
    static void ParkingBarrier() {
        var g=new Geo();
        g.Box(apronConcrete,new Vector3(0,-.025f,0),new Vector3(.6f,.15f,.5f));
        g.Box(barrierGray,new Vector3(0,.575f,0),new Vector3(.34f,1.05f,.3f));
        g.Box(railRed,new Vector3(0,.9f,-.155f),new Vector3(.36f,.1f,.012f));
        g.Box(railRed,new Vector3(0,.25f,-.155f),new Vector3(.36f,.1f,.012f));
        // Rail pivots on the housing's +x side and is held at 70 degrees, so the entry stays open.
        var dir=new Vector3(Mathf.Cos(70*Mathf.Deg2Rad),Mathf.Sin(70*Mathf.Deg2Rad),0);
        var rot=Quaternion.FromToRotation(Vector3.right,dir);
        var pivot=new Vector3(.17f,.95f,0);
        for(int k=0;k<6;k++)g.Box(k%2==0?railRed:railWhite,pivot+dir*((k+.5f)*.6f),new Vector3(.6f,.1f,.07f),rot);
        g.Box(barrierGray,pivot-dir*.2f,new Vector3(.4f,.08f,.08f),rot);
        Emit(g,"Entry_parking_barrier_raised",parent,new Vector3(18,0,-23),Quaternion.identity,true);
    }

    // ---------------------------------------------------------------- shared building helpers
    internal static void Parapet(Geo g,Material wall,Material cap,float hw,float hd,float y,float height,float t) {
        float cy=y+height/2,capY=y+height+.03f;
        g.Box(wall,new Vector3(0,cy,-hd+t/2),new Vector3(2*hw,height,t));
        g.Box(wall,new Vector3(0,cy,hd-t/2),new Vector3(2*hw,height,t));
        g.Box(cap,new Vector3(0,capY,-hd+t/2),new Vector3(2*hw+.1f,.06f,t+.1f));
        g.Box(cap,new Vector3(0,capY,hd-t/2),new Vector3(2*hw+.1f,.06f,t+.1f));
        for(int s=-1;s<=1;s+=2) {
            g.Box(wall,new Vector3(s*(hw-t/2),cy,0),new Vector3(t,height,2*(hd-t)));
            g.Box(cap,new Vector3(s*(hw-t/2),capY,0),new Vector3(t+.1f,.06f,2*(hd-t)));
        }
    }

    // Dark backing with angled slats; centre is on the wall surface, normal points out of the wall.
    static void Louver(Geo g,Vector3 center,Vector3 normal,float width,float height,int slats) {
        var rot=Quaternion.LookRotation(normal);
        g.Box(aptGlass,center+normal*.03f,new Vector3(width,height,.06f),rot);
        for(int k=0;k<slats;k++) {
            float y=-height/2+(k+.5f)*height/slats;
            g.Box(aptLouver,center+Vector3.up*y+normal*.09f,new Vector3(width-.12f,.05f,.14f),rot*Quaternion.Euler(30,0,0));
        }
    }

    static float Yaw(Vector3 from,Vector3 to) {
        var d=to-from;
        return Mathf.Atan2(-d.x,-d.z)*Mathf.Rad2Deg;
    }

    // ---------------------------------------------------------------- scene helpers
    static Transform Child(Transform p,string name) {
        var go=new GameObject(name);
        go.transform.SetParent(p,false);
        return go.transform;
    }

    internal static GameObject Emit(Geo geo,string name,Transform p,Vector3 position,Quaternion rotation,bool castShadows) {
        Material[] materials;
        var mesh=geo.Bake(name,out materials);
        var go=new GameObject(name);
        go.transform.SetParent(p,false);
        go.transform.localPosition=position;go.transform.localRotation=rotation;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials=materials;
        renderer.shadowCastingMode=castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;
        renderer.receiveShadows=true;
        renderer.lightProbeUsage=LightProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }

    // Same TextMesh recipe as the lot's other signs; ParkingLotTextDepth.Refresh() swaps the material afterwards.
    static void Text(Transform p,string content,Vector3 position,Quaternion rotation,float size,Color color) {
        var o=new GameObject("Sign_"+content);
        o.transform.SetParent(p,false);
        o.transform.localPosition=position;o.transform.localRotation=rotation;
        var t=o.AddComponent<TextMesh>();
        t.text=content;t.font=font;t.fontSize=80;t.characterSize=size/8f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        var renderer=o.GetComponent<MeshRenderer>();
        renderer.sharedMaterial=font.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    static bool CanRender(string text) {
        foreach(char c in text)if(c!=' '&&!font.HasCharacter(c))return false;
        return true;
    }

    // ---------------------------------------------------------------- materials and textures
    static void CreateMaterials(Texture2D apartmentMap,Texture2D shopMap) {
        aptFacade=Mat("Korean_apartment_facade_photo",new Color(.93f,.93f,.91f),.1f,0,apartmentMap,true);
        aptWall=Mat("Korean_apartment_wall_offwhite",new Color(.80f,.79f,.75f),.06f,0,null,true);
        aptSlab=Mat("Korean_apartment_slab_gray",new Color(.62f,.62f,.60f),.08f,0,null,true);
        aptGlass=Mat("Korean_stair_window_glass",new Color(.14f,.16f,.17f),.5f,.1f);
        aptLouver=Mat("Korean_utility_louver_gray",new Color(.46f,.48f,.47f),.3f,.5f);
        roofGreen=Mat("Korean_roof_waterproofing_green",new Color(.27f,.35f,.30f),.04f,0,null,true);

        shopFacade=Mat("Korean_shop_facade_photo",new Color(.95f,.95f,.93f),.1f,0,shopMap,true);
        shopWall=Mat("Korean_shop_side_wall_brick",SampleWallColor(ShopTexture,new Color(.52f,.37f,.31f)),.06f,0,null,true);
        shopCap=Mat("Korean_shop_parapet_cap",new Color(.64f,.62f,.58f),.06f,0,null,true);
        shopGlass=Mat("Korean_shop_window_glass",new Color(.15f,.17f,.18f),.5f,.1f);
        shopFrame=Mat("Korean_shop_window_frame",new Color(.80f,.80f,.78f),.2f,.2f);
        awningCream=Mat("Korean_shop_awning_cream",new Color(.86f,.83f,.74f),.1f,0,null,true);
        signBoard=Mat("Korean_shop_nameplate_board",new Color(.92f,.91f,.87f),.25f);
        steel=Mat("Korean_galvanized_steel",new Color(.50f,.53f,.53f),.45f,.7f);
        tankSteel=Mat("Korean_rooftop_water_tank",new Color(.70f,.72f,.72f),.55f,.55f);
        acUnit=Mat("Korean_ac_outdoor_unit",new Color(.84f,.85f,.83f),.3f);
        acGrille=Mat("Korean_ac_fan_grille",new Color(.10f,.11f,.11f),.2f);

        paintWhite=Mat("Korean_road_paint_white",new Color(.80f,.80f,.76f),.08f,0,null,true);
        paintYellow=Mat("Korean_road_paint_yellow",new Color(.86f,.66f,.12f),.08f,0,null,true);
        apronConcrete=Mat("Korean_concrete_apron",new Color(.62f,.61f,.58f),.05f,0,null,true);
        sidewalkConcrete=Mat("Korean_sidewalk_concrete",new Color(.55f,.54f,.51f),.05f,0,null,true);
        granite=Mat("Korean_granite_kerb",new Color(.66f,.66f,.65f),.12f,0,null,true);
        tactile=Mat("Korean_tactile_yellow",new Color(.88f,.70f,.08f),.25f);
        pavers=new[] {
            Mat("Korean_paver_gray_light",new Color(.60f,.60f,.58f),.08f,0,null,true),
            Mat("Korean_paver_gray_mid",new Color(.50f,.50f,.49f),.08f,0,null,true),
            Mat("Korean_paver_gray_dark",new Color(.40f,.40f,.40f),.08f,0,null,true),
            Mat("Korean_paver_brick_red",new Color(.55f,.28f,.22f),.08f,0,null,true),
            Mat("Korean_paver_terracotta",new Color(.62f,.38f,.28f),.08f,0,null,true)
        };

        poleConcrete=Mat("Korean_utility_pole_concrete",new Color(.55f,.55f,.52f),.04f,0,null,true);
        insulator=Mat("Korean_pole_insulator",new Color(.42f,.36f,.30f),.5f);
        cable=Mat("Korean_overhead_cable_black",new Color(.05f,.05f,.05f),.3f);
        transformer=Mat("Korean_pole_transformer_gray",new Color(.45f,.50f,.48f),.3f,.3f);
        lampHead=Mat("Korean_street_lamp_head",new Color(.80f,.80f,.75f),.35f);

        signBlue=Mat("Korean_sign_blue",new Color(.04f,.26f,.50f),.25f);
        signWhite=Mat("Korean_sign_white",new Color(.92f,.92f,.90f),.25f);
        noticeGreen=Mat("Korean_notice_board_green",new Color(.10f,.30f,.24f),.2f);
        paper=Mat("Korean_notice_paper",new Color(.90f,.89f,.84f),.05f,0,null,true);
        ink=Mat("Korean_notice_ink",new Color(.22f,.22f,.22f),.05f,0,null,true);
        barrierGray=Mat("Korean_barrier_housing",new Color(.72f,.72f,.70f),.3f,.3f);
        railWhite=Mat("Korean_barrier_rail_white",new Color(.90f,.90f,.88f),.3f);
        railRed=Mat("Korean_barrier_rail_red",new Color(.70f,.10f,.08f),.3f);
        ParkingLotArchitecturalSurfaces.Roof(roofGreen);
        foreach(var material in new[]{aptWall,aptSlab,shopCap,apronConcrete,sidewalkConcrete,granite})ParkingLotArchitecturalSurfaces.Concrete(material);
    }

    internal static Material Mat(string name,Color color,float smoothness,float metallic=0,Texture2D map=null,bool matte=false) {
        var shader=Shader.Find("Standard");
        if(shader==null)throw new InvalidOperationException("The Standard shader is unavailable.");
        string path=MaterialDir+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        m.color=color;
        m.mainTexture=map;m.mainTextureScale=Vector2.one;m.mainTextureOffset=Vector2.zero;
        m.SetFloat("_Mode",0);
        m.SetFloat("_SmoothnessTextureChannel",0);
        m.SetFloat("_Glossiness",smoothness);
        m.SetFloat("_Metallic",metallic);
        m.SetFloat("_SpecularHighlights",matte?0:1);
        m.SetFloat("_GlossyReflections",matte?0:1);
        if(matte){m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");}
        else{m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");m.DisableKeyword("_GLOSSYREFLECTIONS_OFF");}
        m.enableInstancing=true;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    // Generated by the parent pipeline: must exist, never replaced by a plain wall. Only these two importers are touched.
    internal static Texture2D LoadFacade(string path) {
        if(!File.Exists(path))throw new FileNotFoundException("Generated facade texture is missing: "+path,path);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null){AssetDatabase.ImportAsset(path);importer=AssetImporter.GetAtPath(path) as TextureImporter;}
        if(importer==null)throw new InvalidOperationException("Not importable as a texture: "+path);
        if(importer.textureType!=TextureImporterType.Default||!importer.sRGBTexture||importer.maxTextureSize!=4096||importer.anisoLevel!=8||!importer.mipmapEnabled
            ||importer.textureCompression!=TextureImporterCompression.CompressedHQ||importer.wrapMode!=TextureWrapMode.Clamp||importer.filterMode!=FilterMode.Trilinear) {
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
            importer.maxTextureSize=4096;importer.anisoLevel=8;importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;
            importer.SaveAndReimport();
        }
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)throw new InvalidOperationException("Texture failed to import: "+path);
        return texture;
    }

    // Average of the brighter half of the facade photo, so plain side walls match its brick hue.
    static Color SampleWallColor(string assetPath,Color fallback) {
        var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            if(!image.LoadImage(File.ReadAllBytes(assetPath)))return fallback;
            var pixels=image.GetPixels32();
            int step=Mathf.Max(1,pixels.Length/60000),count=0;
            float mean=0;
            for(int i=0;i<pixels.Length;i+=step){mean+=Luma(pixels[i]);count++;}
            if(count==0)return fallback;
            mean/=count;
            Vector3 sum=Vector3.zero;int bright=0;
            for(int i=0;i<pixels.Length;i+=step) {
                if(Luma(pixels[i])<mean)continue;
                sum+=new Vector3(pixels[i].r,pixels[i].g,pixels[i].b);bright++;
            }
            if(bright==0)return fallback;
            sum/=bright*255f;
            return new Color(sum.x,sum.y,sum.z,1)*.92f;
        } finally {
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static float Luma(Color32 c){return .299f*c.r+.587f*c.g+.114f*c.b;}

    static void EnsureFolder(string path) {
        if(AssetDatabase.IsValidFolder(path))return;
        int split=path.LastIndexOf('/');
        string up=path.Substring(0,split);
        EnsureFolder(up);
        AssetDatabase.CreateFolder(up,path.Substring(split+1));
    }

    // ---------------------------------------------------------------- mesh accumulation
    /// <summary>Flat-shaded geometry grouped into one submesh per shared material, baked to a single mesh asset.</summary>
    internal sealed class Geo {
        readonly List<Vector3> verts=new List<Vector3>();
        readonly List<Vector3> normals=new List<Vector3>();
        readonly List<Vector2> uvs=new List<Vector2>();
        readonly List<Material> slots=new List<Material>();
        readonly List<List<int>> tris=new List<List<int>>();
        static readonly Vector3[] BoxAxes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};

        int Slot(Material m) {
            int i=slots.IndexOf(m);
            if(i>=0)return i;
            slots.Add(m);tris.Add(new List<int>());
            return slots.Count-1;
        }

        int Vert(Vector3 p,Vector3 n,Vector2 uv) {
            verts.Add(p);normals.Add(n);uvs.Add(uv);
            return verts.Count-1;
        }

        // Winding is corrected against the supplied normals, so callers never need to think about it.
        void Tri(int slot,int a,int b,int c) {
            var face=Vector3.Cross(verts[b]-verts[a],verts[c]-verts[a]);
            if(face.sqrMagnitude<1e-12f)return;
            if(Vector3.Dot(face,normals[a]+normals[b]+normals[c])<0){int t=b;b=c;c=t;}
            tris[slot].Add(a);tris[slot].Add(b);tris[slot].Add(c);
        }

        public void Quad(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n) {
            Quad(m,a,b,c,d,n,new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1));
        }

        public void Quad(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud) {
            int s=Slot(m);n=n.normalized;
            int i=Vert(a,n,ua);Vert(b,n,ub);Vert(c,n,uc);Vert(d,n,ud);
            Tri(s,i,i+1,i+2);Tri(s,i,i+2,i+3);
        }

        public void Triangle(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 n) {
            int s=Slot(m);n=n.normalized;
            int i=Vert(a,n,new Vector2(0,0));Vert(b,n,new Vector2(1,0));Vert(c,n,new Vector2(.5f,1));
            Tri(s,i,i+1,i+2);
        }

        public void Flat(Material m,float x0,float x1,float z0,float z1,float y) {
            Quad(m,new Vector3(x0,y,z0),new Vector3(x1,y,z0),new Vector3(x1,y,z1),new Vector3(x0,y,z1),Vector3.up);
        }

        public void Box(Material m,Vector3 center,Vector3 size) {Box(m,center,size,Quaternion.identity);}

        public void Box(Material m,Vector3 center,Vector3 size,Quaternion rot) {
            Vector3 h=size*.5f;
            foreach(var axis in BoxAxes) {
                Vector3 u=Mathf.Abs(axis.y)>.5f?Vector3.right:Vector3.up;
                Vector3 w=Vector3.Cross(axis,u);
                Vector3 o=axis*Mathf.Abs(Vector3.Dot(h,axis));
                u*=Mathf.Abs(Vector3.Dot(h,u));w*=Mathf.Abs(Vector3.Dot(h,w));
                Quad(m,center+rot*(o-u-w),center+rot*(o+u-w),center+rot*(o+u+w),center+rot*(o-u+w),rot*axis);
            }
        }

        public void Cone(Material m,Vector3 baseCenter,float r0,float r1,float height,int segments) {Cone(m,baseCenter,r0,r1,height,segments,Quaternion.identity);}

        public void Cone(Material m,Vector3 baseCenter,float r0,float r1,float height,int segments,Quaternion rot) {
            int s=Slot(m),first=verts.Count;
            for(int i=0;i<=segments;i++) {
                float a=i*Mathf.PI*2/segments;
                var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var n=rot*(dir*height+Vector3.up*(r0-r1)).normalized;
                float u=(float)i/segments;
                Vert(baseCenter+rot*(dir*r0),n,new Vector2(u,0));
                Vert(baseCenter+rot*(dir*r1+Vector3.up*height),n,new Vector2(u,1));
            }
            for(int i=0;i<segments;i++) {
                int k=first+i*2;
                Tri(s,k,k+1,k+2);Tri(s,k+1,k+3,k+2);
            }
            Cap(s,baseCenter,r0,rot,Vector3.down,segments);
            Cap(s,baseCenter+rot*(Vector3.up*height),r1,rot,Vector3.up,segments);
        }

        void Cap(int s,Vector3 center,float radius,Quaternion rot,Vector3 axis,int segments) {
            if(radius<=0)return;
            var n=rot*axis;
            int c=Vert(center,n,new Vector2(.5f,.5f));
            for(int i=0;i<segments;i++) {
                float a=i*Mathf.PI*2/segments;
                Vert(center+rot*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),n,new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f));
            }
            for(int i=0;i<segments;i++)Tri(s,c,c+1+i,c+1+(i+1)%segments);
        }

        public void Tube(Material m,IList<Vector3> points,float radius,int sides) {
            int s=Slot(m),first=verts.Count;
            for(int i=0;i<points.Count;i++) {
                var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;
                var side=Vector3.Cross(tangent,Vector3.up);
                if(side.sqrMagnitude<1e-4f)side=Vector3.Cross(tangent,Vector3.right);
                side.Normalize();
                var up=Vector3.Cross(side,tangent);
                for(int k=0;k<sides;k++) {
                    float a=k*Mathf.PI*2/sides;
                    var n=side*Mathf.Cos(a)+up*Mathf.Sin(a);
                    Vert(points[i]+n*radius,n,new Vector2((float)k/sides,(float)i/(points.Count-1)));
                }
            }
            for(int i=0;i<points.Count-1;i++)for(int k=0;k<sides;k++) {
                int a=first+i*sides+k,b=first+i*sides+(k+1)%sides;
                Tri(s,a,b,b+sides);Tri(s,a,b+sides,a+sides);
            }
        }

        /// <summary>Writes (or overwrites in place) Meshes/KoreanContext/{name}.asset; the mesh is named after the file.</summary>
        public Mesh Bake(string name,out Material[] materials) {
            var used=new List<int>();
            for(int i=0;i<slots.Count;i++)if(tris[i].Count>0)used.Add(i);
            if(used.Count==0)throw new InvalidOperationException("No geometry for mesh '"+name+"'.");
            string path=MeshDir+"/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created=mesh==null;
            if(created)mesh=new Mesh();else mesh.Clear();
            mesh.name=name;
            mesh.indexFormat=verts.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);
            mesh.subMeshCount=used.Count;
            materials=new Material[used.Count];
            for(int i=0;i<used.Count;i++) {
                mesh.SetTriangles(tris[used[i]],i,false);
                materials[i]=slots[used[i]];
            }
            mesh.RecalculateBounds();mesh.RecalculateTangents();mesh.UploadMeshData(false);
            if(created)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }
    }
}
}
