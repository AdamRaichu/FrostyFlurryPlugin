using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Flurry.Editor
{
    public class ProjectJson
    {
        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; } = 1;

        [JsonProperty("gameProfile")]
        public string GameProfile { get; set; }

        [JsonProperty("gameVersion")]
        public uint GameVersion { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("modifiedAt")]
        public string ModifiedAt { get; set; }

        [JsonProperty("modSettings")]
        public ModSettingsJson ModSettings { get; set; } = new ModSettingsJson();

        [JsonProperty("addedBundles")]
        public List<AddedBundleJson> AddedBundles { get; set; } = new List<AddedBundleJson>();

        [JsonProperty("addedEbx")]
        public List<AddedEbxJson> AddedEbx { get; set; } = new List<AddedEbxJson>();

        [JsonProperty("addedRes")]
        public List<AddedResJson> AddedRes { get; set; } = new List<AddedResJson>();

        [JsonProperty("addedChunks")]
        public List<AddedChunkJson> AddedChunks { get; set; } = new List<AddedChunkJson>();
    }

    public class ModSettingsJson
    {
        [JsonProperty("title")]
        public string Title { get; set; } = "";

        [JsonProperty("author")]
        public string Author { get; set; } = "";

        [JsonProperty("version")]
        public string Version { get; set; } = "";

        [JsonProperty("category")]
        public string Category { get; set; } = "";

        [JsonProperty("description")]
        public string Description { get; set; } = "";
    }

    public class AddedBundleJson
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("superBundle")]
        public string SuperBundle { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }
    }

    public class AddedEbxJson
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("guid")]
        public string Guid { get; set; }
    }

    public class AddedResJson
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("resRid")]
        public ulong ResRid { get; set; }

        [JsonProperty("resType")]
        public uint ResType { get; set; }

        [JsonProperty("resMeta")]
        public string ResMeta { get; set; }
    }

    public class AddedChunkJson
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("h32")]
        public int H32 { get; set; }
    }

    public class EbxMetaJson
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("isTransientModified")]
        public bool IsTransientModified { get; set; }

        [JsonProperty("userData")]
        public string UserData { get; set; } = "";

        [JsonProperty("isCustomHandler")]
        public bool IsCustomHandler { get; set; }

        [JsonProperty("addedBundles")]
        public List<string> AddedBundles { get; set; } = new List<string>();

        [JsonProperty("linkedAssets")]
        public List<LinkedAssetJson> LinkedAssets { get; set; } = new List<LinkedAssetJson>();
    }

    public class ResMetaJson
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sha1")]
        public string Sha1 { get; set; }

        [JsonProperty("originalSize")]
        public long OriginalSize { get; set; }

        [JsonProperty("resMeta")]
        public string ResMeta { get; set; }

        [JsonProperty("userData")]
        public string UserData { get; set; } = "";

        [JsonProperty("isCustomHandler")]
        public bool IsCustomHandler { get; set; }

        [JsonProperty("addedBundles")]
        public List<string> AddedBundles { get; set; } = new List<string>();

        [JsonProperty("linkedAssets")]
        public List<LinkedAssetJson> LinkedAssets { get; set; } = new List<LinkedAssetJson>();
    }

    public class ChunkMetaJson
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sha1")]
        public string Sha1 { get; set; }

        [JsonProperty("logicalOffset")]
        public uint LogicalOffset { get; set; }

        [JsonProperty("logicalSize")]
        public uint LogicalSize { get; set; }

        [JsonProperty("rangeStart")]
        public uint RangeStart { get; set; }

        [JsonProperty("rangeEnd")]
        public uint RangeEnd { get; set; }

        [JsonProperty("firstMip")]
        public int FirstMip { get; set; } = -1;

        [JsonProperty("h32")]
        public int H32 { get; set; }

        [JsonProperty("addToChunkBundle")]
        public bool AddToChunkBundle { get; set; }

        [JsonProperty("userData")]
        public string UserData { get; set; } = "";

        [JsonProperty("addedBundles")]
        public List<string> AddedBundles { get; set; } = new List<string>();
    }

    public class LinkedAssetJson
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
