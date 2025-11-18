using Frosty.Core;
using Frosty.Core.Controls;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flurry.Patches
{
    [HarmonyPatch(typeof(FrostyAssetEditor))]
    [HarmonyPatch(nameof(FrostyAssetEditor.RegisterToolbarItems))]
    [HarmonyPatchCategory("flurry.generic")]
    public class FrostyAssetEditor_ViewInstancesPatch
    {
        [HarmonyPostfix]
        public static void PostFix(FrostyAssetEditor __instance, ref List<ToolbarItem> __result)
        {
            ToolbarItem viewInstances = __result.First();
            Traverse assetEditorTraversal = Traverse.Create(__instance);
            __result = new List<ToolbarItem>
            {
                new ToolbarItem($"View Instances ({__instance.Asset.RootObjects.Count()})", "View class instances", "Images/Database.png", new RelayCommand(blah => assetEditorTraversal.Method("ViewInstances_Click", typeof(object)).GetValue(), state => true))
            };
        }
    }
}
