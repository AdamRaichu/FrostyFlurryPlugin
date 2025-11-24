using Frosty.Core;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Flurry.Editor
{
    public class FlurryEditorUtils
    {
        public static void OpenInBlueprintEditor(EbxAssetEntry entry)
        {
            //App.Logger.Log("Unimplmemented: FlurryEditorUtils.OpenInBlueprintEditor");
            try
            {
                Type extensionManager = System.Type.GetType("BlueprintEditorPlugin.ExtensionsManager, BlueprintEditorPlugin");
                if (extensionManager == null) { App.Logger.LogWarning("BlueprintEditorPlugin.ExtensionsManager type not found"); return; }

                dynamic iEbxGraphEditor = Traverse.Create(extensionManager).Method("GetValidGraphEditor", new Type[] { typeof(EbxAssetEntry) }).GetValue(entry);
                if (iEbxGraphEditor == null) { App.Logger.LogWarning("No valid graph editor exists for this file"); return; }


                Type editorOptionsType = System.Type.GetType("BlueprintEditorPlugin.Options.EditorOptions, BlueprintEditorPlugin");
                Traverse editorOptionsTraverse = Traverse.Create(editorOptionsType);

                Type blueprintEditorType = System.Type.GetType("BlueprintEditorPlugin.BlueprintEditor, BlueprintEditorPlugin");

                FileLog.Log("Opening Blueprint Editor for " + entry.Filename);

                dynamic editor;
                if ((bool)editorOptionsTraverse.Property("LoadBeforeOpen").GetValue())
                {
                    FileLog.Log("Using parameterless constructor and LoadBlueprint");
                    editor = Activator.CreateInstance(blueprintEditorType);
                    FileLog.Log("Calling LoadBlueprint");
                    editor.LoadBlueprint(entry, iEbxGraphEditor);
                    FileLog.Log("LoadBlueprint call complete");
                }
                else
                {
                    //editor = new BlueprintEditor(App.SelectedAsset, iEbxGraphEditor);
                    FileLog.Log("Using constructor with parameters (EbxAssetEntry, IEbxGraphEditor)");
                    editor = Activator.CreateInstance(blueprintEditorType, new object[] { entry, iEbxGraphEditor });
                }

                App.EditorWindow.OpenEditor($"{entry.Filename} (Ebx Graph)", editor);
            }
            catch (Exception ex)
            {
                App.Logger.LogError("Error in FlurryEditorUtils.OpenInBlueprintEditor: " + ex.ToString());
                App.Logger.LogError("Please report this bug to the Flurry GitHub issues page.");
                App.Logger.LogError("https://github.com/AdamRaichu/FrostyFlurryPlugin/issues");
            }
        }
    }
}
