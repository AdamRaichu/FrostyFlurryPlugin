using Frosty.Core;
using Frosty.Core.Mod;
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
    public static class ProjectExporter
    {
        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static void ExportDirectory(FrostyProject project, string path, bool updateDirtyState)
        {
            SCLog.Verbose("Export started: " + path);
            Directory.CreateDirectory(path);

            ModSettings modSettings = project.ModSettings;

            WriteProjectJson(project, path);
            SCLog.Verbose("Wrote project.json");
            WriteModImages(modSettings, path);

            try
            {
                WriteEbxAssets(path, updateDirtyState);
            }
            catch (Exception ex)
            {
                SCLog.Error("Failed to write EBX assets: " + ex);
            }

            try
            {
                WriteResAssets(path, updateDirtyState);
            }
            catch (Exception ex)
            {
                SCLog.Error("Failed to write RES assets: " + ex);
            }

            try
            {
                WriteChunkAssets(path, updateDirtyState);
            }
            catch (Exception ex)
            {
                SCLog.Error("Failed to write chunk assets: " + ex);
            }

            WriteLegacyHandlers(path);
            CleanRemovedAssets(path);

            SCLog.Log("Project saved to exploded directory: " + path);
        }

        #region project.json

        private static void WriteProjectJson(FrostyProject project, string basePath)
        {
            ModSettings ms = project.ModSettings;

            var projectJson = new ProjectJson
            {
                FormatVersion = 1,
                GameProfile = ProfilesLibrary.ProfileName,
                GameVersion = App.FileSystem.Head,
                CreatedAt = DateTime.Now.ToString("o"),
                ModifiedAt = DateTime.Now.ToString("o"),
                ModSettings = new ModSettingsJson
                {
                    Title = ms.Title,
                    Author = ms.Author,
                    Version = ms.Version,
                    Category = ms.Category,
                    Description = ms.Description
                }
            };

            foreach (BundleEntry entry in App.AssetManager.EnumerateBundles(modifiedOnly: true))
            {
                if (entry.Added)
                {
                    projectJson.AddedBundles.Add(new AddedBundleJson
                    {
                        Name = entry.Name,
                        SuperBundle = App.AssetManager.GetSuperBundle(entry.SuperBundleId).Name,
                        Type = (int)entry.Type
                    });
                }
            }

            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true))
            {
                if (entry.IsAdded)
                {
                    projectJson.AddedEbx.Add(new AddedEbxJson
                    {
                        Name = entry.Name,
                        Guid = entry.Guid.ToString()
                    });
                }
            }

            foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
            {
                if (entry.IsAdded)
                {
                    projectJson.AddedRes.Add(new AddedResJson
                    {
                        Name = entry.Name,
                        ResRid = entry.ResRid,
                        ResType = entry.ResType,
                        ResMeta = entry.ResMeta != null ? Convert.ToBase64String(entry.ResMeta) : null
                    });
                }
            }

            foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
            {
                if (entry.IsAdded)
                {
                    projectJson.AddedChunks.Add(new AddedChunkJson
                    {
                        Id = entry.Id.ToString(),
                        H32 = entry.H32
                    });
                }
            }

            string json = JsonConvert.SerializeObject(projectJson, s_jsonSettings);
            File.WriteAllText(Path.Combine(basePath, "project.json"), json);
        }

        #endregion

        #region Mod Images

        private static void WriteModImages(ModSettings ms, string basePath)
        {
            if (ms.Icon != null && ms.Icon.Length > 0)
                File.WriteAllBytes(Path.Combine(basePath, "icon.png"), ms.Icon);

            for (int i = 0; i < 4; i++)
            {
                byte[] screenshot = ms.GetScreenshot(i);
                if (screenshot != null && screenshot.Length > 0)
                    File.WriteAllBytes(Path.Combine(basePath, "screenshot_" + i + ".png"), screenshot);
            }
        }

        #endregion

        #region EBX Assets

        private static void WriteEbxAssets(string basePath, bool updateDirtyState)
        {
            string ebxDir = Path.Combine(basePath, "ebx");

            int ebxCount = 0;
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true, includeLinked: true))
            {
                ebxCount++;
                string safeName = SanitizePath(entry.Name);

                var meta = new EbxMetaJson
                {
                    Name = entry.Name,
                    IsTransientModified = entry.HasModifiedData && entry.ModifiedEntry.IsTransientModified,
                    UserData = entry.HasModifiedData ? entry.ModifiedEntry.UserData : "",
                    AddedBundles = GetBundleNames(entry.AddedBundles),
                    LinkedAssets = GetLinkedAssets(entry)
                };

                if (entry.HasModifiedData)
                {
                    ModifiedResource modifiedResource = entry.ModifiedEntry.DataObject as ModifiedResource;
                    bool isCustomHandler = modifiedResource != null;
                    meta.IsCustomHandler = isCustomHandler;

                    if (isCustomHandler)
                    {
                        byte[] buf = modifiedResource.Save();
                        string datPath = Path.Combine(ebxDir, safeName + ".dat");
                        EnsureDirectory(datPath);
                        File.WriteAllBytes(datPath, buf);
                        SCLog.Verbose("  EBX [custom handler] " + entry.Name + " (" + buf.Length + " bytes)");
                    }
                    else
                    {
                        EbxAsset asset = entry.ModifiedEntry.DataObject as EbxAsset;
                        if (asset != null)
                        {
                            string xmlPath = Path.Combine(ebxDir, safeName + ".xml");
                            EnsureDirectory(xmlPath);
                            using (var dbxWriter = new DbxWriter(xmlPath))
                            {
                                dbxWriter.Write(asset);
                            }
                            SCLog.Verbose("  EBX [xml] " + entry.Name + " (" + asset.Objects.Count() + " objects)");
                        }
                        else
                        {
                            SCLog.Verbose("  EBX [linked only] " + entry.Name);
                        }
                    }

                    if (updateDirtyState)
                        entry.ModifiedEntry.IsDirty = false;
                }
                else
                {
                    SCLog.Verbose("  EBX [linked only] " + entry.Name);
                }

                string metaPath = Path.Combine(ebxDir, safeName + ".meta.json");
                EnsureDirectory(metaPath);
                File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, s_jsonSettings));

                if (updateDirtyState)
                    entry.IsDirty = false;
            }

            SCLog.Verbose("Wrote " + ebxCount + " EBX entries");
        }

        #endregion

        #region RES Assets

        private static void WriteResAssets(string basePath, bool updateDirtyState)
        {
            string resDir = Path.Combine(basePath, "res");

            int resCount = 0;
            foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
            {
                resCount++;
                string safeName = SanitizePath(entry.Name);

                var meta = new ResMetaJson
                {
                    Name = entry.Name,
                    AddedBundles = GetBundleNames(entry.AddedBundles),
                    LinkedAssets = GetLinkedAssets(entry)
                };

                if (entry.HasModifiedData)
                {
                    meta.Sha1 = entry.ModifiedEntry.Sha1.ToString();
                    meta.OriginalSize = entry.ModifiedEntry.OriginalSize;
                    meta.ResMeta = entry.ModifiedEntry.ResMeta != null
                        ? Convert.ToBase64String(entry.ModifiedEntry.ResMeta)
                        : null;
                    meta.UserData = entry.ModifiedEntry.UserData;

                    byte[] buffer = entry.ModifiedEntry.Data;
                    if (entry.ModifiedEntry.DataObject != null)
                    {
                        ModifiedResource md = entry.ModifiedEntry.DataObject as ModifiedResource;
                        if (md != null)
                        {
                            buffer = md.Save();
                            meta.IsCustomHandler = true;
                        }
                    }

                    if (buffer != null)
                    {
                        string datPath = Path.Combine(resDir, safeName + ".dat");
                        EnsureDirectory(datPath);
                        File.WriteAllBytes(datPath, buffer);
                        SCLog.Verbose("  RES " + (meta.IsCustomHandler ? "[custom handler] " : "") + entry.Name + " (" + buffer.Length + " bytes)");
                    }

                    if (updateDirtyState)
                        entry.ModifiedEntry.IsDirty = false;
                }

                string metaPath = Path.Combine(resDir, safeName + ".meta.json");
                EnsureDirectory(metaPath);
                File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, s_jsonSettings));

                if (updateDirtyState)
                    entry.IsDirty = false;
            }

            SCLog.Verbose("Wrote " + resCount + " RES entries");
        }

        #endregion

        #region Chunk Assets

        private static void WriteChunkAssets(string basePath, bool updateDirtyState)
        {
            string chunksDir = Path.Combine(basePath, "chunks");

            int chunkCount = 0;
            foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
            {
                chunkCount++;
                string id = entry.Id.ToString();

                var meta = new ChunkMetaJson
                {
                    Id = id,
                    FirstMip = entry.HasModifiedData ? entry.ModifiedEntry.FirstMip : entry.FirstMip,
                    H32 = entry.HasModifiedData ? entry.ModifiedEntry.H32 : entry.H32,
                    AddedBundles = GetBundleNames(entry.AddedBundles)
                };

                if (entry.HasModifiedData)
                {
                    meta.Sha1 = entry.ModifiedEntry.Sha1.ToString();
                    meta.LogicalOffset = entry.ModifiedEntry.LogicalOffset;
                    meta.LogicalSize = entry.ModifiedEntry.LogicalSize;
                    meta.RangeStart = entry.ModifiedEntry.RangeStart;
                    meta.RangeEnd = entry.ModifiedEntry.RangeEnd;
                    meta.AddToChunkBundle = entry.ModifiedEntry.AddToChunkBundle;
                    meta.UserData = entry.ModifiedEntry.UserData;

                    if (entry.ModifiedEntry.Data != null)
                    {
                        string datPath = Path.Combine(chunksDir, id + ".dat");
                        EnsureDirectory(datPath);
                        File.WriteAllBytes(datPath, entry.ModifiedEntry.Data);
                        SCLog.Verbose("  Chunk " + id + " (" + entry.ModifiedEntry.Data.Length + " bytes)");
                    }

                    if (updateDirtyState)
                        entry.ModifiedEntry.IsDirty = false;
                }

                string metaPath = Path.Combine(chunksDir, id + ".meta.json");
                EnsureDirectory(metaPath);
                File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, s_jsonSettings));

                if (updateDirtyState)
                    entry.IsDirty = false;
            }

            SCLog.Verbose("Wrote " + chunkCount + " chunk entries");
        }

        #endregion

        #region Legacy Handlers

        private static void WriteLegacyHandlers(string basePath)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (NativeWriter writer = new NativeWriter(ms, true))
                {
                    var legacyHandler = new Frosty.Core.Handlers.LegacyCustomActionHandler();
                    legacyHandler.SaveToProject(writer);
                }

                byte[] data = ms.ToArray();
                if (data.Length > 0)
                {
                    string handlerPath = Path.Combine(basePath, "legacy_handlers.dat");
                    File.WriteAllBytes(handlerPath, data);
                }
            }
        }

        #endregion

        #region Cleanup

        private static void CleanRemovedAssets(string basePath)
        {
            HashSet<string> currentEbx = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true, includeLinked: true))
                currentEbx.Add(entry.Name.Replace('\\', '/'));

            HashSet<string> currentRes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
                currentRes.Add(entry.Name.Replace('\\', '/'));

            HashSet<string> currentChunks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
                currentChunks.Add(entry.Id.ToString());

            CleanDirectory(Path.Combine(basePath, "ebx"), currentEbx, stripExtension: true);
            CleanDirectory(Path.Combine(basePath, "res"), currentRes, stripExtension: true);
            CleanDirectory(Path.Combine(basePath, "chunks"), currentChunks, stripExtension: true);
        }

        private static void CleanDirectory(string dir, HashSet<string> validNames, bool stripExtension)
        {
            if (!Directory.Exists(dir))
                return;

            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(dir.Length + 1);
                string name = relativePath;

                if (stripExtension)
                {
                    if (name.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - ".meta.json".Length);
                    else
                    {
                        string ext = Path.GetExtension(name);
                        if (!string.IsNullOrEmpty(ext))
                            name = name.Substring(0, name.Length - ext.Length);
                    }
                }

                name = name.Replace('\\', '/');

                if (!validNames.Contains(name))
                {
                    try { File.Delete(file); } catch { }
                }
            }

            foreach (string subDir in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(subDir).Length == 0)
                        Directory.Delete(subDir);
                }
                catch { }
            }
        }

        #endregion

        #region Helpers

        private static string SanitizePath(string name)
        {
            return name.Replace('/', Path.DirectorySeparatorChar);
        }

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        private static List<string> GetBundleNames(IEnumerable<int> bundleIds)
        {
            var names = new List<string>();
            foreach (int bid in bundleIds)
            {
                BundleEntry be = App.AssetManager.GetBundleEntry(bid);
                if (be != null)
                    names.Add(be.Name);
            }
            return names;
        }

        private static List<LinkedAssetJson> GetLinkedAssets(AssetEntry entry)
        {
            var result = new List<LinkedAssetJson>();
            foreach (AssetEntry linked in entry.LinkedAssets)
            {
                var la = new LinkedAssetJson { Type = linked.AssetType };
                if (linked is ChunkAssetEntry chunkEntry)
                    la.Id = chunkEntry.Id.ToString();
                else
                    la.Name = linked.Name;
                result.Add(la);
            }
            return result;
        }

        #endregion
    }
}
