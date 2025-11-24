using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Attributes;
using Frosty.Core.Bookmarks;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostyEditor;
using FrostyEditor.Windows;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Flurry.Editor.Patches
{
    [HarmonyPatch(typeof(MainWindow))]
    [HarmonyPatchCategory("flurry.editor")]
    public class MainWindow_EditorUIPatches
    {
        private static AccessTools.FieldRef<MainWindow, Grid> mainGridRef = AccessTools.FieldRefAccess<MainWindow, Grid>("mainGrid");
        private static AccessTools.FieldRef<MainWindow, TreeView> BookmarkTreeViewRef = AccessTools.FieldRefAccess<MainWindow, TreeView>("BookmarkTreeView");

        // Patches: Extra export button

        [HarmonyPatch("InitializeComponent")]
        [HarmonyPostfix]
        public static void StartupUIChanges(MainWindow __instance)
        {
            #region Extra export button
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

            #endregion
        }

        [HarmonyPatch("LoadTabExtensions")]
        [HarmonyPostfix]
        public static void BookmarksMenuChanges(MainWindow __instance) {

            FileLog.Log("BookmarksMenuChanges Patch Applied");
            FlurryEditorConfig config = new FlurryEditorConfig();
            config.Load();

            FileLog.Log("Post config load");
            ImageSourceConverter imageSourceConverter = new ImageSourceConverter();

            #region Bookmarks modifications
            if (config.BookmarksTabTweaks)
            {
                FileLog.Log("Applying bookmarks tweaks");
                ContextMenu bookmarksContextMenu = __instance.FindResource("bookmarksContextMenu") as ContextMenu;
                FileLog.Log("Context menu found");

                // Open asset (modify)
                MenuItem openAssetOption = bookmarksContextMenu.Items.GetItemAt(0) as MenuItem;
                (openAssetOption.Icon as Image).Opacity = 0.5;

                FileLog.Log("Open asset modified");

                // Find in explorer (modify)
                MenuItem findInExplorerOption = bookmarksContextMenu.Items.GetItemAt(1) as MenuItem;
                Image findInExplorerImage = new Image()
                {
                    Source = imageSourceConverter.ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Open.png") as ImageSource,
                    Opacity = 0.5
                };
                RenderOptions.SetBitmapScalingMode(findInExplorerImage, BitmapScalingMode.Fant);
                findInExplorerOption.Icon = findInExplorerImage;

                FileLog.Log("Find in explorer modified");


                TreeView BookmarkTreeView = BookmarkTreeViewRef(__instance);

                // Open in Blueprint Editor (add)
                if (config.BlueprintEditorTweaks)
                {
                    MenuItem openInBlueprintEditorOption = new MenuItem()
                    {
                        Header = "Open in Blueprint Editor",
                        Icon = new Image()
                        {
                            Source = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Assets/BlueprintFileType.png") as ImageSource,
                            Opacity = 0.5
                        }
                    };
                    RenderOptions.SetBitmapScalingMode(openInBlueprintEditorOption.Icon as Image, BitmapScalingMode.Fant);
                    openInBlueprintEditorOption.Click += (sender, e) =>
                    {
                        if (BookmarkTreeView.SelectedItem == null)
                            return;
                        BookmarkItem target = BookmarkTreeView.SelectedItem as BookmarkItem;
                        if (target.Target is AssetBookmarkTarget assetTarget)
                        {
                            if (assetTarget.Asset is EbxAssetEntry entry)
                            {
                                if (entry != null)
                                {
                                    FlurryEditorUtils.OpenInBlueprintEditor(entry);
                                }
                            }
                        }
                    };

                    bookmarksContextMenu.Items.Add(openInBlueprintEditorOption);
                }

                // Copy file path (add)
                MenuItem copyFilePathOption = new MenuItem()
                {
                    Icon = new Image() { 
                        Source = imageSourceConverter.ConvertFromString("pack://application:,,,/FrostyCore;component/Images/Copy.png") as ImageSource,
                        Opacity = 0.5
                    },
                    Header = "Copy file path"
                };
                copyFilePathOption.Click += (sender, e) => {
                    if (BookmarkTreeView.SelectedItem == null)
                        return;
                    BookmarkItem target = BookmarkTreeView.SelectedItem as BookmarkItem;
                    if (target.Target is AssetBookmarkTarget assetTarget)
                    {
                        if (assetTarget.Asset is EbxAssetEntry entry)
                        {
                            if (entry != null)
                            {
                                Clipboard.SetText(entry.Name);
                            }
                        }
                    }
                };
                bookmarksContextMenu.Items.Add(copyFilePathOption);
            }
            #endregion
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
                if (!(Frosty.Core.App.AssetManager != null && Frosty.Core.App.AssetManager.GetModifiedCount() != 0))
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
                                Frosty.Core.App.Logger.Log("Export Cancelled");

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
