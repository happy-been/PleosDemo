using System.Collections.Generic;
using UnityEngine;
namespace ParkingLotEnvironment {
// The default GUI font shader renders through cars. Keep scene text depth-tested,
// while tracking Unity's dynamic font atlas across editor/domain/play reloads.
[ExecuteAlways]
public sealed class ParkingLotTextDepth:MonoBehaviour {
    public Font font;
    public Material textMaterial;
    Renderer[] renderers;
    void OnEnable(){renderers=null;}
    void LateUpdate(){Refresh();}
    public void Refresh(){
        if(font==null||textMaterial==null)return;
        textMaterial.mainTexture=font.material.mainTexture;
        if(renderers==null){var list=new List<Renderer>();foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var text in root.GetComponentsInChildren<TextMesh>(true))if(text.font==font)list.Add(text.GetComponent<Renderer>());renderers=list.ToArray();}
        foreach(var renderer in renderers)if(renderer!=null&&renderer.sharedMaterial!=textMaterial)renderer.sharedMaterial=textMaterial;
    }
}
}
