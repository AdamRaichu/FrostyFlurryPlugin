using Flurry.Editor.Windows;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace Flurry.Editor
{
    /// <summary>
    /// Deep Duplicate: Duplicates an asset along with all the assets it depends on
    /// (textures, meshes, materials, etc.), updating internal references so the
    /// duplicated set is fully self-contained.
    ///
    /// Particularly useful for meshes inside ObjectBlueprints — duplicating a mesh
    /// and all its referenced sub-assets in one operation.
    /// </summary>
    public class DeepDuplicateMenuExt : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Flurry";
        public override string MenuItemName => "Deep Duplicate (with Dependencies)";
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Add.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
        {
            EbxAssetEntry selectedEntry = App.SelectedAsset as EbxAssetEntry;
            if (selectedEntry == null)
            {
                FrostyMessageBox.Show("Please select an EBX asset first.", "Deep Duplicate", MessageBoxButton.OK);
                return;
            }

            // Ask for destination path first
            string destPath = SimpleInputDialog.Show(
                "Deep Duplicate",
                $"Selected: {selectedEntry.Name} ({selectedEntry.Type})\n\nEnter destination path for duplicated asset(s):",
                selectedEntry.Path,
                Application.Current.MainWindow);

            if (destPath == null)
                return;

            destPath = destPath.Replace('\\', '/').Trim('/');

            // Collect dependencies and ask if user wants to include them
            EbxAsset rootAsset = App.AssetManager.GetEbx(selectedEntry);
            List<EbxAssetEntry> dependencies = new List<EbxAssetEntry>();
            bool includeDeps = false;

            foreach (Guid depGuid in rootAsset.Dependencies)
            {
                EbxAssetEntry depEntry = App.AssetManager.GetEbxEntry(depGuid);
                if (depEntry != null)
                    dependencies.Add(depEntry);
            }

            if (dependencies.Count > 0)
            {
                string depList = "";
                foreach (var dep in dependencies)
                    depList += $"\n  {dep.Filename} ({dep.Type})";

                MessageBoxResult result = FrostyMessageBox.Show(
                    $"This asset has {dependencies.Count} dependency asset(s):\n{depList}\n\n" +
                    "Include dependencies in duplication?\n\n" +
                    "Yes = duplicate everything and rewrite references\n" +
                    "No = duplicate only the selected asset",
                    "Deep Duplicate - Include Dependencies?",
                    MessageBoxButton.YesNoCancel);

                if (result == MessageBoxResult.Cancel)
                    return;

                includeDeps = (result == MessageBoxResult.Yes);
            }

            int duplicated = 0;
            int failed = 0;

            Dictionary<Guid, Guid> guidMap = new Dictionary<Guid, Guid>();
            Dictionary<Guid, string> nameMap = new Dictionary<Guid, string>();

            FrostyTaskWindow.Show("Deep Duplicate", "", (task) =>
            {
                // Phase 1: Duplicate dependencies if requested
                if (includeDeps)
                {
                    for (int i = 0; i < dependencies.Count; i++)
                    {
                        EbxAssetEntry dep = dependencies[i];
                        task.Update($"Duplicating {dep.Filename} ({i + 1}/{dependencies.Count + 1})");

                        string newName = string.IsNullOrEmpty(destPath)
                            ? dep.Filename
                            : destPath + "/" + dep.Filename;

                        string baseName = newName;
                        int suffix = 1;
                        while (App.AssetManager.GetEbxEntry(newName) != null)
                        {
                            newName = baseName + "_" + suffix;
                            suffix++;
                        }

                        try
                        {
                            EbxAssetEntry newDep = DuplicateEbxAsset(dep, newName);
                            guidMap[dep.Guid] = newDep.Guid;
                            nameMap[dep.Guid] = newDep.Name;
                            duplicated++;
                        }
                        catch (Exception ex)
                        {
                            App.Logger.LogWarning($"Failed to duplicate dependency {dep.Name}: {ex.Message}");
                            failed++;
                        }
                    }
                }

                // Phase 2: Duplicate the root asset
                task.Update($"Duplicating {selectedEntry.Filename}...");
                {
                    string newName = string.IsNullOrEmpty(destPath)
                        ? selectedEntry.Filename
                        : destPath + "/" + selectedEntry.Filename;

                    string baseName = newName;
                    int suffix = 1;
                    while (App.AssetManager.GetEbxEntry(newName) != null)
                    {
                        newName = baseName + "_" + suffix;
                        suffix++;
                    }

                    try
                    {
                        EbxAssetEntry newRoot = DuplicateEbxAsset(selectedEntry, newName);
                        guidMap[selectedEntry.Guid] = newRoot.Guid;
                        nameMap[selectedEntry.Guid] = newRoot.Name;
                        duplicated++;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.LogWarning($"Failed to duplicate root asset {selectedEntry.Name}: {ex.Message}");
                        failed++;
                    }
                }

                // Phase 3: Rewrite references if dependencies were included
                if (includeDeps && guidMap.Count > 1)
                {
                    task.Update("Updating internal references...");
                    foreach (var kvp in guidMap)
                    {
                        string newName = nameMap[kvp.Key];
                        EbxAssetEntry newEntry = App.AssetManager.GetEbxEntry(newName);
                        if (newEntry == null)
                            continue;

                        EbxAsset asset = App.AssetManager.GetEbx(newEntry);
                        bool modified = false;

                        foreach (object obj in asset.Objects)
                        {
                            if (RewriteReferences(obj, guidMap) > 0)
                                modified = true;
                        }

                        if (modified)
                            App.AssetManager.ModifyEbx(newEntry.Name, asset);
                    }
                }
            });

            App.Logger.Log($"Deep Duplicate complete: {duplicated} assets duplicated, {failed} failed.");
            App.EditorWindow.DataExplorer.RefreshAll();
        });

        private static EbxAssetEntry DuplicateEbxAsset(EbxAssetEntry entry, string newName)
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

        /// <summary>
        /// Walks all PointerRef fields in an object and replaces any external references
        /// whose FileGuid is in the guidMap with the corresponding new GUID.
        /// Returns the count of replaced references.
        /// </summary>
        private static int RewriteReferences(object obj, Dictionary<Guid, Guid> guidMap)
        {
            if (obj == null)
                return 0;

            int count = 0;
            Type type = obj.GetType();

            foreach (PropertyInfo pi in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!pi.CanRead || !pi.CanWrite)
                    continue;

                try
                {
                    if (pi.PropertyType == typeof(PointerRef))
                    {
                        PointerRef pr = (PointerRef)pi.GetValue(obj);
                        if (pr.Type == PointerRefType.External && guidMap.TryGetValue(pr.External.FileGuid, out Guid newGuid))
                        {
                            EbxImportReference newRef = new EbxImportReference
                            {
                                FileGuid = newGuid,
                                ClassGuid = pr.External.ClassGuid
                            };
                            pi.SetValue(obj, new PointerRef(newRef));
                            count++;
                        }
                    }
                    else if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        Type elementType = pi.PropertyType.GetGenericArguments()[0];

                        if (elementType == typeof(PointerRef))
                        {
                            IList list = (IList)pi.GetValue(obj);
                            if (list != null)
                            {
                                for (int i = 0; i < list.Count; i++)
                                {
                                    PointerRef pr = (PointerRef)list[i];
                                    if (pr.Type == PointerRefType.External && guidMap.TryGetValue(pr.External.FileGuid, out Guid newGuid))
                                    {
                                        EbxImportReference newRef = new EbxImportReference
                                        {
                                            FileGuid = newGuid,
                                            ClassGuid = pr.External.ClassGuid
                                        };
                                        list[i] = new PointerRef(newRef);
                                        count++;
                                    }
                                }
                            }
                        }
                        else if (!elementType.IsPrimitive && !elementType.IsEnum && elementType != typeof(string))
                        {
                            IList list = (IList)pi.GetValue(obj);
                            if (list != null)
                            {
                                foreach (object item in list)
                                {
                                    if (item != null && !item.GetType().IsPrimitive)
                                        count += RewriteReferences(item, guidMap);
                                }
                            }
                        }
                    }
                    else if (pi.PropertyType.IsClass && pi.PropertyType != typeof(string) && !pi.PropertyType.IsArray)
                    {
                        object child = pi.GetValue(obj);
                        if (child != null)
                            count += RewriteReferences(child, guidMap);
                    }
                    else if (pi.PropertyType.IsValueType && !pi.PropertyType.IsPrimitive && !pi.PropertyType.IsEnum
                             && pi.PropertyType != typeof(Guid) && pi.PropertyType != typeof(PointerRef))
                    {
                        object child = pi.GetValue(obj);
                        if (child != null)
                            count += RewriteReferences(child, guidMap);
                    }
                }
                catch
                {
                    // Skip inaccessible properties
                }
            }

            return count;
        }
    }
}
