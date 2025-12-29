using FrostyModManager;
using HarmonyLib;
using System;
using System.Windows.Controls;
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
        private static AccessTools.FieldRef<MainWindow, FrostyPack> selectedPackRef = AccessTools.FieldRefAccess<MainWindow, FrostyPack>("selectedPack");

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

        [HarmonyPatch("LoadMenuExtensions")]
        [HarmonyPostfix]
        public static void AddInvertButton(MainWindow __instance) {
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
    }
}
