using Frosty.Controls;
using Frosty.Core.Mod;
using FrostyModManager;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Flurry.Manager.Patches
{
    [HarmonyPatch(typeof(MainWindow))]
    [HarmonyPatchCategory("flurry.manager")]
    public class MainWindow_ManagerUIPatches
    {
        private static AccessTools.FieldRef<MainWindow, Button> removeButtonRef = AccessTools.FieldRefAccess<MainWindow, Button>("removeButton");
        private static AccessTools.FieldRef<MainWindow, ListBox> appliedModsListRef = AccessTools.FieldRefAccess<MainWindow, ListBox>("appliedModsList");
        public static AccessTools.FieldRef<MainWindow, FrostyPack> selectedPackRef = AccessTools.FieldRefAccess<MainWindow, FrostyPack>("selectedPack");
        private static AccessTools.FieldRef<MainWindow, FrostyWatermarkTextBox> availableModsFilterTextBoxRef = AccessTools.FieldRefAccess<MainWindow, FrostyWatermarkTextBox>("availableModsFilterTextBox");
        private static AccessTools.FieldRef<MainWindow, ListView> availableModsListRef = AccessTools.FieldRefAccess<MainWindow, ListView>("availableModsList");
        private static AccessTools.FieldRef<MainWindow, Button> installModButtonRef = AccessTools.FieldRefAccess<MainWindow, Button>("installModButton");

        public static Button invertSelectionButton = new Button()
        {
            Content = new Image()
            {
                Source = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlurryManagerPlugin;component/Images/InvertSelect.png") as ImageSource,
                Margin = new System.Windows.Thickness(0, 0, 2, 0),
            },
            IsEnabled = false,
            ToolTip = "Toggle enabled for all selected mods\nShift click to disable all selected\nCtrl Shift click to enable all selected",
        };

        private static DockPanel searchPanelContainer = new DockPanel()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Width = Double.NaN,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        private static ToggleButton appliedModsFilterButton = new ToggleButton()
        {
            ToolTip = "Filter applied mods",
            Content = new Image()
            {
                Source = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlurryManagerPlugin;component/Images/CircleCheck.png") as ImageSource,
                Height = 16,
                Width = 16,
                Margin = new Thickness(0, 0, 2, 0),
            }
        };
        private static ToggleButton notAppliedModsFilterButton = new ToggleButton()
        {
            ToolTip = "Filter not applied mods",
            Content = new Image()
            {
                Source = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlurryManagerPlugin;component/Images/CircleX.png") as ImageSource,
                Height = 16,
                Width = 16,
                Margin = new Thickness(0, 0, 2, 0),
            }
        };
        private static Grid availableModsTabGrid;

        #region Helper Methods

        /// <summary>
        /// Finds all children of a specified type in the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of the elements to find.</typeparam>
        /// <param name="depObj">The parent dependency object to start the search from.</param>
        /// <returns>An enumerable collection of matching elements.</returns>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T tChild)
                {
                    yield return tChild;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        public static bool HasProperty(object obj, string propertyName)
        {
            if (obj == null)
            {
                return false;
            }

            Type type = obj.GetType();
            // Use GetProperty to search for a public instance property
            PropertyInfo propInfo = type.GetProperty(propertyName);

            // If propInfo is not null, the property exists
            return propInfo != null;
        }

        private static void PrintVisualTree(int depth, DependencyObject obj)
        {
            FileLog.Debug(new string(' ', depth * 2) + obj.GetType().Name + ":" + (HasProperty(obj, "Name") ? ((obj as dynamic).Name) : "_"));
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                PrintVisualTree(depth + 1, VisualTreeHelper.GetChild(obj, i));
            }
        }

        #endregion

        #region Applied Mods Filter Refresh
        [HarmonyPatch("removeButton_Click")]
        [HarmonyPostfix]
        public static void RefreshOnRemoveButtonClick(MainWindow __instance) {
            RefreshFilter(__instance);
        }
        [HarmonyPatch("addModButton_Click")]
        [HarmonyPostfix]
        public static void RefreshOnAddButtonClick(MainWindow __instance)
        {
            RefreshFilter(__instance);
        }
        [HarmonyPatch("uninstallModButton_Click")]
        [HarmonyPostfix]
        public static void RefreshOnUninstallButtonClick(MainWindow __instance)
        {
            RefreshFilter(__instance);
        }
        [HarmonyPatch("availableModsList_MouseDoubleClick")]
        [HarmonyPostfix]
        public static void RefreshOnApplyViaDoubleClick(MainWindow __instance)
        {
            RefreshFilter(__instance);
        }
        [HarmonyPatch("collectionModsList_MouseDoubleClick")]
        [HarmonyPostfix]
        public static void RefreshOnApplyViaDoubleClick_Collection(MainWindow __instance)
        {
            RefreshFilter(__instance);
        }
        [HarmonyPatch("InstallMods")]
        [HarmonyPostfix]
        public static void RefreshOnInstallMods(MainWindow __instance)
        {
            RefreshFilter(__instance);
        }
        #endregion

        [HarmonyPatch("LoadMenuExtensions")]
        [HarmonyPostfix]
        public static void AddUIElements(MainWindow __instance) {
            #region Invert Button
            // Invert button
            FileLog.Debug("Within PostFix");
            App.Logger.Log("Adding Invert Selection Button to Mod Manager UI");
            Button removeButton = removeButtonRef(__instance);
            StackPanel parentPanel = (StackPanel)removeButton.Parent;
            
            invertSelectionButton.Click += (s, e) =>
            {
                ListBox appliedModsList = appliedModsListRef(__instance);
                foreach (FrostyAppliedMod mod in appliedModsList.SelectedItems)
                {
                    bool targetValue = !mod.IsEnabled;
                    if (Keyboard.IsKeyDown(Key.LeftShift))
                    {
                        if (Keyboard.IsKeyDown(Key.LeftCtrl))
                        {
                            // ctrl + shift = enable all
                            targetValue = true;
                        }
                        else
                        {
                            // just shift = disable all
                            targetValue = false;
                        }
                    }
                    mod.IsEnabled = targetValue;
                }
                appliedModsList.Items.Refresh();
                selectedPackRef(__instance).Refresh();
            };
            parentPanel.Children.Add(invertSelectionButton);
            // End of Invert button
            #endregion

            #region Applied Mods Filter
            // Applied mods filter stuff
            __instance.Width = 1150;

            FileLog.Debug("Pre applied mods changes");
            ListView availableModsList = availableModsListRef(__instance);
            availableModsTabGrid = availableModsList.Parent as Grid;
            FrostyWatermarkTextBox availableModsFilterTextBox = availableModsFilterTextBoxRef(__instance);
            FileLog.Debug("Got references;");
            searchPanelContainer.Children.Add(appliedModsFilterButton);
            FileLog.Debug("Added appliedModsFilterButton");
            searchPanelContainer.Children.Add(notAppliedModsFilterButton);
            FileLog.Debug("Added notAppliedModsFilterButton");

            availableModsTabGrid.Children.Remove(availableModsFilterTextBox);
            FileLog.Debug("Removed original filter textbox");
            searchPanelContainer.Children.Add(availableModsFilterTextBox);
            FileLog.Debug("Added filter textbox to new container");

            availableModsTabGrid.Children.Insert(availableModsTabGrid.Children.IndexOf(availableModsList), searchPanelContainer);
            FileLog.Debug("Inserted new container into grid");
            Grid.SetRow(searchPanelContainer, 1);
            Grid.SetRow(availableModsList, 2);
            availableModsFilterTextBox.ClearValue(Grid.RowProperty);

            appliedModsFilterButton.Click += (o, e) =>
            {
                // NotApplied + Applied both checked makes no sense, same as neither checked.
                if (appliedModsFilterButton.IsChecked.GetValueOrDefault())
                {
                    notAppliedModsFilterButton.IsChecked = false;
                }

                RefreshFilter(__instance);
            };
            notAppliedModsFilterButton.Click += (o, e) => {
                // NotApplied + Applied both checked makes no sense, same as neither checked.
                if (notAppliedModsFilterButton.IsChecked.GetValueOrDefault())
                {
                    appliedModsFilterButton.IsChecked = false;
                }

                RefreshFilter(__instance);
            };

            RefreshFilter(__instance);
            dynamic installModButton = installModButtonRef(__instance);

            
            FrostyDockablePanel somethingSomethingAvailableMods = installModButton.Parent.Parent.Parent.Parent.Parent.Parent.HeaderControl.Parent.Parent;

            IEnumerable<FrostyTabItem> tabs = FindVisualChildren<FrostyTabItem>(somethingSomethingAvailableMods);

            FrostyTabItem availableModsTab = null;
            foreach (FrostyTabItem tab in tabs)
            {
                if (tab.Header.ToString().Contains("Available Mods"))
                {
                    availableModsTab = tab;
                    break;
                }
            }
            if (availableModsTab == null)
            {
                PrintVisualTree(2, somethingSomethingAvailableMods);
                throw new Exception("No available mods tab? Printing visual tree to debug log.");
            }

            //availableModsTab.Header = "Available Mods (" + availableModsList.Items.Count + ")";

            FrameworkElementFactory factory = new FrameworkElementFactory(typeof(Image));
            factory.SetValue(Image.SourceProperty, new ImageSourceConverter().ConvertFromString("pack://application:,,,/FlurryManagerPlugin;component/Images/CircleCheck.png") as ImageSource);
            factory.SetValue(Image.HeightProperty, 16.0d);
            factory.SetValue(Image.WidthProperty, 16.0d);
            factory.SetValue(Image.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(Image.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.SetBinding(Image.VisibilityProperty, new Binding("ModDetails.Title")
            {
                Converter = new ModAppliedConverter(),
                ConverterParameter = __instance,
                Mode = BindingMode.OneWay,
            });
            DataTemplate dt = new DataTemplate();
            dt.VisualTree = factory;
            GridView gridView = availableModsList.View as GridView;
            gridView.Columns.Add(new GridViewColumn() { Header = "Applied" });
            GridViewColumn appliedBindingColumn = gridView.Columns[2];
            appliedBindingColumn.CellTemplate = dt;
            // End of Applied mods filter stuff
            #endregion
        }

        [HarmonyPatch("updateAppliedModButtons")]
        [HarmonyPostfix]
        public static void UpdateInvertButtonState(MainWindow __instance)
        {
            ListBox appliedModsList = appliedModsListRef(__instance);
            if (appliedModsList.SelectedItem == null)
            {
                invertSelectionButton.IsEnabled = false;
            } else
            {
                invertSelectionButton.IsEnabled = true;
            }
        }

        [HarmonyPatch("availableModsFilter_LostFocus")]
        [HarmonyPrefix]
        public static bool RefreshFilter(MainWindow __instance)
        {
            //
            FrostyPack selectedPack = selectedPackRef(__instance);
            TextBox availableModsFilterTextBox = availableModsFilterTextBoxRef(__instance);
            ListView availableModsList = availableModsListRef(__instance);

            Func<IFrostyMod, bool> nameFilter = a =>
            {
                if (availableModsFilterTextBox.Text != "")
                {
                    return (a).ModDetails.Title.ToLower().Contains(availableModsFilterTextBox.Text.ToLower());
                }

                return true;
            };

            Func<IFrostyMod, bool> appliedOrNotFilter = a =>
            {
                if (appliedModsFilterButton.IsChecked.GetValueOrDefault())
                {
                    return selectedPack.AppliedMods.Exists(x => x.ModName == ((IFrostyMod)a).ModDetails.Title);
                }
                else if (notAppliedModsFilterButton.IsChecked.GetValueOrDefault())
                {
                    return !selectedPack.AppliedMods.Exists(x => x.ModName == ((IFrostyMod)a).ModDetails.Title);
                }

                return true;
            };

            availableModsList.Items.Filter = new Predicate<object>((object a) => appliedOrNotFilter((IFrostyMod)a) && nameFilter((IFrostyMod)a));
            /*
            if (availableModsFilterTextBox.Text != "" || appliedModsFilterButton.IsChecked.GetValueOrDefault() || notAppliedModsFilterButton.IsChecked.GetValueOrDefault())
            {
                availableModsStatusBar.Text = string.Format("{0} mods pass filter.", availableModsList.Items.Count);
            }
            else
            {
                availableModsStatusBar.Text = string.Format("{0} mods available.", availableModsList.Items.Count);
            } */

            // Skips original method.
            return false;
        }
            
    }

    public class ModAppliedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            String title = (String)value;
            MainWindow mainWindow = (MainWindow)parameter;
            FrostyPack selectedPack = MainWindow_ManagerUIPatches.selectedPackRef(mainWindow);

            if (selectedPack != null)
            {
                if (selectedPack.AppliedMods.Exists(x => x.ModName == title))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
