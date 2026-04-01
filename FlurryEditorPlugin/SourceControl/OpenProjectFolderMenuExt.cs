using Frosty.Core;
using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace Flurry.Editor
{
    public class OpenProjectFolderMenuExt : MenuExtension
    {
        public override string TopLevelMenuName => "File";
        public override string SubLevelMenuName => null;
        public override string MenuItemName => "Open Project Folder...";

        public override ImageSource Icon => new ImageSourceConverter().ConvertFromString(
            "pack://application:,,,/FlurryEditorPlugin;component/Images/OpenFolder.png") as ImageSource;

        public override RelayCommand MenuItemClicked => new RelayCommand((object o) =>
        {
            string folderPath = ModernFolderPicker.ShowDialog("Open Exploded Project Folder");
            if (folderPath == null)
                return;

            if (!File.Exists(Path.Combine(folderPath, "project.fxproject")))
            {
                MessageBox.Show(
                    "The selected folder does not contain a project.fxproject file.\n\nSelect a folder that was saved with the Exploded Directory Format.",
                    "Not a Project Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

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

            loadProjectMethod.Invoke(mainWindow, new object[] { folderPath, false });
        });
    }
}
