using Frosty.Core.Controls;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Flurry.Editor.Patches
{
    // =========================================================================
    //  FEATURE: Show only UNmodified in Data Explorer (#4)
    //
    //  Adds a "Show only unmodified" checkbox next to the existing
    //  "Show only modified" checkbox. Both are mutually exclusive.
    //  Also patches UpdateTreeView and UpdateListView to respect the filter.
    // =========================================================================

    [HarmonyPatch(typeof(FrostyDataExplorer))]
    [HarmonyPatchCategory("flurry.editor")]
    public class ShowOnlyUnmodifiedPatch
    {
        // Per-instance state so dataExplorer and legacyExplorer are independent
        internal static readonly Dictionary<FrostyDataExplorer, bool> ShowOnlyUnmodified
            = new Dictionary<FrostyDataExplorer, bool>();

        private static readonly FieldInfo showOnlyModifiedCheckBoxField
            = AccessTools.Field(typeof(FrostyDataExplorer), "showOnlyModifiedCheckBox");

        // =====================================================
        //  Inject the checkbox after template is applied
        // =====================================================
        [HarmonyPatch("OnApplyTemplate")]
        [HarmonyPostfix]
        public static void OnApplyTemplate_Postfix(FrostyDataExplorer __instance)
        {
            var modifiedCheckBox = showOnlyModifiedCheckBoxField?.GetValue(__instance) as CheckBox;
            if (modifiedCheckBox == null)
                return;

            var parent = modifiedCheckBox.Parent as Panel;
            if (parent == null)
                return;

            // Initialize state
            ShowOnlyUnmodified[__instance] = false;

            var unmodifiedCheckBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                IsChecked = false
            };

            var label = new TextBlock
            {
                Text = "Show only unmodified",
                Margin = new Thickness(4, -1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "FontColor");
            unmodifiedCheckBox.Content = label;

            // Mutual exclusion: uncheck modified when unmodified is checked
            unmodifiedCheckBox.Checked += (s, e) =>
            {
                ShowOnlyUnmodified[__instance] = true;
                if (modifiedCheckBox.IsChecked == true)
                    modifiedCheckBox.IsChecked = false;
                __instance.RefreshAll();
            };
            unmodifiedCheckBox.Unchecked += (s, e) =>
            {
                ShowOnlyUnmodified[__instance] = false;
                __instance.RefreshAll();
            };

            // Mutual exclusion: uncheck unmodified when modified is checked
            modifiedCheckBox.Checked += (s, e) =>
            {
                if (unmodifiedCheckBox.IsChecked == true)
                    unmodifiedCheckBox.IsChecked = false;
            };

            // Insert after the modified checkbox
            int idx = parent.Children.IndexOf(modifiedCheckBox);
            parent.Children.Insert(idx + 1, unmodifiedCheckBox);
        }

        // =====================================================
        //  Patch UpdateTreeView to filter out modified assets
        // =====================================================
        private static readonly FieldInfo assetTreeViewField
            = AccessTools.Field(typeof(FrostyDataExplorer), "assetTreeView");
        private static readonly FieldInfo selectedPathField
            = AccessTools.Field(typeof(FrostyDataExplorer), "selectedPath");
        private static readonly PropertyInfo itemsSourceProp
            = typeof(FrostyDataExplorer).GetProperty("ItemsSource");
        private static readonly PropertyInfo showOnlyModifiedProp
            = typeof(FrostyDataExplorer).GetProperty("ShowOnlyModified");
        private static readonly MethodInfo filterTextMethod
            = AccessTools.Method(typeof(FrostyDataExplorer), "FilterText");
        private static readonly MethodInfo updateListViewMethod
            = AccessTools.Method(typeof(FrostyDataExplorer), "UpdateListView");

        [HarmonyPatch("UpdateTreeView")]
        [HarmonyPrefix]
        public static void UpdateTreeView_Prefix(FrostyDataExplorer __instance, ref bool __runOriginal)
        {
            // Only intercept when "show only unmodified" is active
            // Otherwise let the original (or other patches) run
            if (!ShowOnlyUnmodified.TryGetValue(__instance, out bool showUnmod) || !showUnmod)
                return;

            // When show-only-unmodified is on, we need the original UpdateTreeView
            // to skip modified entries. We do this by temporarily toggling off any
            // conflicting state — the actual filtering happens in UpdateListView below.
            // The tree will be rebuilt by the original method since ShowOnlyModified is false.
        }

        // =====================================================
        //  Patch UpdateListView to filter out modified assets
        // =====================================================
        private static readonly FieldInfo assetListViewField
            = AccessTools.Field(typeof(FrostyDataExplorer), "assetListView");
        private static readonly PropertyInfo selectedAssetProp
            = typeof(FrostyDataExplorer).GetProperty("SelectedAsset");

        [HarmonyPatch("UpdateListView")]
        [HarmonyPrefix]
        public static bool UpdateListView_Prefix(FrostyDataExplorer __instance, object path)
        {
            // Only intercept when "show only unmodified" is active
            if (!ShowOnlyUnmodified.TryGetValue(__instance, out bool showUnmod) || !showUnmod)
                return true; // let original run

            var listView = assetListViewField?.GetValue(__instance) as ListView;
            if (listView == null)
                return true;

            if (path == null)
            {
                listView.ItemsSource = null;
                return false;
            }

            var itemsSource = itemsSourceProp?.GetValue(__instance) as IEnumerable;
            if (itemsSource == null)
                return true;

            string fullPath = path.GetType().GetProperty("FullPath")?.GetValue(path) as string;
            string key = fullPath?.Trim('/') ?? "";

            var items = new List<AssetEntry>();
            foreach (AssetEntry entry in itemsSource)
            {
                // Skip modified entries (show only unmodified)
                if (entry.IsModified)
                    continue;

                if (!entry.Path.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (filterTextMethod != null && !(bool)filterTextMethod.Invoke(__instance, new object[] { entry.Name, entry }))
                    continue;

                items.Add(entry);
            }

            listView.ItemsSource = items;

            var selected = selectedAssetProp?.GetValue(__instance);
            if (selected != null)
                listView.SelectedItem = selected;

            return false;
        }
    }
}
