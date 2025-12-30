using BundleEditPlugin;
using Frosty.Controls;
using Frosty.Core;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flurry.Editor.Patches
{
    [HarmonyPatch(typeof(BundleTabItem))]
    [HarmonyPatchCategory("flurry.editor")]
    public class BundleEditorPluginPatch
    {
        [HarmonyPatch("RefreshBundles")]
        [HarmonyPostfix]
        public static void BundleCounter(BundleTabItem __instance, EbxAssetEntry entry)
        {
            FlurryEditorConfig config = new FlurryEditorConfig();
            config.Load();

            if (!config.BundlesTabTweaks)
            {
                return;
            }

            foreach (var item in App.EditorWindow.MiscTabControl.Items)
            {
                if (item is FrostyTabItem)
                {
                    FrostyTabItem tab = item as FrostyTabItem;
                    if (tab.Header.ToString().Contains("Bundles"))
                    {
                        if (entry == null)
                        {
                            tab.Header = "Bundles (0 + 0)";
                        }
                        tab.Header = $"Bundles ({entry.Bundles.Count} + {entry.AddedBundles.Count})";
                    }
                }
            }
        }
    }
}
