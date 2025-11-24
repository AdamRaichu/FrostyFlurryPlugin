using Frosty.Core;
using FrostySdk.Interfaces;
using HarmonyLib;
using System;
using System.Reflection;
//using Newtonsoft.Json;

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
}
