using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Checks actual generated scene bounds, including rotated buildings, rather than trusting layout comments.
public static class ParkingLotNeighborhoodAudit {
    public static void Check(Scene scene){
        if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Wrong audit scene.");
        // Editor transform edits can leave physics bounds cached until the next simulation step.
        Physics.SyncTransforms();
        var buildings=new List<BoxCollider>();var streets=new List<MeshRenderer>();var trees=new List<Transform>();
        foreach(var root in scene.GetRootGameObjects()){
            if(root.name=="09_Korean_neighborhood_context"||root.name=="12_Korean_residential_infill")buildings.AddRange(root.GetComponentsInChildren<BoxCollider>());
            if(root.name=="10_Connected_Korean_neighborhood")foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.shader!=null&&(renderer.sharedMaterial.shader.name=="ParkingLot/RoadSurface"||renderer.sharedMaterial.shader.name=="ParkingLot/WholeLookRoad"))streets.Add(renderer);
            foreach(var tree in root.GetComponentsInChildren<Transform>())if(tree.name=="Photographic_3D_tree"||tree.name=="Neighborhood_tree_12k")trees.Add(tree);
        }
        var problems=new List<string>();
        foreach(var b in buildings)foreach(var street in streets){var a=b.bounds;var r=street.bounds;float overlapX=Mathf.Min(a.max.x,r.max.x)-Mathf.Max(a.min.x,r.min.x),overlapZ=Mathf.Min(a.max.z,r.max.z)-Mathf.Max(a.min.z,r.min.z);if(overlapX>.02f&&overlapZ>.02f)problems.Add(b.name+" overlaps "+street.name+" by "+overlapX.ToString("F2")+" x "+overlapZ.ToString("F2")+"m");}
        int streetConflicts=problems.Count;
        foreach(var tree in trees)foreach(var b in buildings){var a=b.bounds;var p=tree.position;if(p.x>a.min.x&&p.x<a.max.x&&p.z>a.min.z&&p.z<a.max.z)problems.Add(tree.name+" root inside "+b.name);}
        var report=DateTime.Now.ToString("s")+"\nActual building colliders="+buildings.Count+"\nStreet surfaces="+streets.Count+"\nBuilding/street overlaps="+streetConflicts+"\nActual 3D tree roots="+trees.Count+"\nTree/building overlaps="+(problems.Count-streetConflicts)+"\n"+string.Join("\n",problems);
        File.WriteAllText(ParkingLotEditorPaths.Output+"/neighborhood-layout-audit.txt",report);
        if(buildings.Count!=32)throw new InvalidOperationException("Expected 32 neighborhood buildings, found "+buildings.Count);
        if(streets.Count!=13)throw new InvalidOperationException("Expected 13 street surfaces, found "+streets.Count+"; layout audit is incomplete.");
        if(problems.Count>0)throw new InvalidOperationException(report);
    }
}
}
