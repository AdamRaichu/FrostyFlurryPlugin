using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using HarmonyLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Flurry.Editor.SourceControl
{
    [HarmonyPatch(typeof(EditorOptionsData))]
    [HarmonyPatchCategory("flurry.editor")]
    public class EditorOptionsDataPatch
    {
        [HarmonyPatch("Save")]
        [HarmonyPostfix]
        public static void SetOurValuesOnSave(EditorOptionsData __instance)
        {
            if (__instance.DefaultInstallation) { 
                if (!CheckFileAssociation())
                    CreateFileAssociation();
            }
        }


        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static void CreateFileAssociation()
        {
            string Extension = ".fxproject";
            string KeyName = "frostyprojectfolder";
            string OpenWith = Assembly.GetEntryAssembly().Location;
            string FileDescription = "Frosty Project Folder";
            //string FileIcon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "fbproject.ico");

            try
            {
                RegistryKey BaseKey = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{Extension}");
                BaseKey.SetValue("", KeyName);

                RegistryKey OpenMethod = Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{KeyName}");
                OpenMethod.SetValue("", FileDescription);
                //OpenMethod.CreateSubKey("DefaultIcon").SetValue("", $"\"{FileIcon}\"");

                RegistryKey Shell = OpenMethod.CreateSubKey("shell");
                Shell.CreateSubKey("edit").CreateSubKey("command").SetValue("", $"\"{OpenWith}\" \"%1\"");
                Shell.CreateSubKey("open").CreateSubKey("command").SetValue("", $"\"{OpenWith}\" \"%1\"");
                BaseKey.Close();
                OpenMethod.Close();
                Shell.Close();

                RegistryKey CurrentUser = Registry.CurrentUser.OpenSubKey($"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\{Extension}", true);
                CurrentUser.DeleteSubKey("UserChoice", false);
                CurrentUser.Close();

                // Tell explorer the file association has been changed
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                SystemSounds.Hand.Play();
                App.Logger.LogError($"Unable to Set File Association: {ex.Message}");
            }
        }

        private static bool CheckFileAssociation()
        {
            // Checks the registry for the current association against current frosty installation
            string KeyName = "frostyprojectfolder";
            string OpenWith = Assembly.GetEntryAssembly().Location;

            try
            {
                string openCommand = Registry.CurrentUser.OpenSubKey("Software\\Classes\\" + KeyName).OpenSubKey("shell").OpenSubKey("open").OpenSubKey("command").GetValue("").ToString();
                return openCommand.Contains(OpenWith);
            }
            catch
            {
                return false;
            }
        }
    }

    public class ProjectLoaderStartupActions : StartupAction
    {
        public override Action<ILogger> Action => logger =>
        {
            // debug disable
            return;

            //App.Logger.Log(Environment.GetCommandLineArgs().Join(null, ", "));
            string[] args = Environment.GetCommandLineArgs();
            if (args.Count() < 2)
            {
                // Only one argument (the executable itself), so no file was passed in
                return;
            }

            string openedFile = args[1];
            if (!openedFile.EndsWith(".fxproject", StringComparison.OrdinalIgnoreCase))
            {
                // Not a .fxproject file, ignore.
                return;
            }

            // load fxproject file
            string dir = Path.GetDirectoryName(openedFile);
            //ProjectImporter.ImportDirectory(dir);

            Window mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                SCLog.Error(" Could not find MainWindow");
                return;
            }

            MethodInfo loadProjectMethod = mainWindow.GetType().GetMethod("LoadProject",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (loadProjectMethod == null)
            {
                SCLog.Error(" Could not find LoadProject method");
                return;
            }

            loadProjectMethod.Invoke(mainWindow, new object[] { dir, false });
        };
    }
}
