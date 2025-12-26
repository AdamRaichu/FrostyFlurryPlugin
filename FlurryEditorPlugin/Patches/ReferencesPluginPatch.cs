using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Managers;
using HarmonyLib;
using ReferencesPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flurry.Editor.Patches
{
    [HarmonyPatch(typeof(ReferenceTabItem))]
    [HarmonyPatchCategory("flurry.editor")]
    public class ReferencesPluginPatch
    {
        private static AccessTools.FieldRef<ReferenceTabItem, MenuItem> explorerToFindItem_ref = AccessTools.FieldRefAccess<ReferenceTabItem, MenuItem>("refExplorerToFindItem");
        private static AccessTools.FieldRef<ReferenceTabItem, MenuItem> explorerFromFindItem_ref = AccessTools.FieldRefAccess<ReferenceTabItem, MenuItem>("refExplorerFromFindItem");
        private static AccessTools.FieldRef<ReferenceTabItem, FrostyAssetListView> refToList_ref = AccessTools.FieldRefAccess<ReferenceTabItem, FrostyAssetListView>("refExplorerToList");
        private static AccessTools.FieldRef<ReferenceTabItem, FrostyAssetListView> refFromList_ref = AccessTools.FieldRefAccess<ReferenceTabItem, FrostyAssetListView>("refExplorerFromList");

        [HarmonyPatch("RefreshReferences")]
        [HarmonyPostfix]
        public static void ReferenceCounter(ReferenceTabItem __instance)
        {
            FrostyAssetListView refExplorerToList = refToList_ref(__instance);
            FrostyAssetListView refExplorerFromList = refFromList_ref(__instance);

            if (refExplorerToList.ItemsSource == null || refExplorerFromList.ItemsSource == null)
                return;

            foreach (var item in App.EditorWindow.MiscTabControl.Items)
            {
                if (item is FrostyTabItem)
                {
                    FrostyTabItem tab = item as FrostyTabItem;
                    if (tab.Header.ToString().Contains("References"))
                    {
                        tab.Header = $"References ({Enumerable.Count((IEnumerable<EbxAssetEntry>)refExplorerToList.ItemsSource)} & {Enumerable.Count((IEnumerable<EbxAssetEntry>)refExplorerFromList.ItemsSource)})";
                    }
                }
            }
        }

        [HarmonyPatch("OnApplyTemplate")]
        [HarmonyPostfix]
        public static void PatchContextMenu(ReferenceTabItem __instance)
        {
            FlurryEditorConfig config = new FlurryEditorConfig();
            config.Load();
            if (!config.ReferencesTabTweaks)
            {
                return;
            }

            ImageSourceConverter converter = new ImageSourceConverter();

            MenuItem refExplorerToFindItem = explorerToFindItem_ref(__instance);
            MenuItem refExploreFromFindItem = explorerFromFindItem_ref(__instance);
            FrostyAssetListView refExplorerToList = refToList_ref(__instance);
            FrostyAssetListView refExplorerFromList = refFromList_ref(__instance);

            ContextMenu cmTo = refExplorerToFindItem.Parent as ContextMenu;
            ContextMenu cmFrom = refExploreFromFindItem.Parent as ContextMenu;

            // Add an icon to the "Open in Explorer" menu items
            refExplorerToFindItem.Icon = new Image()
            {
                Source = converter.ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Open.png") as ImageSource,
                Opacity = 0.5
            };
            refExploreFromFindItem.Icon = new Image()
            {
                Source = converter.ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Open.png") as ImageSource,
                Opacity = 0.5
            };


            // Additional icons
            // Open in Blueprint Editor
            if (config.BlueprintEditorTweaks)
            {
                MenuItem blueprintEditorToItem = new MenuItem()
                {
                    Header = "Open in Blueprint Editor",
                    Icon = new Image()
                    {
                        Source = converter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/BlueprintFileType.png") as ImageSource,
                        Opacity = 0.5
                    }
                };
                blueprintEditorToItem.Click += (s, e) =>
                {
                    if (refExplorerToList.SelectedItem is EbxAssetEntry)
                        FlurryEditorUtils.OpenInBlueprintEditor(refExplorerToList.SelectedItem as EbxAssetEntry);
                };

                MenuItem blueprintEditorFromItem = new MenuItem()
                {
                    Header = "Open in Blueprint Editor",
                    Icon = new Image()
                    {
                        Source = converter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/BlueprintFileType.png") as ImageSource,
                        Opacity = 0.5
                    }
                };
                blueprintEditorFromItem.Click += (s, e) =>
                {
                    if (refExplorerFromList.SelectedItem is EbxAssetEntry)
                        FlurryEditorUtils.OpenInBlueprintEditor(refExplorerFromList.SelectedItem as EbxAssetEntry);
                };

                cmTo.Items.Add(blueprintEditorToItem);
                cmFrom.Items.Add(blueprintEditorFromItem);
            }


            // Copy file GUID
            MenuItem copyGuidToItem = new MenuItem()
            {
                Header = "Copy file GUID",
                Icon = new Image()
                {
                    Source = converter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Copy.png") as ImageSource,
                    Opacity = 0.5
                }
            };
            copyGuidToItem.Click += (s, e) =>
            {
                if (refExplorerToList.SelectedItem == null)
                    return;
                if (refExplorerToList.SelectedItem is EbxAssetEntry ebxEntry)
                {
                    Clipboard.SetText(ebxEntry.Guid.ToString());
                }
            };

            MenuItem copyGuidFromItem = new MenuItem()
            {
                Header = "Copy file GUID",
                Icon = new Image()
                {
                    Source = converter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Copy.png") as ImageSource,
                    Opacity = 0.5
                }
            };
            copyGuidFromItem.Click += (s, e) =>
            {
                if (refExplorerFromList.SelectedItem == null)
                    return;
                if (refExplorerFromList.SelectedItem is EbxAssetEntry ebxEntry)
                {
                    Clipboard.SetText(ebxEntry.Guid.ToString());
                }
            };

            cmTo.Items.Add(copyGuidToItem);
            cmFrom.Items.Add(copyGuidFromItem);


            // Copy file path
            MenuItem copyPathToItem = new MenuItem()
            {
                Header = "Copy file path",
                Icon = new Image()
                {
                    Source = converter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Copy.png") as ImageSource,
                    Opacity = 0.5
                }
            };
            copyPathToItem.Click += (s, e) =>
            {
                EbxAssetEntry entry = refExplorerToList.SelectedItem as EbxAssetEntry;
                if (entry == null)
                    return;
                Clipboard.SetText(entry.Name);
            };

            MenuItem copyPathFromItem = new MenuItem()
            {
                Header = "Copy file path",
                Icon = new Image()
                {
                    Source = converter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Copy.png") as ImageSource,
                    Opacity = 0.5
                }
            };
            copyPathFromItem.Click += (s, e) =>
            {
                EbxAssetEntry entry = refExplorerFromList.SelectedItem as EbxAssetEntry;
                if (entry == null)
                    return;
                Clipboard.SetText(entry.Name);
            };

            cmTo.Items.Add(copyPathToItem);
            cmFrom.Items.Add(copyPathFromItem);
        }
    }
}
