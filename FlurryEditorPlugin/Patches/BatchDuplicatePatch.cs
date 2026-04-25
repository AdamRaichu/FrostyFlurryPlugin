using Flurry.Editor.Windows;
using DuplicationPlugin;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flurry.Editor.Patches
{
    /// <summary>
    /// Patches the Data Explorer's context menu to add a "Batch Duplicate" option
    /// that works when multiple assets are selected (requires pluginm for multi-select).
    /// </summary>
    [HarmonyPatch(typeof(FrostyDataExplorer))]
    [HarmonyPatchCategory("flurry.editor")]
    public class BatchDuplicatePatch
    {
        [HarmonyPatch("OnApplyTemplate")]
        [HarmonyPostfix]
        public static void AddBatchDuplicateMenuItem(FrostyDataExplorer __instance)
        {
            if (__instance != App.EditorWindow?.DataExplorer)
                return;

            ContextMenu cm = __instance.AssetContextMenu;
            if (cm == null)
                return;

            cm.Opened += (s, e) =>
            {
                MenuItem batchDupeItem = null;
                foreach (var item in cm.Items)
                {
                    if (item is MenuItem mi && mi.Header?.ToString() == "Batch Duplicate")
                    {
                        batchDupeItem = mi;
                        break;
                    }
                }

                if (batchDupeItem == null)
                {
                    ImageSourceConverter converter = new ImageSourceConverter();
                    batchDupeItem = new MenuItem()
                    {
                        Header = "Batch Duplicate",
                        Icon = new Image()
                        {
                            Source = converter.ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Add.png") as ImageSource,
                            Opacity = 0.5
                        }
                    };
                    batchDupeItem.Click += BatchDuplicate_Click;
                    cm.Items.Add(batchDupeItem);
                }

                IList<AssetEntry> selectedAssets = __instance.SelectedAssets;
                batchDupeItem.Visibility = (selectedAssets != null && selectedAssets.Count > 1)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };
        }

        private static void BatchDuplicate_Click(object sender, RoutedEventArgs e)
        {
            IList<AssetEntry> selectedAssets = App.EditorWindow.DataExplorer.SelectedAssets;
            if (selectedAssets == null || selectedAssets.Count <= 1)
                return;

            List<EbxAssetEntry> entries = selectedAssets.OfType<EbxAssetEntry>().ToList();
            if (entries.Count == 0)
                return;

            // Find common parent path of all selected assets
            string commonPath = GetCommonPath(entries);

            // Ask user for destination path in the asset tree
            string destPath = SimpleInputDialog.Show(
                "Batch Duplicate",
                $"Duplicating {entries.Count} assets.\n\nEnter the destination path in the asset tree.",
                commonPath,
                Application.Current.MainWindow);

            if (destPath == null)
                return;

            destPath = destPath.Replace('\\', '/').Trim('/');

            // Optional find/replace for renaming
            string findText = SimpleInputDialog.Show(
                "Batch Duplicate - Rename (Optional)",
                "Enter text to find in asset filenames.\nLeave empty to keep original names.",
                "",
                Application.Current.MainWindow);

            if (findText == null)
                return; // User cancelled

            string replaceText = "";
            if (!string.IsNullOrEmpty(findText))
            {
                replaceText = SimpleInputDialog.Show(
                    "Batch Duplicate - Rename",
                    $"Replace \"{findText}\" with:",
                    "",
                    Application.Current.MainWindow);

                if (replaceText == null)
                    return; // User cancelled
            }

            bool doRename = !string.IsNullOrEmpty(findText);
            Dictionary<string, DuplicationTool.DuplicateAssetExtension> extensions = BuildDuplicationExtensions();

            int duplicated = 0;
            int failed = 0;

            FrostyTaskWindow.Show("Batch Duplicate", "", (task) =>
            {
                if (!MeshVariationDb.IsLoaded)
                    MeshVariationDb.LoadVariations(task);

                for (int i = 0; i < entries.Count; i++)
                {
                    EbxAssetEntry entry = entries[i];
                    task.Update($"Duplicating {entry.Filename} ({i + 1}/{entries.Count})");

                    try
                    {
                        string filename = entry.Filename;

                        // Apply find/replace if user provided one
                        if (doRename)
                        {
                            // Case-insensitive find, case-aware replace
                            int idx = filename.IndexOf(findText, StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                // Match the casing of the first character
                                string matched = filename.Substring(idx, findText.Length);
                                string casedReplace = replaceText;
                                if (casedReplace.Length > 0 && matched.Length > 0)
                                {
                                    char first = char.IsUpper(matched[0])
                                        ? char.ToUpper(casedReplace[0])
                                        : char.ToLower(casedReplace[0]);
                                    casedReplace = first + casedReplace.Substring(1);
                                }
                                filename = filename.Substring(0, idx) + casedReplace + filename.Substring(idx + findText.Length);
                            }
                        }

                        string newName = string.IsNullOrEmpty(destPath)
                            ? filename
                            : destPath + "/" + filename;

                        if (App.AssetManager.GetEbxEntry(newName) != null)
                        {
                            App.Logger.LogWarning($"Skipping {entry.Name}: {newName} already exists");
                            failed++;
                            continue;
                        }

                        DuplicateAsset(entry, newName, extensions);
                        duplicated++;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogWarning($"Failed to duplicate {entry.Name}: {ex.Message}");
                        failed++;
                    }
                }
            });

            App.Logger.Log($"Batch Duplicate complete: {duplicated} duplicated, {failed} failed/skipped.");
            App.EditorWindow.DataExplorer.RefreshAll();
        }

        private static string GetCommonPath(List<EbxAssetEntry> entries)
        {
            if (entries.Count == 0)
                return "";

            string[] first = entries[0].Path.Split('/');
            int commonLength = first.Length;

            for (int i = 1; i < entries.Count; i++)
            {
                string[] parts = entries[i].Path.Split('/');
                commonLength = Math.Min(commonLength, parts.Length);
                for (int j = 0; j < commonLength; j++)
                {
                    if (!string.Equals(first[j], parts[j], StringComparison.OrdinalIgnoreCase))
                    {
                        commonLength = j;
                        break;
                    }
                }
            }

            return string.Join("/", first.Take(commonLength));
        }

        private static Dictionary<string, DuplicationTool.DuplicateAssetExtension> BuildDuplicationExtensions()
        {
            var extensions = new Dictionary<string, DuplicationTool.DuplicateAssetExtension>(StringComparer.OrdinalIgnoreCase);
            Type extensionBaseType = typeof(DuplicationTool.DuplicateAssetExtension);
            var assembly = extensionBaseType.Assembly;

            foreach (Type type in assembly.GetTypes())
            {
                if (!type.IsSubclassOf(extensionBaseType) || type.IsAbstract)
                    continue;

                try
                {
                    var extension = (DuplicationTool.DuplicateAssetExtension)Activator.CreateInstance(type);
                    if (extension?.AssetType != null && !extensions.ContainsKey(extension.AssetType))
                        extensions.Add(extension.AssetType, extension);
                }
                catch
                {
                    // Ignore extensions that fail to instantiate; we'll fall back to EBX-only duplication.
                }
            }

            return extensions;
        }

        private static EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName,
            Dictionary<string, DuplicationTool.DuplicateAssetExtension> extensions)
        {
            foreach (var kvp in extensions)
            {
                if (TypeLibrary.IsSubClassOf(entry.Type, kvp.Key))
                {
                    return kvp.Value.DuplicateAsset(entry, newName, false, null);
                }
            }

            // Fallback for asset types without a dedicated duplication extension.
            return DuplicateEbxOnly(entry, newName);
        }

        private static EbxAssetEntry DuplicateEbxOnly(EbxAssetEntry entry, string newName)
        {
            EbxAsset asset = App.AssetManager.GetEbx(entry);

            EbxAsset newAsset;
            using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream(), EbxWriteFlags.DoNotSort))
            {
                writer.WriteAsset(asset);
                byte[] buf = writer.ToByteArray();
                using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(buf)))
                    newAsset = reader.ReadAsset<EbxAsset>();
            }

            newAsset.SetFileGuid(Guid.NewGuid());

            dynamic obj = newAsset.RootObject;
            obj.Name = newName;

            AssetClassGuid guid = new AssetClassGuid(
                Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
            obj.SetInstanceGuid(guid);

            EbxAssetEntry newEntry = App.AssetManager.AddEbx(newName, newAsset);
            newEntry.AddedBundles.AddRange(entry.EnumerateBundles());
            newEntry.ModifiedEntry.DependentAssets.AddRange(newAsset.Dependencies);

            return newEntry;
        }
    }
}
