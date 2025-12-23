using Flurry.Editor.Editors;
using Frosty.Controls;
using Frosty.Core;
using FrostySdk.Interfaces;
using HarmonyLib;
using System;
using System.Windows.Media;

namespace Flurry.Editor
{
    public class HarmonyPatcherAction : StartupAction
    {
        public override Action<ILogger> Action => logger =>
        {
            FlurryEditorConfig config = new FlurryEditorConfig();
            config.Load();
            Harmony.DEBUG = config.HarmonyDebug;

            FileLog.Log("ManagerType: " + App.PluginManager.ManagerType);
            switch (App.PluginManager.ManagerType)
            {
                case PluginManagerType.Editor:
                    ApplyEditorOnlyPatches(logger);
                    break;
            }
        };

        private void ApplyEditorOnlyPatches(ILogger taskLogger)
        {
            taskLogger.Log("[Flurry] Applying editor patches...");
            var harmony = new Harmony("io.github.adamraichu.frosty.flurry.editor");
            harmony.PatchCategory("flurry.editor");
        }
    }

    public class LoadOrderEditorMenuExt : MenuExtension
    {
        public override string TopLevelMenuName => "Tools";
        public override string SubLevelMenuName => "Kyber Tooling";
        public override string MenuItemName => "Open Launch Overrides Editor";
        //public override ImageSource ParentIcon => Icon;
        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlurryEditorPlugin;component/Images/KyberCog.png") as ImageSource;
        public override RelayCommand MenuItemClicked => new RelayCommand((object o) => {
            FlurryEditorConfig config = new FlurryEditorConfig();
            config.Load();
            if (!config.KyberIntegration)
            {
                FrostyMessageBox.Show("Kyber Integration is not enabled in the Flurry Editor Plugin settings. Please enable it to use the Load Order Editor.", "Kyber Integration Disabled", System.Windows.MessageBoxButton.OK);
                return;
            }
            App.EditorWindow.OpenEditor("[Flurry] Kyber Overrides Editor", new KyberLaunchOverridesEditor());
        });
    }
}
