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
    /// Postfix exports the exploded directory after the binary save completes.
    /// </summary>
    [HarmonyPatch(typeof(FrostyProject), "Save")]
    [HarmonyPatchCategory("flurry.editor")]
    public class FrostyProject_Save_Patch
    {
        /// <summary>
        /// Stores the exploded directory path so Postfix can export after the binary save.
        /// </summary>
        [ThreadStatic]
        private static string s_explodedDirPath;

        public static void Prefix(FrostyProject __instance, ref string overrideFilename)
        {
            s_explodedDirPath = null;

            string path = !string.IsNullOrEmpty(overrideFilename) ? overrideFilename : __instance.Filename;

            // If the project filename points to a directory (loaded from exploded format),
            // we need to redirect the binary save to a .fbproject *file* alongside the directory
            // so the original Save code doesn't try to File.Delete a directory (which fails with Access Denied).
            if (Directory.Exists(path))
            {
                // Derive a sibling .fbproject file path:
                //   "E:\path\MyMod" (directory) -> "E:\path\MyMod.fbproject" (file)
                //   "E:\path\MyMod.fbproject" (directory) -> "E:\path\MyMod.fbproject" (file) — need different name
                string parentDir = Path.GetDirectoryName(path);
                string dirName = Path.GetFileName(path);

                string fbprojectFile;
                if (dirName.EndsWith(".fbproject", StringComparison.OrdinalIgnoreCase))
                {
                    // Directory already ends with .fbproject — put the binary file inside the parent
                    // with a "_binary.fbproject" suffix to avoid collision
                    string baseName = dirName.Substring(0, dirName.Length - ".fbproject".Length);
                    fbprojectFile = Path.Combine(parentDir, baseName + "_binary.fbproject");
                }
                else
                {
                    fbprojectFile = path + ".fbproject";
                }

                overrideFilename = fbprojectFile;
                s_explodedDirPath = path;

                SCLog.Verbose("Redirecting binary save to: " + fbprojectFile);
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
                SCLog.Verbose("Exporting exploded directory: " + s_explodedDirPath);
                ProjectExporter.ExportDirectory(__instance, s_explodedDirPath, false);
            }
            catch (Exception ex)
            {
                SCLog.Error("Save postfix failed: " + ex);
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
                SCLog.Verbose("Load intercepted (directory). Path: " + inFilename);

                try
                {
                    var filenameField = AccessTools.Field(typeof(FrostyProject), "filename");
                    if (filenameField == null)
                    {
                        SCLog.Error("Could not find 'filename' field on FrostyProject");
                        __result = false;
                        return false;
                    }
                    filenameField.SetValue(__instance, inFilename);

                    __result = ProjectImporter.ImportDirectory(__instance, inFilename);
                }
                catch (Exception ex)
                {
                    SCLog.Error("Load failed: " + ex);
                    __result = false;
                }
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
                SCLog.Error("Menu reposition failed: " + ex.Message);
            }
        }
    }
}
