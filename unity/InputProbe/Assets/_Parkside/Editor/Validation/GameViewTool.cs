using System;
using System.Reflection;
using UnityEditor;

namespace Parkside.Editor
{
    /// <summary>Selects a fixed Game view resolution (added once as "Parkside WxH") through the editor's internal API.</summary>
    public static class GameViewTool
    {
        private static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
        private static readonly Type GameViewType = EditorAssembly.GetType("UnityEditor.GameView");

        private static PropertyInfo Selected => GameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static EditorWindow Window => EditorWindow.GetWindow(GameViewType, false, null, false);

        /// <summary>Selects width x height and returns the previously selected index (for Restore).</summary>
        public static int Select(int width, int height)
        {
            var window = Window;
            var previous = (int)Selected.GetValue(window);
            var sizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            var count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var index = -1;
            for (var i = 0; i < count && index < 0; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var t = size.GetType();
                if ((int)t.GetProperty("width").GetValue(size) == width && (int)t.GetProperty("height").GetValue(size) == height && t.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution") index = i;
            }
            if (index < 0)
            {
                var sizeType = EditorAssembly.GetType("UnityEditor.GameViewSize");
                var kind = EditorAssembly.GetType("UnityEditor.GameViewSizeType");
                var created = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) }).Invoke(new[] { Enum.Parse(kind, "FixedResolution"), width, height, (object)("Parkside " + width + "x" + height) });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { created });
                index = count;
            }
            Restore(index);
            return previous;
        }

        public static void Restore(int index)
        {
            var window = Window;
            Selected.SetValue(window, index);
            window.Repaint();
        }
    }
}
