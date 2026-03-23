using Frosty.Core;
using HarmonyLib;
using System;
using System.IO;
using System.Windows.Controls;

namespace Flurry.Editor.Patches
{
    /// <summary>
    /// Prefix on FrostyProject.Save — when the project was loaded from an exploded directory,
    /// redirect the filename so the original Save writes a .fbproject file alongside it
    /// (the original Save would fail trying to File.Delete a directory path).
    /// </summary>
    [HarmonyPatch(typeof(FrostyProject), "Save")]
    [HarmonyPatchCategory("flurry.editor")]
    public class FrostyProject_Save_Patch
    {
        /// <summary>
        /// Stores the original directory path so Postfix can export the exploded directory.
        /// </summary>
        [ThreadStatic]
        private static string s_explodedDirPath;

        public static void Prefix(FrostyProject __instance, ref string overrideFilename)
        {
            s_explodedDirPath = null;

            string path = !string.IsNullOrEmpty(overrideFilename) ? overrideFilename : __instance.Filename;

            // If the project filename is a directory (loaded from exploded format),
            // redirect to save a .fbproject file alongside it so the original Save doesn't choke.
            if (Directory.Exists(path))
            {
                // "MyMod.fbproject" directory -> "MyMod.fbproject.bin" file for the binary save
                string fbprojectFile = path + ".fbproject";
                overrideFilename = fbprojectFile;
                s_explodedDirPath = path;

                App.Logger.Log("[SourceControl] Redirecting binary save to: " + fbprojectFile);
            }
            else
            {
                // Normal .fbproject file — check if exploded export is enabled
                FlurryEditorConfig config = new FlurryEditorConfig();
                config.Load();

                if (config.ExplodedDirectoryFormat)
                {
                    // Derive exploded directory path alongside the .fbproject file
                    string dirName = Path.GetFileNameWithoutExtension(path);
                    string parentDir = Path.GetDirectoryName(path);
                    s_explodedDirPath = Path.Combine(parentDir, dirName);
                }
            }
        }

        public static void Postfix(FrostyProject __instance)
        {
            if (s_explodedDirPath == null)
                return;

            try
            {
                App.Logger.Log("[SourceControl] Exporting exploded directory: " + s_explodedDirPath);
                ProjectExporter.ExportDirectory(__instance, s_explodedDirPath, false);
            }
            catch (Exception ex)
            {
                App.Logger.LogError("[SourceControl] Save postfix failed: " + ex);
            }
            finally
            {
                s_explodedDirPath = null;
            }
        }
    }

    /// <summary>
    /// Prefix on FrostyProject.Load — if the path is a directory (exploded format),
    /// load it via ProjectImporter instead of the normal binary loader.
    /// </summary>
    [HarmonyPatch(typeof(FrostyProject), "Load")]
    [HarmonyPatchCategory("flurry.editor")]
    public class FrostyProject_Load_Patch
    {
        public static bool Prefix(FrostyProject __instance, ref bool __result, string inFilename)
        {
            if (Directory.Exists(inFilename))
            {
                App.Logger.Log("[SourceControl] Load intercepted (directory). Path: " + inFilename);

                var filenameField = AccessTools.Field(typeof(FrostyProject), "filename");
                filenameField?.SetValue(__instance, inFilename);

                __result = ProjectImporter.ImportDirectory(__instance, inFilename);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Repositions the "Open Project Folder..." menu item right after "Open" in the File menu.
    /// Applied as a manual patch in the startup action.
    /// </summary>
    public static class MenuRepositionPatch
    {
        public static void Postfix(object __instance)
        {
            try
            {
                var menuField = AccessTools.Field(__instance.GetType(), "menu");
                if (menuField == null) return;

                Menu menu = menuField.GetValue(__instance) as Menu;
                if (menu == null) return;

                MenuItem fileMenu = null;
                foreach (MenuItem item in menu.Items)
                {
                    if ("File".Equals(item.Header as string, StringComparison.OrdinalIgnoreCase))
                    {
                        fileMenu = item;
                        break;
                    }
                }
                if (fileMenu == null) return;

                MenuItem openFolderItem = null;
                int openFolderIndex = -1;
                int openIndex = -1;

                for (int i = 0; i < fileMenu.Items.Count; i++)
                {
                    if (fileMenu.Items[i] is MenuItem mi)
                    {
                        string header = mi.Header as string;
                        if (header == "Open Project Folder...")
                        {
                            openFolderItem = mi;
                            openFolderIndex = i;
                        }
                        else if (header == "Open")
                        {
                            openIndex = i;
                        }
                    }
                }

                if (openFolderItem != null && openIndex >= 0 && openFolderIndex != openIndex + 1)
                {
                    fileMenu.Items.RemoveAt(openFolderIndex);
                    int insertAt = openIndex + 1;
                    if (openFolderIndex < openIndex)
                        insertAt--;
                    fileMenu.Items.Insert(insertAt, openFolderItem);
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError("[SourceControl] Menu reposition failed: " + ex.Message);
            }
        }
    }
}
