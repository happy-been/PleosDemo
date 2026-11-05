using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
/// <summary>
/// Small Korean public-lot site details: a CCTV pole cluster, a management booth, a three-stream waste sorting bank
/// and an entry-lane speed hump. Each prop is one combined mesh with submeshes; only the booth and the hump get colliders.
/// Scene-local; creates assets only under Materials/SiteDetails and Meshes/SiteDetails.
/// </summary>
public static class ParkingLotSiteDetails {
    const string Root="Assets/ParkingLotEnvironment";
    const string SceneTarget="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string RootName="11_Korean_public_lot_details";
    const string MaterialDir=Root+"/Materials/SiteDetails";
    const string MeshDir=Root+"/Meshes/SiteDetails";
    const int MaxTriangles=12000;
    const int MaxRenderers=70;

    static readonly Vector3 PolePosition=new Vector3(24f,0,20f);
    static readonly Vector3 BoothPosition=new Vector3(23.6f,0,-14f);
    static readonly Vector3 BinPosition=new Vector3(-24f,0,20f);
    static readonly Vector3 HumpPosition=new Vector3(21f,0,-19.2f);

    static Font font;
    static Transform parent;
    static int triangleCount;
    // CCTV pole
    static Material zinc,steel,concreteFoot,camWhite,camBlack,bezel,lensGlass,irLens,junctionGray,conduit,cableBlack;
    // booth
    static Material wallWhite,wallBand,alu,tint,interiorDark,roofTop,roofFascia,plinth,signBoard,doorPanel,doorSteel;
    // waste bank
    static Material binBody,binLid,binDark,signYellow,signBlue,signGreen;
    // speed hump
    static Material humpYellow,humpBlack;

    public static void Build(Scene scene,Font signFont) {
        if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("The target scene is not loaded.");
        if(scene.path!=SceneTarget)throw new InvalidOperationException("Site details may only be built in "+SceneTarget+" (got '"+scene.path+"').");
        if(signFont==null)throw new ArgumentNullException(nameof(signFont));
        font=signFont;
        EnsureFolder(MaterialDir);EnsureFolder(MeshDir);

        // Everything that can fail is resolved before the previous root is removed.
        CreateMaterials();
        triangleCount=0;

        foreach(var existing in scene.GetRootGameObjects())if(existing.name==RootName)UnityEngine.Object.DestroyImmediate(existing);
        var rootObject=new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootObject,scene);
        parent=rootObject.transform;
        int renderers;
        try {
            CctvPole();Booth();WasteBank();SpeedHump();
            renderers=rootObject.GetComponentsInChildren<Renderer>(true).Length;
            if(triangleCount>=MaxTriangles||renderers>=MaxRenderers)
                throw new InvalidOperationException("Site details exceed the budget: "+triangleCount+" triangles, "+renderers+" renderers.");
        } catch {
            UnityEngine.Object.DestroyImmediate(rootObject);
            throw;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("PARKINGLOT_SITE_DETAILS: CCTV pole, management booth, 3-stream waste bank, entry speed hump; "+triangleCount+" triangles, "+renderers
            +" renderers. New assets confined to "+MaterialDir+" and "+MeshDir+".");
    }

    // ---------------------------------------------------------------- CCTV pole cluster
    static void CctvPole() {
        var g=new Geo();
        // Concrete footing, base plate, anchor bolts with nuts, and a flared base cover.
        g.Cone(concreteFoot,new Vector3(0,-.03f,0),.20f,.19f,.18f,20);
        g.Box(zinc,new Vector3(0,.157f,0),new Vector3(.27f,.014f,.27f));
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2) {
            var bolt=new Vector3(sx*.105f,.164f,sz*.105f);
            g.Cone(steel,bolt,.007f,.007f,.05f,8);
            g.Cone(steel,bolt+Vector3.up*.006f,.0125f,.0125f,.013f,6);
        }
        g.Cone(zinc,new Vector3(0,.164f,0),.12f,.0785f,.15f,16);
        // Two-section tapered galvanized mast, slip-joint collar, cap and finial: 5.0 m overall.
        g.Cone(zinc,new Vector3(0,.314f,0),.0785f,.065f,2.136f,16);
        g.Cone(zinc,new Vector3(0,2.42f,0),.070f,.070f,.07f,16);
        g.Cone(zinc,new Vector3(0,2.45f,0),.060f,.050f,2.43f,16);
        g.Cone(zinc,new Vector3(0,4.88f,0),.052f,.034f,.05f,16);
        g.Cone(zinc,new Vector3(0,4.93f,0),.034f,0,.07f,16);

        Arm(g,Vector3.left,4.72f,-102f,18f);
        Arm(g,Vector3.back,4.30f,190f,20f);
        JunctionBox(g);

