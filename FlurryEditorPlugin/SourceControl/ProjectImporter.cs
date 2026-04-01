using Frosty.Core;
using Frosty.Core.Mod;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Flurry.Editor
{
    public static class ProjectImporter
    {
        public static bool ImportDirectory(FrostyProject project, string path)
        {
            string projectJsonPath = Path.Combine(path, "project.fxproject");
            if (!File.Exists(projectJsonPath))
            {
                SCLog.Error("No project.fxproject found in: " + path);
                return false;
            }

            ProjectJson projectJson;
            try
            {
                string json = File.ReadAllText(projectJsonPath);
                projectJson = JsonConvert.DeserializeObject<ProjectJson>(json);
            }
            catch (Exception ex)
            {
                SCLog.Error("Failed to read project.fxproject: " + ex.Message);
                return false;
            }

            if (projectJson == null)
            {
                SCLog.Error("project.fxproject deserialized to null");
                return false;
            }

            if (!string.Equals(projectJson.GameProfile, ProfilesLibrary.ProfileName, StringComparison.OrdinalIgnoreCase))
            {
                SCLog.Error("Project game profile '" + projectJson.GameProfile +
                    "' does not match current profile '" + ProfilesLibrary.ProfileName + "'");
                return false;
            }

            SCLog.Verbose("Loading project: " + projectJson.GameProfile + " v" + projectJson.GameVersion);

            ModSettings ms = project.ModSettings;
            if (projectJson.ModSettings != null)
            {
                ms.Title = projectJson.ModSettings.Title ?? "";
                ms.Author = projectJson.ModSettings.Author ?? "";
                ms.Version = projectJson.ModSettings.Version ?? "";
                ms.Category = projectJson.ModSettings.Category ?? "";
                ms.Description = projectJson.ModSettings.Description ?? "";
            }

            LoadModImages(ms, path);

            ms.ClearDirtyFlag();

            project.gameVersion = projectJson.GameVersion;

            Dictionary<int, AssetEntry> h32map = new Dictionary<int, AssetEntry>();

            if (projectJson.AddedBundles != null && projectJson.AddedBundles.Count > 0)
            {
                SCLog.Verbose("Adding " + projectJson.AddedBundles.Count + " bundles...");
                foreach (AddedBundleJson bundle in projectJson.AddedBundles)
                {
                    App.AssetManager.AddBundle(bundle.Name, (BundleType)bundle.Type,
                        App.AssetManager.GetSuperBundleId(bundle.SuperBundle));
                    SCLog.Verbose("  Bundle: " + bundle.Name);
                }
            }

            if (projectJson.AddedEbx != null && projectJson.AddedEbx.Count > 0)
            {
                SCLog.Verbose("Adding " + projectJson.AddedEbx.Count + " EBX entries...");
                foreach (AddedEbxJson ebx in projectJson.AddedEbx)
                {
                    EbxAssetEntry entry = new EbxAssetEntry
                    {
                        Name = ebx.Name,
                        Guid = Guid.Parse(ebx.Guid)
                    };
                    App.AssetManager.AddEbx(entry);
                    SCLog.Verbose("  Added EBX: " + ebx.Name);
                }
            }

            if (projectJson.AddedRes != null && projectJson.AddedRes.Count > 0)
            {
                SCLog.Verbose("Adding " + projectJson.AddedRes.Count + " RES entries...");
                foreach (AddedResJson res in projectJson.AddedRes)
                {
                    ResAssetEntry entry = new ResAssetEntry
                    {
                        Name = res.Name,
                        ResRid = res.ResRid,
                        ResType = res.ResType,
                        ResMeta = res.ResMeta != null ? Convert.FromBase64String(res.ResMeta) : new byte[0x10]
                    };
                    App.AssetManager.AddRes(entry);
                    SCLog.Verbose("  Added RES: " + res.Name);
                }
            }

            if (projectJson.AddedChunks != null && projectJson.AddedChunks.Count > 0)
            {
                SCLog.Verbose("Adding " + projectJson.AddedChunks.Count + " chunk entries...");
                foreach (AddedChunkJson chunk in projectJson.AddedChunks)
                {
                    ChunkAssetEntry entry = new ChunkAssetEntry
                    {
                        Id = Guid.Parse(chunk.Id),
                        H32 = chunk.H32
                    };
                    App.AssetManager.AddChunk(entry);
                    SCLog.Verbose("  Added Chunk: " + chunk.Id);
                }
            }

            SCLog.Verbose("Loading modified assets...");
            LoadModifiedEbx(path, h32map);
            LoadModifiedRes(path, h32map);
            LoadModifiedChunks(path, h32map);
            LoadLegacyHandlers(path);

            SCLog.Log("Project loaded from exploded directory: " + path);
            return true;
        }

        #region Mod Images

        private static void LoadModImages(ModSettings ms, string basePath)
        {
            string iconPath = Path.Combine(basePath, "icon.png");
            if (File.Exists(iconPath))
                ms.Icon = File.ReadAllBytes(iconPath);

            for (int i = 0; i < 4; i++)
            {
                string screenshotPath = Path.Combine(basePath, "screenshot_" + i + ".png");
                if (File.Exists(screenshotPath))
                    ms.SetScreenshot(i, File.ReadAllBytes(screenshotPath));
            }
        }

        #endregion

        #region Modified EBX

        private static void LoadModifiedEbx(string basePath, Dictionary<int, AssetEntry> h32map)
        {
            string ebxDir = Path.Combine(basePath, "ebx");
            if (!Directory.Exists(ebxDir))
                return;

            foreach (string metaFile in Directory.GetFiles(ebxDir, "*.meta.json", SearchOption.AllDirectories))
            {
                EbxMetaJson meta;
                try
                {
                    meta = JsonConvert.DeserializeObject<EbxMetaJson>(File.ReadAllText(metaFile));
                }
                catch (Exception ex)
                {
                    SCLog.Warn(" Failed to read EBX meta: " + metaFile + " - " + ex.Message);
                    continue;
                }

                EbxAssetEntry entry = App.AssetManager.GetEbxEntry(meta.Name);
                if (entry == null)
                {
                    SCLog.Warn(" EBX entry not found: " + meta.Name);
                    continue;
                }

                entry.LinkedAssets.AddRange(ResolveLinkedAssets(meta.LinkedAssets));

                foreach (string bundleName in meta.AddedBundles)
                {
                    int bid = App.AssetManager.GetBundleId(bundleName);
                    if (bid != -1)
                        entry.AddedBundles.Add(bid);
                }

                string baseName = metaFile.Substring(0, metaFile.Length - ".meta.json".Length);
                string xmlPath = baseName + ".xml";
                string datPath = baseName + ".dat";

                bool hasData = false;

                if (meta.IsCustomHandler && File.Exists(datPath))
                {
                    byte[] data = File.ReadAllBytes(datPath);
                    entry.ModifiedEntry = new ModifiedAssetEntry
                    {
                        IsTransientModified = meta.IsTransientModified,
                        UserData = meta.UserData,
                        DataObject = ModifiedResource.Read(data)
                    };
                    hasData = true;
                    SCLog.Verbose("  EBX [custom handler] " + meta.Name + " (" + data.Length + " bytes)");
                }
                else if (File.Exists(xmlPath))
                {
                    try
                    {
                        var dbxReader = new DbxReader(xmlPath);
                        EbxAsset asset = dbxReader.ReadAsset();
                        asset.Update();
                        SCLog.Verbose("  EBX [xml] " + meta.Name + " (" + asset.Objects.Count() + " objects)");

                        entry.ModifiedEntry = new ModifiedAssetEntry
                        {
                            IsTransientModified = meta.IsTransientModified,
                            UserData = meta.UserData,
                            DataObject = asset
                        };

                        if (entry.IsAdded)
                            entry.Type = asset.RootObject.GetType().Name;
                        entry.ModifiedEntry.DependentAssets.AddRange(asset.Dependencies);

                        hasData = true;
                    }
                    catch (Exception ex)
                    {
                        SCLog.Warn(" Failed to read EBX XML: " + xmlPath + " - " + ex);
                    }
                }

                if (hasData)
                    entry.OnModified();

                int hash = Fnv1.HashString(entry.Name);
                if (!h32map.ContainsKey(hash))
                    h32map.Add(hash, entry);
            }
        }

        #endregion

        #region Modified RES

        private static void LoadModifiedRes(string basePath, Dictionary<int, AssetEntry> h32map)
        {
            string resDir = Path.Combine(basePath, "res");
            if (!Directory.Exists(resDir))
                return;

            foreach (string metaFile in Directory.GetFiles(resDir, "*.meta.json", SearchOption.AllDirectories))
            {
                ResMetaJson meta;
                try
                {
                    meta = JsonConvert.DeserializeObject<ResMetaJson>(File.ReadAllText(metaFile));
                }
                catch (Exception ex)
                {
                    SCLog.Warn(" Failed to read RES meta: " + metaFile + " - " + ex.Message);
                    continue;
                }

                ResAssetEntry entry = App.AssetManager.GetResEntry(meta.Name);
                if (entry == null)
                {
                    SCLog.Warn(" RES entry not found: " + meta.Name);
                    continue;
                }

                entry.LinkedAssets.AddRange(ResolveLinkedAssets(meta.LinkedAssets));

                foreach (string bundleName in meta.AddedBundles)
                {
                    int bid = App.AssetManager.GetBundleId(bundleName);
                    if (bid != -1)
                        entry.AddedBundles.Add(bid);
                }

                string baseName = metaFile.Substring(0, metaFile.Length - ".meta.json".Length);
                string datPath = baseName + ".dat";

                if (File.Exists(datPath))
                {
                    Sha1 sha1 = !string.IsNullOrEmpty(meta.Sha1) ? new Sha1(meta.Sha1) : Sha1.Zero;

                    entry.ModifiedEntry = new ModifiedAssetEntry
                    {
                        Sha1 = sha1,
                        OriginalSize = meta.OriginalSize,
                        ResMeta = meta.ResMeta != null ? Convert.FromBase64String(meta.ResMeta) : null,
                        UserData = meta.UserData
                    };

                    byte[] data = File.ReadAllBytes(datPath);

                    if (sha1 == Sha1.Zero && meta.IsCustomHandler)
                    {
                        entry.ModifiedEntry.DataObject = ModifiedResource.Read(data);
                    }
                    else
                    {
                        entry.ModifiedEntry.Data = data;
                    }

                    entry.OnModified();
                    SCLog.Verbose("  RES " + (meta.IsCustomHandler ? "[custom handler] " : "") + meta.Name + " (" + data.Length + " bytes)");
                }

                int hash = Fnv1.HashString(entry.Name);
                if (!h32map.ContainsKey(hash))
                    h32map.Add(hash, entry);
            }
        }

        #endregion

        #region Modified Chunks

        private static void LoadModifiedChunks(string basePath, Dictionary<int, AssetEntry> h32map)
        {
            string chunksDir = Path.Combine(basePath, "chunks");
            if (!Directory.Exists(chunksDir))
                return;

            foreach (string metaFile in Directory.GetFiles(chunksDir, "*.meta.json", SearchOption.AllDirectories))
            {
                ChunkMetaJson meta;
                try
                {
                    meta = JsonConvert.DeserializeObject<ChunkMetaJson>(File.ReadAllText(metaFile));
                }
                catch (Exception ex)
                {
                    SCLog.Warn(" Failed to read chunk meta: " + metaFile + " - " + ex.Message);
                    continue;
                }

                Guid id = Guid.Parse(meta.Id);
                ChunkAssetEntry entry = App.AssetManager.GetChunkEntry(id);

                if (entry == null)
                {
                    ChunkAssetEntry newEntry = new ChunkAssetEntry
                    {
                        Id = id,
                        H32 = meta.H32
                    };
                    App.AssetManager.AddChunk(newEntry);

                    if (h32map.ContainsKey(newEntry.H32))
                    {
                        foreach (int bundleId in h32map[newEntry.H32].Bundles)
                            newEntry.AddToBundle(bundleId);
                    }
                    entry = newEntry;
                }

                foreach (string bundleName in meta.AddedBundles)
                {
                    int bid = App.AssetManager.GetBundleId(bundleName);
                    if (bid != -1)
                        entry.AddedBundles.Add(bid);
                }

                string datPath = Path.Combine(chunksDir, meta.Id + ".dat");
                if (File.Exists(datPath))
                {
                    entry.ModifiedEntry = new ModifiedAssetEntry
                    {
                        Sha1 = !string.IsNullOrEmpty(meta.Sha1) ? new Sha1(meta.Sha1) : Sha1.Zero,
                        LogicalOffset = meta.LogicalOffset,
                        LogicalSize = meta.LogicalSize,
                        RangeStart = meta.RangeStart,
                        RangeEnd = meta.RangeEnd,
                        FirstMip = meta.FirstMip,
                        H32 = meta.H32,
                        AddToChunkBundle = meta.AddToChunkBundle,
                        UserData = meta.UserData,
                        Data = File.ReadAllBytes(datPath)
                    };
                    entry.OnModified();
                    SCLog.Verbose("  Chunk " + meta.Id + " (" + entry.ModifiedEntry.Data.Length + " bytes)");
                }
                else
                {
                    entry.H32 = meta.H32;
                    entry.FirstMip = meta.FirstMip;
                }
            }
        }

        #endregion

        #region Legacy Handlers

        private static void LoadLegacyHandlers(string basePath)
        {
            string handlerPath = Path.Combine(basePath, "legacy_handlers.dat");
            if (!File.Exists(handlerPath))
                return;

            try
            {
                byte[] data = File.ReadAllBytes(handlerPath);
                using (NativeReader reader = new NativeReader(new MemoryStream(data)))
                {
                    string typeString = reader.ReadNullTerminatedString();
                    var handler = new Frosty.Core.Handlers.LegacyCustomActionHandler();
                    handler.LoadFromProject(14, reader, typeString);
                }
            }
            catch (Exception ex)
            {
                SCLog.Warn(" Failed to load legacy handlers: " + ex.Message);
            }
        }

        #endregion

        #region Helpers

        private static List<AssetEntry> ResolveLinkedAssets(List<LinkedAssetJson> linkedAssets)
        {
            var result = new List<AssetEntry>();
            if (linkedAssets == null) return result;

            foreach (LinkedAssetJson la in linkedAssets)
            {
                AssetEntry resolved = null;

                switch (la.Type)
                {
                    case "ebx":
                        resolved = App.AssetManager.GetEbxEntry(la.Name);
                        break;
                    case "res":
                        resolved = App.AssetManager.GetResEntry(la.Name);
                        break;
                    case "chunk":
                        if (la.Id != null)
                            resolved = App.AssetManager.GetChunkEntry(Guid.Parse(la.Id));
                        break;
                    default:
                        if (la.Name != null)
                            resolved = App.AssetManager.GetCustomAssetEntry(la.Type, la.Name);
                        break;
                }

                if (resolved != null)
                    result.Add(resolved);
            }

            return result;
        }

        #endregion
    }
}
