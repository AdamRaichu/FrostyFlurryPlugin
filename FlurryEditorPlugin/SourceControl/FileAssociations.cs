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
}
