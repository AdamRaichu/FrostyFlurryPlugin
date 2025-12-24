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
            FileLog.Log("[Flurry] Patching FrostyPropertyGridItem context menu for Filter Guid option.");
            FrostyPropertyGridItemData item = (FrostyPropertyGridItemData)__instance.DataContext;
            if (!item.IsPointerRef)
            {
                return;
            }

            for (var i = 0; i < __instance.ContextMenu.Items.Count; i++)
            {
                var menuItem = __instance.ContextMenu.Items[i] as MenuItem;
                if (!(menuItem is MenuItem)) continue;
                FileLog.Log("[Flurry] Found context menu item: " + menuItem.Header.ToString());
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