        var go=Emit(g,"SiteDetails_CCTV_pole",parent,PolePosition,Quaternion.identity);
        go.name="CCTV_pole_cluster";
    }

    // Radius of the mast at height y (matches the two cones built in CctvPole).
    static float MastR(float y) {
        return y<2.45f?Mathf.Lerp(.0785f,.065f,(y-.314f)/2.136f):Mathf.Lerp(.060f,.050f,(y-2.45f)/2.43f);
    }

    static void Arm(Geo g,Vector3 dir,float y,float yaw,float pitch) {
        float rp=MastR(y);
        var rot=Quaternion.LookRotation(dir,Vector3.up);
        var lateral=Vector3.Cross(Vector3.up,dir)*.035f;
        var level=new Vector3(0,y,0);
        // Clamp collar and block on the mast.
        g.Cone(zinc,new Vector3(0,y-.045f,0),rp+.014f,rp+.014f,.09f,16);
        g.Box(zinc,level+dir*(rp+.034f),new Vector3(.062f,.09f,.07f),rot);
        // Cross arm and diagonal brace with its own band on the mast.
        g.Cyl(zinc,level+dir*(rp+.03f),level+dir*.9f,.0225f,.0225f,12);
        float yb=y-.34f,rb=MastR(yb);
        g.Cone(zinc,new Vector3(0,yb-.02f,0),rb+.010f,rb+.010f,.04f,16);
        g.Cyl(zinc,new Vector3(0,yb,0)+dir*(rb+.002f),level+Vector3.down*.02f+dir*.58f,.011f,.011f,8);
        // Camera pivot post at the end of the arm.
        var end=level+dir*.9f;
        var pivot=end+Vector3.up*.06f;
        g.Cyl(zinc,end+Vector3.up*.015f,pivot,.011f,.011f,8);
        var rotation=Quaternion.Euler(pitch,yaw,0);
        g.Xf=Matrix4x4.TRS(pivot,rotation,Vector3.one);
        BulletCamera(g);
        g.Xf=Matrix4x4.identity;
        // Signal cable from the rear gland, beside the arm and into the mast above the clamp block.
        var gland=pivot+rotation*new Vector3(0,.09f,-.105f);
        var cable=new List<Vector3> {
            gland,
            gland-dir*.05f+Vector3.down*.06f,
            new Vector3(0,y+.06f,0)+dir*.7f+lateral,
            new Vector3(0,y+.06f,0)+dir*(rp+.04f)+lateral*.3f,
            new Vector3(0,y+.06f,0)+dir*(rp-.01f)
        };
        g.Tube(cableBlack,cable,.0065f,6);
    }

    // Rectangular bullet camera in its own frame: origin at the swivel ball, +Z is the viewing direction.
    static void BulletCamera(Geo g) {
        g.Sphere(bezel,Vector3.zero,.022f,10,6);
        g.Cyl(bezel,Vector3.zero,new Vector3(0,.05f,0),.011f,.011f,8);
        g.Box(bezel,new Vector3(0,.061f,.04f),new Vector3(.05f,.022f,.10f));
        g.Box(camWhite,new Vector3(0,.111f,.055f),new Vector3(.078f,.078f,.25f));
        // Weather hood: top plate overhanging the lens, side lips and a front drip lip.
        g.Box(camWhite,new Vector3(0,.155f,.075f),new Vector3(.098f,.010f,.29f));
        for(int s=-1;s<=1;s+=2)g.Box(camWhite,new Vector3(s*.046f,.144f,.075f),new Vector3(.006f,.012f,.29f));
        g.Box(camWhite,new Vector3(0,.150f,.217f),new Vector3(.098f,.020f,.006f));
        // Black front window, lens bezel, lens glass and four IR emitters.
        g.Box(camBlack,new Vector3(0,.111f,.186f),new Vector3(.068f,.068f,.012f));
        g.Cyl(bezel,new Vector3(0,.111f,.192f),new Vector3(0,.111f,.198f),.024f,.0235f,14);
        g.Cyl(lensGlass,new Vector3(0,.111f,.196f),new Vector3(0,.111f,.2005f),.0155f,.015f,14);
        for(int i=0;i<4;i++) {
            float a=(i*90f+45f)*Mathf.Deg2Rad;
            var c=new Vector3(Mathf.Cos(a)*.028f,.111f+Mathf.Sin(a)*.028f,.192f);
            g.Cyl(irLens,c,c+new Vector3(0,0,.0015f),.004f,.004f,6);
        }
        // Rear cable gland.
        g.Cyl(bezel,new Vector3(0,.09f,-.07f),new Vector3(0,.09f,-.105f),.012f,.012f,8);
    }

    static void JunctionBox(Geo g) {
        const float yc=1.35f;
        float r=MastR(yc);
        float rear=-(r+.012f);
        // Two stainless bands and a saddle plate carry the box.
        g.Cone(steel,new Vector3(0,yc-.11f,0),r+.0025f,r+.0025f,.022f,16);
        g.Cone(steel,new Vector3(0,yc+.09f,0),r+.0025f,r+.0025f,.022f,16);
        g.Box(zinc,new Vector3(rear+.006f,yc,0),new Vector3(.012f,.26f,.09f));
        // Polycarbonate enclosure with an inset lid, two latches and two hinge barrels.
        g.Box(junctionGray,new Vector3(rear-.045f,yc,0),new Vector3(.09f,.30f,.22f));
        g.Box(junctionGray,new Vector3(rear-.100f,yc,0),new Vector3(.020f,.292f,.212f));
        for(int s=-1;s<=1;s+=2) {
            g.Box(camBlack,new Vector3(rear-.1115f,yc+s*.09f,-.085f),new Vector3(.006f,.04f,.024f));
            g.Cyl(bezel,new Vector3(rear-.106f,yc+s*.1f-.025f,.113f),new Vector3(rear-.106f,yc+s*.1f+.025f,.113f),.008f,.008f,8);
        }
        // Cable glands and the conduit runs: down into the mast base, up to the camera arms.
        const float cx=-.0985f;
        g.Cyl(bezel,new Vector3(cx,yc-.15f,0),new Vector3(cx,yc-.185f,0),.012f,.012f,8);
        g.Cyl(bezel,new Vector3(cx,yc+.15f,0),new Vector3(cx,yc+.185f,0),.012f,.012f,8);
        g.Tube(conduit,ConduitRun(cx,yc-.185f,.5f),.011f,8);
        g.Tube(conduit,ConduitRun(cx,yc+.185f,4.28f),.011f,8);
        foreach(float y in new[]{.9f,1.9f,2.8f,3.6f}) {
            float rm=MastR(y);
            g.Box(steel,new Vector3(-(rm+.1095f)*.5f,y,0),new Vector3(.1095f-rm,.02f,.028f));
        }
    }

    // Straight conduit that finishes in a quarter-round elbow entering the mast at yEnd.
    static List<Vector3> ConduitRun(float x,float yFrom,float yEnd) {
        const float bend=.05f;
        float s=yEnd>yFrom?1f:-1f;
        float yb=yEnd-s*bend;
        var pts=new List<Vector3> {new Vector3(x,yFrom,0),new Vector3(x,yb,0)};
        for(int i=1;i<=4;i++) {
            float phi=i*90f/4f*Mathf.Deg2Rad;
            pts.Add(new Vector3(x+bend-bend*Mathf.Cos(phi),yb+s*bend*Mathf.Sin(phi),0));
        }
        return pts;
    }

    // ---------------------------------------------------------------- management booth
    sealed class Face {
        public readonly Quaternion rot;
        public readonly Vector3 center;
        public readonly float half;

        public Face(Vector3 normal,float distance,float half) {
            rot=Quaternion.LookRotation(normal,Vector3.up);
            center=normal*distance;
            this.half=half;
        }

        // a: along the face, y: absolute height, d: along the outward normal (negative is into the wall).
        public Vector3 P(float a,float y,float d) {return center+rot*new Vector3(a,y,d);}
    }

    static void FBox(Geo g,Material m,Face f,float a,float y,float d,float sa,float sy,float sd) {
        g.Box(m,f.P(a,y,d),new Vector3(sa,sy,sd),f.rot);
    }

    // Wall skin 5 cm thick whose outer surface is the nominal wall plane.
    static void Skin(Geo g,Material m,Face f,float a0,float a1,float y0,float y1) {
        if(a1-a0<1e-4f||y1-y0<1e-4f)return;
        FBox(g,m,f,(a0+a1)*.5f,(y0+y1)*.5f,-.025f,a1-a0,y1-y0,.05f);
    }

    static void Booth() {
        const float baseY=.15f,topY=2.33f,sillY=.95f,headY=1.95f;
        var g=new Geo();
        g.Box(plinth,new Vector3(0,.075f,0),new Vector3(2.3f,.15f,2.1f));
        g.Box(plinth,new Vector3(0,.04f,-1.25f),new Vector3(1.1f,.08f,.4f));
        g.Box(wallWhite,new Vector3(0,(baseY+topY)*.5f,0),new Vector3(2.1f,topY-baseY,1.9f));

        var front=new Face(Vector3.forward,1f,1.1f);
        var rear=new Face(Vector3.back,1f,1.1f);
        var left=new Face(Vector3.left,1.1f,.95f);
        var right=new Face(Vector3.right,1.1f,.95f);
        foreach(var pair in new[]{new KeyValuePair<Face,float>(front,.85f),new KeyValuePair<Face,float>(left,.6f),new KeyValuePair<Face,float>(right,.6f)}) {
            var f=pair.Key;float o=pair.Value;
            Skin(g,wallBand,f,-f.half,f.half,baseY,.55f);
            Skin(g,wallWhite,f,-f.half,f.half,.55f,sillY);
            Skin(g,wallWhite,f,-f.half,f.half,headY,topY);
            Skin(g,wallWhite,f,-f.half,-o,sillY,headY);
            Skin(g,wallWhite,f,o,f.half,sillY,headY);
            SlidingWindow(g,f,-o,o,sillY,headY);
        }
        // Rear wall with the service door.
        Skin(g,wallBand,rear,-rear.half,-.4f,baseY,.55f);
        Skin(g,wallBand,rear,.4f,rear.half,baseY,.55f);
        Skin(g,wallWhite,rear,-rear.half,-.4f,.55f,topY);
        Skin(g,wallWhite,rear,.4f,rear.half,.55f,topY);
        Skin(g,wallWhite,rear,-.4f,.4f,2.05f,topY);
        Door(g,rear);

        // Corner trims, a little proud of the wall plane.
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            g.Box(alu,new Vector3(sx*1.075f,(baseY+topY)*.5f,sz*.975f),new Vector3(.07f,topY-baseY,.07f));

        // Shallow roof: fascia slab with a 20 cm overhang, a light soffit and a low pitched top.
        g.Box(roofFascia,new Vector3(0,2.38f,0),new Vector3(2.6f,.10f,2.4f));
        g.Quad(wallWhite,new Vector3(-1.3f,2.329f,-1.2f),new Vector3(1.3f,2.329f,-1.2f),new Vector3(1.3f,2.329f,1.2f),new Vector3(-1.3f,2.329f,1.2f),Vector3.down);
        g.Frustum(roofTop,2.43f,2.50f,1.25f,1.15f,.85f,.65f);

        // Name board above the front window.
        FBox(g,alu,front,0,2.19f,.004f,.96f,.27f,.008f);
        FBox(g,signBoard,front,0,2.19f,.012f,.92f,.24f,.008f);

        var go=Emit(g,"SiteDetails_management_booth",parent,BoothPosition,Quaternion.Euler(0,-90f,0));
        go.name="Management_booth";
        var box=go.AddComponent<BoxCollider>();
        box.center=new Vector3(0,1.25f,0);
        box.size=new Vector3(2.3f,2.5f,2.1f);
        Text(go.transform,"주차관리",front.P(0,2.19f,.0185f),Quaternion.Euler(0,180f,0),.15f,new Color(.95f,.95f,.92f));
    }

    // Aluminium-framed, tinted two-pane slider set into a 5 cm reveal.
    static void SlidingWindow(Geo g,Face f,float a0,float a1,float y0,float y1) {
        const float bar=.035f;
        float w=a1-a0,h=y1-y0,am=(a0+a1)*.5f,ym=(y0+y1)*.5f;
        g.Quad(interiorDark,f.P(a0,y0,-.047f),f.P(a1,y0,-.047f),f.P(a1,y1,-.047f),f.P(a0,y1,-.047f),f.rot*Vector3.forward);
        FBox(g,alu,f,am,y1-bar*.5f,-.025f,w,bar,.04f);
        FBox(g,alu,f,am,y0+bar*.5f,-.025f,w,bar,.04f);
        FBox(g,alu,f,a0+bar*.5f,ym,-.025f,bar,h-2*bar,.04f);
        FBox(g,alu,f,a1-bar*.5f,ym,-.025f,bar,h-2*bar,.04f);
        FBox(g,alu,f,am,y0-.009f,.0125f,w+.05f,.026f,.025f);
        Pane(g,f,a0+bar,am+.025f,y0+bar,y1-bar,-.034f);
        Pane(g,f,am-.025f,a1-bar,y0+bar,y1-bar,-.0205f);
        FBox(g,doorSteel,f,am-.015f,ym,-.0095f,.012f,.06f,.006f);
    }

    static void Pane(Geo g,Face f,float a0,float a1,float y0,float y1,float d) {
        float a=(a0+a1)*.5f,y=(y0+y1)*.5f;
        FBox(g,alu,f,a,y,d,a1-a0,y1-y0,.012f);
        FBox(g,tint,f,a,y,d,a1-a0-.04f,y1-y0-.04f,.016f);
    }

    static void Door(Geo g,Face f) {
        const float a0=-.4f,a1=.4f,y0=.15f,y1=2.05f,bar=.035f;
        float sideY0=y0+.02f,sideY1=y1-bar;
        // Frame (head, jambs, threshold) and the leaf.
        FBox(g,alu,f,0,y1-bar*.5f,-.025f,a1-a0,bar,.04f);
        FBox(g,alu,f,a0+bar*.5f,(sideY0+sideY1)*.5f,-.025f,bar,sideY1-sideY0,.04f);
        FBox(g,alu,f,a1-bar*.5f,(sideY0+sideY1)*.5f,-.025f,bar,sideY1-sideY0,.04f);
        FBox(g,alu,f,0,y0+.01f,-.025f,a1-a0,.02f,.04f);
        FBox(g,doorPanel,f,0,(sideY0+sideY1)*.5f,-.02f,a1-a0-2*bar,sideY1-sideY0,.03f);
        // Vision panel and ventilation louvres.
        FBox(g,alu,f,0,1.5f,-.001f,.42f,.38f,.008f);
        FBox(g,tint,f,0,1.5f,.001f,.36f,.32f,.006f);
        for(int i=0;i<4;i++)FBox(g,doorSteel,f,0,.30f+i*.05f,0,.5f,.014f,.01f);
        // Lever handle with escutcheon and cylinder lock, three hinge knuckles on the opposite jamb.
        FBox(g,doorSteel,f,.28f,1.0f,-.001f,.05f,.2f,.008f);
        FBox(g,doorSteel,f,.28f,1.04f,.012f,.02f,.02f,.016f);
        FBox(g,doorSteel,f,.25f,1.04f,.027f,.09f,.016f,.014f);
        g.Cyl(doorSteel,f.P(.28f,.9f,.003f),f.P(.28f,.9f,.011f),.011f,.011f,10);
        foreach(float y in new[]{.45f,1.1f,1.75f})g.Cyl(doorSteel,f.P(-.375f,y-.05f,.005f),f.P(-.375f,y+.05f,.005f),.01f,.01f,8);
    }

    // ---------------------------------------------------------------- waste sorting bank
    static void WasteBank() {
        var labels=new[]{"일반","캔·병","플라스틱"};
        var plates=new[]{signYellow,signBlue,signGreen};
        var inks=new[]{new Color(.10f,.09f,.06f),new Color(.95f,.95f,.92f),new Color(.95f,.95f,.92f)};
        var sizes=new[]{.09f,.085f,.075f};
        var g=new Geo();
        const float pitch=.62f;
        // Seen from the front (local +Z) the first label is on the viewer's left, which is local +X.
        for(int i=0;i<3;i++) {
            float x=-(i-1)*pitch;
            g.Xf=Matrix4x4.TRS(new Vector3(x,0,0),Quaternion.identity,Vector3.one);
            Bin(g,plates[i]);
        }
        g.Xf=Matrix4x4.identity;
        var go=Emit(g,"SiteDetails_waste_bank",parent,BinPosition,Quaternion.Euler(0,180f,0));
        go.name="Waste_sorting_bank";
        for(int i=0;i<3;i++)
            Text(go.transform,labels[i],new Vector3(-(i-1)*pitch,.52f,.2795f),Quaternion.Euler(0,180f,0),sizes[i],inks[i]);
    }

    // One 0.58 m wide bin in its own frame (front is +Z): rounded body, inset sloped lid, hinge, flap and a coloured sign plate.
    static void Bin(Geo g,Material plate) {
        g.RoundPrism(binDark,.265f,.245f,.045f,0,.06f,.06f,2,false);
        g.RoundPrism(binBody,.29f,.27f,.06f,.06f,.93f,.93f,3,false);
        g.RoundRing(binDark,.29f,.27f,.06f,.262f,.242f,.032f,.93f,.905f,3);
        g.Flat(binDark,-.262f,.262f,-.242f,.242f,.905f);
        g.RoundPrism(binLid,.256f,.236f,.026f,.905f,.925f,.945f,3,true);
        // Lid grip pad, rear hinge barrels with brackets.
        g.Box(binDark,new Vector3(0,.9285f,.185f),new Vector3(.20f,.006f,.04f),Quaternion.Euler(2.43f,0,0));
        for(int s=-1;s<=1;s+=2) {
            g.Cyl(steel,new Vector3(s*.08f,.95f,-.245f),new Vector3(s*.22f,.95f,-.245f),.011f,.011f,8);
            g.Box(steel,new Vector3(s*.15f,.936f,-.255f),new Vector3(.12f,.012f,.03f));
        }
        // Hinged waste flap in a dark recess, with a pull bar.
        g.Box(binDark,new Vector3(0,.74f,.271f),new Vector3(.37f,.20f,.002f));
        g.Box(binLid,new Vector3(0,.74f,.281f),new Vector3(.34f,.17f,.022f));
        g.Box(binDark,new Vector3(0,.665f,.292f),new Vector3(.18f,.02f,.012f));
        // Sign plate with a dark border.
        g.Box(binDark,new Vector3(0,.52f,.272f),new Vector3(.42f,.15f,.004f));
        g.Box(plate,new Vector3(0,.52f,.274f),new Vector3(.40f,.13f,.008f));
    }

    // ---------------------------------------------------------------- speed hump
    static void SpeedHump() {
        const float height=.065f,half=.2f,floor=-.02f,segmentWidth=.5f;
        const int steps=10,segments=12;
        float radius=(half*half+height*height)/(2*height);
        var z=new float[steps+1];
        var y=new float[steps+1];
        var n=new Vector3[steps+1];
        for(int k=0;k<=steps;k++) {
            z[k]=-half+2*half*k/steps;
            float c=Mathf.Sqrt(Mathf.Max(radius*radius-z[k]*z[k],0));
            y[k]=Mathf.Max(.002f,c-(radius-height));
            n[k]=new Vector3(0,c/radius,z[k]/radius).normalized;
        }
        var g=new Geo();
        float xStart=-segments*segmentWidth*.5f;
        for(int s=0;s<segments;s++) {
            var m=s%2==0?humpYellow:humpBlack;
            float xa=xStart+s*segmentWidth,xb=xa+segmentWidth;
            for(int k=0;k<steps;k++)
                g.SmoothQuad(m,new Vector3(xa,y[k],z[k]),new Vector3(xb,y[k],z[k]),new Vector3(xb,y[k+1],z[k+1]),new Vector3(xa,y[k+1],z[k+1]),n[k],n[k],n[k+1],n[k+1]);
            g.Quad(m,new Vector3(xa,floor,-half),new Vector3(xb,floor,-half),new Vector3(xb,y[0],-half),new Vector3(xa,y[0],-half),Vector3.back);
            g.Quad(m,new Vector3(xa,floor,half),new Vector3(xb,floor,half),new Vector3(xb,y[steps],half),new Vector3(xa,y[steps],half),Vector3.forward);
        }
        // Flush, closed ends.
        for(int side=-1;side<=1;side+=2) {
            float x=side*segments*segmentWidth*.5f;
            var m=side<0?humpYellow:(segments%2==0?humpBlack:humpYellow);
            for(int k=0;k<steps;k++)
                g.Quad(m,new Vector3(x,floor,z[k]),new Vector3(x,floor,z[k+1]),new Vector3(x,y[k+1],z[k+1]),new Vector3(x,y[k],z[k]),new Vector3(side,0,0));
        }
        var go=Emit(g,"SiteDetails_speed_hump",parent,HumpPosition,Quaternion.identity);
        go.name="Speed_hump_entry";
        go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
    }

    // ---------------------------------------------------------------- scene helpers
    static GameObject Emit(Geo geo,string meshName,Transform p,Vector3 position,Quaternion rotation) {
        Material[] materials;
        var mesh=geo.Bake(meshName,out materials);
        var go=new GameObject(meshName);
        go.transform.SetParent(p,false);
        go.transform.localPosition=position;go.transform.localRotation=rotation;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials=materials;
        renderer.shadowCastingMode=ShadowCastingMode.On;
        renderer.receiveShadows=true;
        renderer.lightProbeUsage=LightProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
        return go;
    }

    // Same TextMesh recipe as the lot's other signs (font.material); ParkingLotTextDepth.Refresh() swaps the material afterwards.
    // The glyphs read from the -Z side, so callers rotate the transform to look into the sign.
    static void Text(Transform p,string content,Vector3 position,Quaternion rotation,float size,Color color) {
        foreach(char c in content)if(c!=' '&&!font.HasCharacter(c)) {
            Debug.LogWarning("PARKINGLOT_SITE_DETAILS: font cannot render '"+c+"'; sign '"+content+"' was skipped.");
            return;
        }
        var o=new GameObject("Sign_"+content);
        o.transform.SetParent(p,false);
        o.transform.localPosition=position;o.transform.localRotation=rotation;
        var t=o.AddComponent<TextMesh>();
        t.text=content;t.font=font;t.fontSize=80;t.characterSize=size/8f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        var renderer=o.GetComponent<MeshRenderer>();
        renderer.sharedMaterial=font.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    // ---------------------------------------------------------------- materials
    static void CreateMaterials() {
        zinc=Mat("SiteDetails_galvanized_steel",new Color(.64f,.66f,.66f),.5f,.75f);
        steel=Mat("SiteDetails_stainless_fasteners",new Color(.38f,.39f,.40f),.5f,.8f);
        concreteFoot=Mat("SiteDetails_concrete_footing",new Color(.58f,.57f,.54f),.08f,0,true);
        camWhite=Mat("SiteDetails_camera_housing",new Color(.80f,.81f,.79f),.35f);
        camBlack=Mat("SiteDetails_black_plastic",new Color(.03f,.03f,.035f),.3f,0,true);
        bezel=Mat("SiteDetails_dark_bezel",new Color(.10f,.10f,.11f),.4f,.5f);
        lensGlass=Mat("SiteDetails_lens_glass",new Color(.02f,.025f,.04f),.9f,.2f);
        irLens=Mat("SiteDetails_ir_emitter",new Color(.18f,.04f,.04f),.8f);
        junctionGray=Mat("SiteDetails_junction_box",new Color(.56f,.58f,.58f),.3f);
        conduit=Mat("SiteDetails_conduit",new Color(.30f,.32f,.33f),.35f);
        cableBlack=Mat("SiteDetails_cable",new Color(.02f,.02f,.02f),.3f,0,true);

        wallWhite=Mat("SiteDetails_booth_wall",new Color(.84f,.85f,.83f),.25f,0,true);
        wallBand=Mat("SiteDetails_booth_base_band",new Color(.52f,.55f,.57f),.25f,0,true);
        alu=Mat("SiteDetails_aluminium_frame",new Color(.70f,.72f,.73f),.5f,.7f);
        tint=Mat("SiteDetails_tinted_glass",new Color(.07f,.11f,.13f),.9f,.1f);
        interiorDark=Mat("SiteDetails_booth_interior",new Color(.04f,.05f,.05f),.1f,0,true);
        roofTop=Mat("SiteDetails_booth_roof",new Color(.33f,.40f,.46f),.3f);
        roofFascia=Mat("SiteDetails_booth_fascia",new Color(.27f,.33f,.38f),.3f);
        plinth=Mat("SiteDetails_booth_plinth",new Color(.55f,.54f,.51f),.08f,0,true);
        signBoard=Mat("SiteDetails_booth_sign",new Color(.14f,.27f,.40f),.3f);
        doorPanel=Mat("SiteDetails_booth_door",new Color(.78f,.80f,.80f),.28f,0,true);
        doorSteel=Mat("SiteDetails_door_hardware",new Color(.20f,.20f,.21f),.6f,.8f);

        binBody=Mat("SiteDetails_bin_body",new Color(.20f,.22f,.22f),.3f,0,true);
        binLid=Mat("SiteDetails_bin_lid",new Color(.25f,.27f,.27f),.3f,0,true);
        binDark=Mat("SiteDetails_bin_trim",new Color(.07f,.07f,.075f),.25f,0,true);
        signYellow=Mat("SiteDetails_sign_yellow",new Color(.74f,.60f,.20f),.2f,0,true);
        signBlue=Mat("SiteDetails_sign_blue",new Color(.20f,.36f,.58f),.2f,0,true);
        signGreen=Mat("SiteDetails_sign_green",new Color(.24f,.46f,.30f),.2f,0,true);

        humpYellow=Mat("SiteDetails_hump_yellow",new Color(.80f,.63f,.06f),.3f);
        humpBlack=Mat("SiteDetails_hump_rubber",new Color(.045f,.045f,.05f),.2f,0,true);
    }

    static Material Mat(string name,Color color,float smoothness,float metallic=0,bool matte=false) {
        var shader=Shader.Find("Standard");
        if(shader==null)throw new InvalidOperationException("The Standard shader is unavailable.");
        string path=MaterialDir+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        m.color=color;
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

    static void EnsureFolder(string path) {
        if(AssetDatabase.IsValidFolder(path))return;
        int split=path.LastIndexOf('/');
        string up=path.Substring(0,split);
        EnsureFolder(up);
        AssetDatabase.CreateFolder(up,path.Substring(split+1));
    }

    // ---------------------------------------------------------------- mesh accumulation
    /// <summary>Geometry grouped into one submesh per shared material, baked to a single mesh asset. Xf places every primitive.</summary>
    sealed class Geo {
        readonly List<Vector3> verts=new List<Vector3>();
        readonly List<Vector3> normals=new List<Vector3>();
        readonly List<Vector2> uvs=new List<Vector2>();
        readonly List<Material> slots=new List<Material>();
        readonly List<List<int>> tris=new List<List<int>>();
        static readonly Vector3[] BoxAxes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};

        /// <summary>Rigid (rotation + translation) transform applied to everything added afterwards.</summary>
        public Matrix4x4 Xf=Matrix4x4.identity;

        int Slot(Material m) {
            int i=slots.IndexOf(m);
            if(i>=0)return i;
            slots.Add(m);tris.Add(new List<int>());
            return slots.Count-1;
        }

        int Vert(Vector3 p,Vector3 n,Vector2 uv) {
            verts.Add(Xf.MultiplyPoint3x4(p));normals.Add(Xf.MultiplyVector(n).normalized);uvs.Add(uv);
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
            SmoothQuad(m,a,b,c,d,n,n,n,n);
        }

        public void SmoothQuad(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 na,Vector3 nb,Vector3 nc,Vector3 nd) {
            int s=Slot(m);
            int i=Vert(a,na,new Vector2(0,0));
            Vert(b,nb,new Vector2(1,0));Vert(c,nc,new Vector2(1,1));Vert(d,nd,new Vector2(0,1));
            Tri(s,i,i+1,i+2);Tri(s,i,i+2,i+3);
        }

        // Quad whose normal is derived from its corners and pointed away from 'inside'.
        void QuadOut(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 inside) {
            var n=Vector3.Cross(b-a,d-a).normalized;
            if(Vector3.Dot(n,(a+b+c+d)*.25f-inside)<0)n=-n;
            Quad(m,a,b,c,d,n);
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

        /// <summary>Truncated pyramid centred on the Y axis (bottom half-sizes hx0/hz0, top hx1/hz1), closed on top.</summary>
        public void Frustum(Material m,float y0,float y1,float hx0,float hz0,float hx1,float hz1) {
            var inside=new Vector3(0,(y0+y1)*.5f,0);
            var b=new[]{new Vector3(-hx0,y0,-hz0),new Vector3(hx0,y0,-hz0),new Vector3(hx0,y0,hz0),new Vector3(-hx0,y0,hz0)};
            var t=new[]{new Vector3(-hx1,y1,-hz1),new Vector3(hx1,y1,-hz1),new Vector3(hx1,y1,hz1),new Vector3(-hx1,y1,hz1)};
            for(int i=0;i<4;i++) {
                int j=(i+1)%4;
                QuadOut(m,b[i],b[j],t[j],t[i],inside);
            }
            Quad(m,t[0],t[1],t[2],t[3],Vector3.up);
        }

        public void Sphere(Material m,Vector3 center,float radius,int seg,int rings) {
            int s=Slot(m),first=verts.Count;
            for(int i=0;i<=rings;i++) {
                float phi=Mathf.PI*i/rings;
                float y=Mathf.Cos(phi),rr=Mathf.Sin(phi);
                for(int k=0;k<=seg;k++) {
                    float a=k*Mathf.PI*2/seg;
                    var n=new Vector3(Mathf.Cos(a)*rr,y,Mathf.Sin(a)*rr);
                    Vert(center+n*radius,n,new Vector2((float)k/seg,(float)i/rings));
                }
            }
            int w=seg+1;
            for(int i=0;i<rings;i++)for(int k=0;k<seg;k++) {
                int a=first+i*w+k;
                Tri(s,a,a+1,a+w);Tri(s,a+1,a+w+1,a+w);
            }
        }

        /// <summary>Cone / cylinder between two points (different end radii allowed), with end caps.</summary>
        public void Cyl(Material m,Vector3 from,Vector3 to,float r0,float r1,int segments) {
            var d=to-from;
            float length=d.magnitude;
            if(length<1e-5f)return;
            Cone(m,from,r0,r1,length,segments,Quaternion.FromToRotation(Vector3.up,d/length));
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

        // Rounded-rectangle outline around the origin: four corner arcs (arc+1 points each), counter-clockwise from +X/+Z.
        static void Perimeter(float hw,float hd,float r,int arc,List<Vector2> pts,List<Vector2> nrm) {
            for(int c=0;c<4;c++) {
                float sx=(c==0||c==3)?1f:-1f,sz=c<2?1f:-1f;
                for(int k=0;k<=arc;k++) {
                    float ang=(c*90f+90f*k/arc)*Mathf.Deg2Rad;
                    var n=new Vector2(Mathf.Cos(ang),Mathf.Sin(ang));
                    pts.Add(new Vector2(sx*(hw-r),sz*(hd-r))+n*r);
                    nrm.Add(n);
                }
            }
        }

        static float SlopeY(float z,float hd,float yBack,float yFront) {return Mathf.Lerp(yBack,yFront,(z/hd+1f)*.5f);}

        /// <summary>Rounded-rectangle prism from y0 up to a top that may slope from yBack (-Z) to yFront (+Z).</summary>
        public void RoundPrism(Material m,float hw,float hd,float r,float y0,float yFront,float yBack,int arc,bool top) {
            var pts=new List<Vector2>();var nrm=new List<Vector2>();
            Perimeter(hw,hd,r,arc,pts,nrm);
            int count=pts.Count;
            for(int i=0;i<count;i++) {
                int j=(i+1)%count;
                var ni=new Vector3(nrm[i].x,0,nrm[i].y);var nj=new Vector3(nrm[j].x,0,nrm[j].y);
                SmoothQuad(m,
                    new Vector3(pts[i].x,y0,pts[i].y),new Vector3(pts[j].x,y0,pts[j].y),
                    new Vector3(pts[j].x,SlopeY(pts[j].y,hd,yBack,yFront),pts[j].y),new Vector3(pts[i].x,SlopeY(pts[i].y,hd,yBack,yFront),pts[i].y),
                    ni,nj,nj,ni);
            }
            if(!top)return;
            int s=Slot(m);
            var up=new Vector3(0,1f,-(yFront-yBack)/(2*hd)).normalized;
            int c0=Vert(new Vector3(0,(yFront+yBack)*.5f,0),up,new Vector2(.5f,.5f));
            for(int i=0;i<count;i++)Vert(new Vector3(pts[i].x,SlopeY(pts[i].y,hd,yBack,yFront),pts[i].y),up,new Vector2(.5f+pts[i].x/(2*hw),.5f+pts[i].y/(2*hd)));
            for(int i=0;i<count;i++)Tri(s,c0,c0+1+i,c0+1+(i+1)%count);
        }

        /// <summary>Flat rim between an outer and inner rounded outline at yTop, plus the inner wall down to yBottom.</summary>
        public void RoundRing(Material m,float hwO,float hdO,float rO,float hwI,float hdI,float rI,float yTop,float yBottom,int arc) {
            var outer=new List<Vector2>();var outerN=new List<Vector2>();
            var inner=new List<Vector2>();var innerN=new List<Vector2>();
            Perimeter(hwO,hdO,rO,arc,outer,outerN);
            Perimeter(hwI,hdI,rI,arc,inner,innerN);
            int count=outer.Count;
            for(int i=0;i<count;i++) {
                int j=(i+1)%count;
                Quad(m,new Vector3(outer[i].x,yTop,outer[i].y),new Vector3(outer[j].x,yTop,outer[j].y),
                    new Vector3(inner[j].x,yTop,inner[j].y),new Vector3(inner[i].x,yTop,inner[i].y),Vector3.up);
                var ni=-new Vector3(innerN[i].x,0,innerN[i].y);var nj=-new Vector3(innerN[j].x,0,innerN[j].y);
                SmoothQuad(m,new Vector3(inner[i].x,yBottom,inner[i].y),new Vector3(inner[j].x,yBottom,inner[j].y),
                    new Vector3(inner[j].x,yTop,inner[j].y),new Vector3(inner[i].x,yTop,inner[i].y),ni,nj,nj,ni);
            }
        }

        /// <summary>Writes (or overwrites in place) Meshes/SiteDetails/{name}.asset; the mesh is named after the file.</summary>
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
                triangleCount+=tris[used[i]].Count/3;
            }
            mesh.RecalculateBounds();
            if(created)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }
    }
}
}
