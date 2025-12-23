using Frosty.Core;
using Frosty.Core.Controls;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace Flurry.Editor.Editors
{
    public class KyberLaunchOverridesEditor : FrostyBaseEditor
    {
        private FrostyPropertyGrid PropertyGrid;
        private bool init;
        private static AccessTools.FieldRef<FrostyPropertyGrid, ObservableCollection<FrostyPropertyGridItemData>> itemsRef = AccessTools.FieldRefAccess<FrostyPropertyGrid, ObservableCollection<FrostyPropertyGridItemData>>("items");

        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlurryEditorPlugin;component/Images/KyberCog.png") as ImageSource;

        static KyberLaunchOverridesEditor()
        {
            FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(KyberLaunchOverridesEditor), new FrameworkPropertyMetadata(typeof(KyberLaunchOverridesEditor)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            PropertyGrid = (base.GetTemplateChild("PART_LoadOrderEditor") as FrostyPropertyGrid);
            base.Loaded += LoadOrderEditor_Loaded;
            (base.GetTemplateChild("RefreshAvailableMods") as Button).Click += (s, e) =>
            {
                var newAvailableMods = new List<CString>();
                string targetPath = Directory.GetCurrentDirectory() + "/Mods/Kyber/";
                if (Directory.Exists(targetPath))
                {
                    Directory.GetFiles(targetPath, "*.fbmod", SearchOption.AllDirectories).ToList().ForEach(filePath =>
                    {
                        newAvailableMods.Add(new CString(filePath.Substring(targetPath.Length)));
                    });
                }

                var gridItems = itemsRef(PropertyGrid);
                foreach (var category in gridItems)
                {
                    if (category.DisplayName == "Helpers")
                    {
                        foreach (var item in category.Children)
                        {
                            if (item.Name == "_AvailableMods")
                            {
                                item.Value = newAvailableMods;
                                return;
                            }
                        }
                    }
                }
            };
            PropertyGrid.OnModified += PropertyGrid_OnModified;
        }

        public void PropertyGrid_OnModified(object sender, ItemModifiedEventArgs e)
        {
            //e.Item.DisplayName
            Save();
        }

        private void Save()
        {
            OverridesContainer container = PropertyGrid.Object as OverridesContainer;
            KyberJsonSettings settings = new KyberJsonSettings();
            settings.LoadOrders = new List<KyberLoadOrderJsonSettings>();
            foreach (var order in container.LoadOrders)
            {
                KyberLoadOrderJsonSettings orderSettings = new KyberLoadOrderJsonSettings();
                orderSettings.Name = order.Name.ToString();
                orderSettings.FbmodNames = new List<string>();
                foreach (var fbmod in order.FbmodNames)
                {
                    orderSettings.FbmodNames.Add(fbmod.ToString());
                }
                settings.LoadOrders.Add(orderSettings);
            }
            settings.LevelOverrides = new List<KyberLevelJsonSettings>();
            foreach (var level in container.LevelOverrides)
            {
                KyberLevelJsonSettings levelSettings = new KyberLevelJsonSettings();
                levelSettings.Name = level.LevelName.ToString();
                levelSettings.LevelId = level.LevelId.ToString();
                levelSettings.ModeIds = new List<string>();
                foreach (var mode in level.ModeIds)
                {
                    levelSettings.ModeIds.Add(mode.ToString());
                }
                settings.LevelOverrides.Add(levelSettings);
            }
            settings.GamemodeOverrides = new List<KyberGamemodeJsonSettings>();
            foreach (var mode in container.GamemodeOverrides)
            {
                KyberGamemodeJsonSettings modeSettings = new KyberGamemodeJsonSettings();
                modeSettings.Name = mode.Name.ToString();
                modeSettings.ModeId = mode.ModeId.ToString();
                modeSettings.PlayerCount = mode.PlayerCount;
                settings.GamemodeOverrides.Add(modeSettings);
            }
            JsonConvert.SerializeObject(settings, Formatting.Indented);
            string jsonName = "Mods/Kyber/Overrides.json";
            File.WriteAllText(jsonName, JsonConvert.SerializeObject(settings, Formatting.None));

            //KyberIntegration.
        }

        public void LoadOrderEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (!init)
            {
                init = true;
                PropertyGrid.SetClass(new OverridesContainer(KyberIntegration.GetKyberJsonSettings()));
            }
        }
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    internal class OverridesContainer
    {
        public OverridesContainer() { }
        public OverridesContainer(KyberJsonSettings settings) {
            LoadOrders = new List<LoadOrderContainter>();
            foreach (var order in settings.LoadOrders)
            {
                LoadOrders.Add(new LoadOrderContainter(order));
            }
            LevelOverrides = new List<LevelOverrideContainter>();
            foreach (var level in settings.LevelOverrides)
            {
                LevelOverrides.Add(new LevelOverrideContainter(level));
            }
            GamemodeOverrides = new List<GamemodeOverrideContainer>();
            foreach (var mode in settings.GamemodeOverrides)
            {
                GamemodeOverrides.Add(new GamemodeOverrideContainer(mode));
            }

            string targetPath = Directory.GetCurrentDirectory() + "/Mods/Kyber/";
            Directory.GetFiles(targetPath, "*.fbmod", SearchOption.AllDirectories).ToList().ForEach(filePath =>
            {
                _AvailableMods.Add(new CString(filePath.Substring(targetPath.Length)));
            });
        }

        [EbxFieldMeta(EbxFieldType.Array,"", EbxFieldType.Inherited)]
        [Category("Config")]
        [DisplayName("Load Orders")]
        public List<LoadOrderContainter> LoadOrders { get; set; }

        [EbxFieldMeta(EbxFieldType.Array, "", EbxFieldType.Inherited)]
        [Category("Config")]
        [DisplayName("Level Overrides")]
        public List<LevelOverrideContainter> LevelOverrides { get; set; }

        [EbxFieldMeta(EbxFieldType.Array, "", EbxFieldType.Inherited)]
        [Category("Config")]
        [DisplayName("Gamemode Overrides")]
        public List<GamemodeOverrideContainer> GamemodeOverrides { get; set; }


        [EbxFieldMeta(EbxFieldType.Array, "", EbxFieldType.CString)]
        [DisplayName("[ReadOnly] Available Mods")]
        [Description("List of all available mods detected in the Mods/Kyber/ folder")]
        [Category("Helpers")]
        public List<CString> _AvailableMods { get; set; } = new List<CString>();
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    internal class LoadOrderContainter
    {
        public LoadOrderContainter() { }
        public LoadOrderContainter(KyberLoadOrderJsonSettings settings)
        {
            Name = settings.Name;
            foreach (string fbmod in settings.FbmodNames)
            {
                FbmodNames.Add(new CString(fbmod));
            }
        }

        [EbxFieldMeta(EbxFieldType.String)]
        [DisplayName("Load Order Name")]
        [Description("Human readable name for the load order (how it appears in the dropdown)")]
        public CString Name { get; set; } = "";
        [EbxFieldMeta(EbxFieldType.Array, "", EbxFieldType.CString)]
        [DisplayName("Mods to Load")]
        [Description("List of file names (relative to <frosty>/Mods/Kyber/) to load in this order. The .fbmod extension is optional. 'KyberMod' is the current project.")]
        public List<CString> FbmodNames { get; set; } = new List<CString>();
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    internal class LevelOverrideContainter
    {
        public LevelOverrideContainter() { }
        public LevelOverrideContainter(KyberLevelJsonSettings settings)
        {
            LevelName = settings.Name;
            LevelId = settings.LevelId;
            foreach (string fbmod in settings.ModeIds)
            {
                ModeIds.Add(new CString(fbmod));
            }
        }

        [EbxFieldMeta(EbxFieldType.String)]
        [DisplayName("Level Name")]
        [Description("Human readable name for the level")]
        public CString LevelName { get; set; } = "";

        [EbxFieldMeta(EbxFieldType.String)]
        [DisplayName("Level Path")]
        [Description("Path to the level to override (e.g. S8_1/Endor_04/Endor_04)")]
        public CString LevelId { get; set; } = "";

        [EbxFieldMeta(EbxFieldType.Array, "", EbxFieldType.CString)]
        [DisplayName("Supported Gamemodes")]
        [Description("List of mode IDs that this level supports (e.g. 'Mode1', 'Mode8')")]
        public List<CString> ModeIds { get; set; } = new List<CString>();
    }

    [EbxClassMeta(EbxFieldType.Struct)]
    internal class GamemodeOverrideContainer
    {
        public GamemodeOverrideContainer() {  }
        public GamemodeOverrideContainer(KyberGamemodeJsonSettings settings)
        {
            Name = settings.Name;
            ModeId = settings.ModeId;
            PlayerCount = settings.PlayerCount;
        }

        [EbxFieldMeta(EbxFieldType.String)]
        [DisplayName("Gamemode Name")]
        [Description("Human readable name for the gamemode. Modes with names starting with 'DO NOT USE' will not show in the UI.")]
        public CString Name { get; set; } = "";
        [EbxFieldMeta(EbxFieldType.String)]
        [DisplayName("Gamemode ID")]
        [Description("ID of the gamemode (e.g. 'Mode1')")]
        public CString ModeId { get; set; } = "";
        [EbxFieldMeta(EbxFieldType.Int32)]
        [DisplayName("Max Players")]
        [Description("Maximum number of players supported in this gamemode. Allowed values: [0,64]")]
        public int PlayerCount { get; set; } = 40;
    }
}
