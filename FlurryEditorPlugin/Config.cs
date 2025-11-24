using Frosty.Core;
using Frosty.Core.Controls.Editors;
using FrostySdk.Attributes;
using FrostySdk.IO;

namespace Flurry.Editor
{
    [DisplayName("Flurry Config (Editor)")]
    public class FlurryEditorConfig : OptionsExtension
    {
        [Category("_General")]
        [DisplayName("Harmony Debug Logging")]
        [Description("Outputs a log file to Desktop/harmony.log.txt.")]
        [Editor(typeof(FrostyBooleanEditor))]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool HarmonyDebug { get; set; } = false;

        [Category("Additional Tweaks")]
        [DisplayName("Enable Blueprint Editor Tweaks")]
        [Description("Enable this if using the Graph Editor for blueprints.")]
        [Editor(typeof(FrostyBooleanEditor))]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        //[DependsOn("updateCheck")]
        public bool BlueprintEditorTweaks { get; set; } = false;

        [Category("Additional Tweaks")]
        [DisplayName("Enable Bookmarks Tab Tweaks")]
        [Description("If you're having issues related to the bookmarks menu, disable this.")]
        [Editor(typeof(FrostyBooleanEditor))]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool BookmarksTabTweaks { get; set; } = true;

        public override void Load()
        {
            HarmonyDebug = Config.Get<bool>("Flurry.HarmonyDebug", false);
            BlueprintEditorTweaks = Config.Get<bool>("Flurry.BlueprintEditorTweaks", false);
            BookmarksTabTweaks = Config.Get<bool>("Flurry.BookmarksTabTweaks", true);
        }

        public override void Save()
        {
            Config.Add("Flurry.HarmonyDebug", HarmonyDebug);
            Config.Add("Flurry.BlueprintEditorTweaks", BlueprintEditorTweaks);
            Config.Add("Flurry.BookmarksTabTweaks", BookmarksTabTweaks);
            Config.Save();
        }

        public override bool Validate()
        {
            return true;
        }
    }
}
