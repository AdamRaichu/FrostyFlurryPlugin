using Frosty.Controls;
using Frosty.Core.Controls;
using Frosty.Core.Converters;
using FrostySdk.Ebx;
using FrostySdk.IO;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flurry.Editor.Patches
{
    [HarmonyPatch(typeof(FrostyPropertyGridItemData))]
    [HarmonyPatchCategory("flurry.editor")]
    public class FilterGuidSiblingPatch
    {
        [HarmonyPatch("FilterGuid")]
        [HarmonyPostfix]
        public static void ShowSiblingsOnMatch(FrostyPropertyGridItemData __instance, bool __result, bool doNotHideSubObjects)
        {
            if (doNotHideSubObjects || __result)
                return;

            // Only unhide siblings when the current item is an array element (like [12]),
            // so all fields of a matched connection/object (Source, Target, Flags, ...) are
            // visible. Do NOT unhide at root/category level — that would also show unrelated
            // top-level categories like LinkConnections/Interface that have no match.
            if (!__instance.IsArrayChild)
                return;

            bool anyVisible = false;
            foreach (var item in __instance.Children)
            {
                if (!item.IsHidden) { anyVisible = true; break; }
            }
            if (anyVisible)
            {
                foreach (var item in __instance.Children)
                    UnhideRecursive(item);
            }
        }

        private static void UnhideRecursive(FrostyPropertyGridItemData item)
        {
            item.IsHidden = false;
            foreach (var child in item.Children)
                UnhideRecursive(child);
        }
    }

    [HarmonyPatch(typeof(FrostyPropertyGridItem))]
    [HarmonyPatchCategory("flurry.editor")]
    public class FrostyPropertyGridPatch
    {
        private static AccessTools.FieldRef<FrostyPropertyGrid, FrostyWatermarkTextBox> filterBoxRef =
            AccessTools.FieldRefAccess<FrostyPropertyGrid, FrostyWatermarkTextBox>("filterBox");

        [HarmonyPatch("OnApplyTemplate")]
        [HarmonyPostfix]
        public static void FilterGuidContextMenuButton(FrostyPropertyGridItem __instance)
        {
            FileLog.Debug("[Flurry] Patching FrostyPropertyGridItem context menu for Filter Guid option.");
            FrostyPropertyGridItemData item = (FrostyPropertyGridItemData)__instance.DataContext;
            if (!item.IsPointerRef)
            {
                return;
            }

            for (var i = 0; i < __instance.ContextMenu.Items.Count; i++)
            {
                var menuItem = __instance.ContextMenu.Items[i] as MenuItem;
                if (!(menuItem is MenuItem)) continue;
                FileLog.Debug("[Flurry] Found context menu item: " + menuItem.Header.ToString());
                if (menuItem != null && menuItem.Header.ToString() == "Copy Guid")
                {
                    //menuItem.Visibility = System.Windows.Visibility.Collapsed;
                    MenuItem filterGuidItem = new MenuItem
                    {
                        Header = "Filter Guid",
                        Icon = new Image
                        {
                            Source = StringToBitmapSourceConverter.CopySource,
                            Opacity = 0.5
                        }
                    };
                    filterGuidItem.Click += (s, e) =>
                    {
                        string guidToCopy = "";

                        PointerRef pointerRef = (PointerRef)item.Value;
                        if (pointerRef.Type == PointerRefType.Null)
                            guidToCopy = "";
                        else if (pointerRef.Type == PointerRefType.External)
                            guidToCopy = pointerRef.External.ClassGuid.ToString();
                        else
                        {
                            dynamic obj = pointerRef.Internal;
                            guidToCopy = obj.GetInstanceGuid().ToString();
                        }
                        FrostyPropertyGrid pg = GetPropertyGrid(__instance);
                        FrostyWatermarkTextBox filterBox = filterBoxRef(pg);
                        filterBox.WatermarkText = "";
                        filterBox.Text = "guid:" + guidToCopy;
                        Traverse.Create(pg).Method("FilterBox_LostFocus", new Type[] { typeof(object), typeof(RoutedEventArgs) })
                            .GetValue(new object[] { filterBox, null });
                    };
                    __instance.ContextMenu.Items.Insert(i + 1, filterGuidItem);
                }
            }
        }

        private static FrostyPropertyGrid GetPropertyGrid(FrostyPropertyGridItem item)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (!(parent.GetType().IsSubclassOf(typeof(FrostyPropertyGrid)) || parent is FrostyPropertyGrid))
                parent = VisualTreeHelper.GetParent(parent);
            return (parent as FrostyPropertyGrid);
        }
    }
}
