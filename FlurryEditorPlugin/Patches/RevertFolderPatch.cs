using App = Frosty.Core.App;
using Frosty.Controls;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flurry.Editor.Patches
{
    // =========================================================================
    //  FEATURE: Revert entire folder in Data Explorer (#6)
    //
    //  Adds a context menu to the folder tree with "Revert Folder" and
    //  "Revert Folder & Subfolders" options. Collects all modified assets
    //  under the selected path, confirms with the user, closes open tabs,
    //  and reverts them.
    // =========================================================================

    [HarmonyPatch(typeof(FrostyDataExplorer), "OnApplyTemplate")]
    [HarmonyPatchCategory("flurry.editor")]
    public class RevertFolderPatch
    {
        private static readonly FieldInfo assetTreeViewField
            = AccessTools.Field(typeof(FrostyDataExplorer), "assetTreeView");

        // AssetPath reflection (internal class)
        private static readonly Type assetPathType
            = typeof(FrostyDataExplorer).Assembly.GetType("Frosty.Core.Controls.AssetPath");
        private static readonly PropertyInfo fullPathProp
            = assetPathType?.GetProperty("FullPath");

        [HarmonyPostfix]
        public static void Postfix(FrostyDataExplorer __instance)
        {
            var treeView = assetTreeViewField?.GetValue(__instance) as TreeView;
            if (treeView == null)
                return;

            var folderContextMenu = new ContextMenu();

            var revertFolderItem = new MenuItem { Header = "Revert Folder" };
            revertFolderItem.Click += (s, e) => RevertFolder(__instance, treeView, includeSubfolders: false);

            var revertSubfoldersItem = new MenuItem { Header = "Revert Folder + Subfolders" };
            revertSubfoldersItem.Click += (s, e) => RevertFolder(__instance, treeView, includeSubfolders: true);

            // Add revert icon if available
            try
            {
                var converter = new ImageSourceConverter();
                var revertIcon = converter.ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Revert.png") as ImageSource;
                if (revertIcon != null)
                {
                    revertFolderItem.Icon = new Image { Source = revertIcon, Width = 16, Height = 16 };
                    revertSubfoldersItem.Icon = new Image { Source = revertIcon, Width = 16, Height = 16 };
                    RenderOptions.SetBitmapScalingMode(revertFolderItem.Icon as Image, BitmapScalingMode.Fant);
                    RenderOptions.SetBitmapScalingMode(revertSubfoldersItem.Icon as Image, BitmapScalingMode.Fant);
                }
            }
            catch { /* icon is optional */ }

            folderContextMenu.Items.Add(revertFolderItem);
            folderContextMenu.Items.Add(revertSubfoldersItem);

            // Only enable items when a folder is selected
            folderContextMenu.Opened += (s, e) =>
            {
                var selectedItem = treeView.SelectedItem;
                bool hasSelection = selectedItem != null && assetPathType != null && assetPathType.IsInstanceOfType(selectedItem);
                revertFolderItem.IsEnabled = hasSelection;
                revertSubfoldersItem.IsEnabled = hasSelection;
            };

            // Apply context menu to tree view items via style, preserving the existing theme style
            var existingStyle = treeView.ItemContainerStyle;
            var itemStyle = new Style(typeof(TreeViewItem));
            if (existingStyle != null)
                itemStyle.BasedOn = existingStyle;
            itemStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, folderContextMenu));
            treeView.ItemContainerStyle = itemStyle;
        }

        private static void RevertFolder(FrostyDataExplorer explorer, TreeView treeView, bool includeSubfolders)
        {
            var selectedItem = treeView.SelectedItem;
            if (selectedItem == null || fullPathProp == null)
                return;

            string folderPath = (fullPathProp.GetValue(selectedItem) as string)?.Trim('/') ?? "";
            if (string.IsNullOrEmpty(folderPath))
                return;

            // Collect all modified assets in this folder (and optionally subfolders)
            var toRevert = new List<AssetEntry>();

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true))
            {
                if (MatchesFolder(entry.Path, folderPath, includeSubfolders))
                    toRevert.Add(entry);
            }
            foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
            {
                if (MatchesFolder(entry.Path, folderPath, includeSubfolders))
                    toRevert.Add(entry);
            }
            foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
            {
                if (MatchesFolder(entry.Path ?? "", folderPath, includeSubfolders))
                    toRevert.Add(entry);
            }

            if (toRevert.Count == 0)
            {
                FrostyMessageBox.Show("No modified assets in this folder.", "Revert Folder");
                return;
            }

            string scope = includeSubfolders ? "folder and all subfolders" : "folder";
            var result = FrostyMessageBox.Show(
                $"Revert {toRevert.Count} modified asset(s) in this {scope}?\n\nFolder: {folderPath}\n\nThis cannot be undone.",
                "Revert Folder",
                MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
                return;

            // Close any open tabs for assets being reverted
            CloseTabsForAssets(toRevert);

            // Revert all — suppress per-asset OnModify events to avoid
            // rebuilding the data explorer on every single revert
            FrostyTaskWindow.Show("Reverting Folder", folderPath, (task) =>
            {
                int count = 0;
                foreach (var entry in toRevert)
                {
                    App.AssetManager.RevertAsset(entry, suppressOnModify: true);
                    count++;
                    task.Update($"Reverted {count}/{toRevert.Count}");
                }
            });

            // Refresh both explorers once at the end, matching normal editor behavior
            RefreshAllExplorers();
            App.Logger.Log($"Reverted {toRevert.Count} asset(s) in {folderPath}");
        }

        private static void RefreshAllExplorers()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;

                var mainWindowType = mainWindow.GetType();

                var dataExplorerField = AccessTools.Field(mainWindowType, "dataExplorer");
                var legacyExplorerField = AccessTools.Field(mainWindowType, "legacyExplorer");

                var dataExplorer = dataExplorerField?.GetValue(mainWindow) as FrostyDataExplorer;
                var legacyExplorer = legacyExplorerField?.GetValue(mainWindow) as FrostyDataExplorer;

                dataExplorer?.RefreshAll();
                legacyExplorer?.RefreshAll();
            }
            catch { /* non-critical */ }
        }

        private static bool MatchesFolder(string assetPath, string folderPath, bool includeSubfolders)
        {
            if (includeSubfolders)
                return assetPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase)
                    || assetPath.StartsWith(folderPath + "/", StringComparison.OrdinalIgnoreCase);
            else
                return assetPath.Equals(folderPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void CloseTabsForAssets(List<AssetEntry> assets)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;

                var mainWindowType = mainWindow.GetType();
                var tabControlField = AccessTools.Field(mainWindowType, "tabControl");
                var removeTabMethod = AccessTools.Method(mainWindowType, "RemoveTab");
                var tabControl = tabControlField?.GetValue(mainWindow) as TabControl;

                if (tabControl == null || removeTabMethod == null) return;

                var assetNames = new HashSet<string>(assets.Select(a => a.Name));
                var tabsToRemove = new List<object>();

                for (int i = 1; i < tabControl.Items.Count; i++)
                {
                    var tabItem = tabControl.Items[i];
                    var tabIdProp = tabItem?.GetType().GetProperty("TabId");
                    string tabId = tabIdProp?.GetValue(tabItem) as string;
                    if (tabId != null && assetNames.Contains(tabId))
                        tabsToRemove.Add(tabItem);
                }

                foreach (var tab in tabsToRemove)
                    removeTabMethod.Invoke(mainWindow, new[] { tab });
            }
            catch { /* non-critical — tabs just stay open */ }
        }
    }
}
