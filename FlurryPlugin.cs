using Frosty.Core;
using FrostySdk.Interfaces;
using HarmonyLib;
using System;
using System.Reflection;
//using Newtonsoft.Json;

namespace Flurry
{
    public class HarmonyPatcherAction : StartupAction
    {
        public override Action<ILogger> Action => logger =>
        {
            FlurryConfig config = new FlurryConfig();
            config.Load();
            ApplyGenericPatches(logger);
            Harmony.DEBUG = config.HarmonyDebug;

            switch (App.PluginManager.ManagerType)
            {
                case PluginManagerType.Editor:
                    ApplyEditorOnlyPatches(logger);
                    break;

                case PluginManagerType.ModManager: 
                    ApplyManagerOnlyPatches(logger);
                    break;
            }

        };

        private void ApplyGenericPatches(ILogger taskLogger)
        {
            taskLogger.Log("[Flurry] Applying generic patches...");
            var harmony = new Harmony("io.github.adamraichu.frosty.flurry.generic");
            harmony.PatchCategory("flurry.generic");
        }

        private void ApplyEditorOnlyPatches(ILogger taskLogger)
        {
            taskLogger.Log("[Flurry] Applying editor patches...");
            var harmony = new Harmony("io.github.adamraichu.frosty.flurry.editor");
            harmony.PatchCategory("flurry.editor");
        }

        private void ApplyManagerOnlyPatches(ILogger taskLogger)
        {
            taskLogger.Log("[Flurry] Applying manager patches...");
            var harmony = new Harmony("io.github.adamraichu.frosty.flurry.manager");
            harmony.PatchCategory("flurry.manager");
        }
    }
}
