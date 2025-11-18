using Frosty.Controls;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostyEditor;
using FrostyEditor.Windows;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Flurry.Patches.EditorOnly
{
    [HarmonyPatch(typeof(MainWindow))]
    [HarmonyPatch("InitializeComponent")]
    [HarmonyPatchCategory("flurry.editor")]
    public class MainWindow_EditorUIPatches
    {
        static AccessTools.FieldRef<MainWindow, Grid> mainGridRef =
        AccessTools.FieldRefAccess<MainWindow, Grid>("mainGrid");

        // Patches: Extra export button

        [HarmonyPostfix]
        public static void PostFix(MainWindow __instance)
        {
            ICommand secondExportCommand = new ExportModMenuItemCommand_AlwaysCanExecute();
            Grid mainGrid = mainGridRef(__instance);
            Grid gridRowOne = (Grid)mainGrid.Children[1];
            Border outerBorder = (Border)gridRowOne.Children[0];
            DockPanel upperDockPanel = (DockPanel)outerBorder.Child;
            Border buttonsBorder = (Border)upperDockPanel.Children[0];
            StackPanel buttonsStackPanel = (StackPanel)buttonsBorder.Child;
            //Button newProjectButton = (Button)buttonsStackPanel.Children[0];

            Image exportImage = new Image()
            {
                Source = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Export.png") as ImageSource,
                Width = 16
            };
            Button extraExportButton = new Button()
            {
                ToolTip = "Export to Mod",
                Margin = new System.Windows.Thickness(4, 0, 0, 0),
                Height = 20,
                Width = 20,
                Command = secondExportCommand,
                CommandParameter = __instance,
                Content = exportImage
            };

            buttonsStackPanel.Children.Add(extraExportButton);
        }

        class ExportModMenuItemCommand_AlwaysCanExecute : ICommand
        {
            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                if (!(App.AssetManager != null && App.AssetManager.GetModifiedCount() != 0))
                {
                    FrostyMessageBox.Show("Cannot export mod when no changes have been made.", "Frosty Editor");
                    return;
                }

                MainWindow mainWin = parameter as MainWindow;
                ModSettingsWindow win = new ModSettingsWindow(mainWin.Project);
                win.ShowDialog();

                if (win.DialogResult == true)
                {
                    FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Mod", "*.fbmod (Frosty Mod)|*.fbmod", "Mod");
                    if (sfd.ShowDialog())
                    {
                        string filename = sfd.FileName;

                        // setup ability to cancel the process
                        CancellationTokenSource cancelToken = new CancellationTokenSource();

                        FrostyTaskWindow.Show("Saving Mod", "", (task) =>
                        {
                            try
                            {
                                mainWin.ExportMod(mainWin.Project.GetModSettings(), filename, false, cancelToken.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                // process was cancelled
                                App.Logger.Log("Export Cancelled");

                                if (File.Exists(filename))
                                {
                                    File.Delete(filename);
                                }
                            }
                        }, showCancelButton: true, cancelCallback: (task) => cancelToken.Cancel());
                    }
                }
            }
        }
    }
}
