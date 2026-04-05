using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostyEditor;
using System;
using System.IO;
using System.Linq;
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

    public class ExportToFolderMenuExt : MenuExtension
    {
        public override string TopLevelMenuName => "File";
        public override string SubLevelMenuName => "Folder System";
        public override string MenuItemName => "Export to folder...";
        public override RelayCommand MenuItemClicked => new RelayCommand((object o) =>
        {
            string folderPath = ModernFolderPicker.ShowDialog("Choose save folder");
            if (folderPath == null) return;
            if (!Directory.Exists(folderPath))
            {
                // Hopefully this never happens.
                MessageBox.Show("The selected folder does not exist.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (Directory.EnumerateFileSystemEntries(folderPath).Count() > 0)
            {
                MessageBoxResult result = MessageBox.Show("The selected folder is not empty.\n\nClick Yes to delete all folder contents (except .git folder) and then save. Click No to just save. Click Cancel to cancel.", "Folder Not Empty", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    FlurryEditorUtils.EmptyDirectory(folderPath);
                }
            }
            FrostyTaskWindow.Show("Exporting Project", "Exporting project to folder...", (taskLogger) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectExporter.ExportDirectory(((MainWindow)Frosty.Core.App.EditorWindow).Project, folderPath, false, false);
                });
            });
        });
    }
}
