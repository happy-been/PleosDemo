using UnityEngine;

namespace Parkside.UI.Hud
{
    /// <summary>
    /// Fonts, sprites and the shared player car for the presentation HUD, loaded from Resources/ParksideHud/HudAssets
    /// so every drive scene can build the HUD without scene references. Created by PARKSIDE → Update HUD assets.
    /// </summary>
    public sealed class HudAssets : ScriptableObject
    {
        public Font regular, medium, semiBold;
        public GameObject playerCar;
        public Sprite round20, round14, circle, arrow, carIcon;
        public Sprite cockpit, wheel, handLeft, handRight;
        public Sprite mirrorRearHousing, mirrorRearMask, mirrorLeftHousing, mirrorLeftMask, mirrorRightHousing, mirrorRightMask;

        private static HudAssets loaded;
        public static HudAssets Load() => loaded != null ? loaded : loaded = Resources.Load<HudAssets>("ParksideHud/HudAssets");
    }
}
