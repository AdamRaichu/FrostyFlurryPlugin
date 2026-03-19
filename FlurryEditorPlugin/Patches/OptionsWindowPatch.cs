using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using FrostySdk.Attributes;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Flurry.Editor.Patches
{
    [HarmonyPatch(typeof(OptionsWindow))]
    [HarmonyPatchCategory("flurry.editor")]
    public class OptionsWindow_UIPatches
    {
        private static readonly AccessTools.FieldRef<OptionsWindow, FrostyTabControl> optionsTabControlRef =
            AccessTools.FieldRefAccess<OptionsWindow, FrostyTabControl>("optionsTabControl");

        private static readonly AccessTools.FieldRef<OptionsWindow, List<OptionsExtension>> optionDataListRef =
            AccessTools.FieldRefAccess<OptionsWindow, List<OptionsExtension>>("optionDataList");

        [HarmonyPatch("InitializeComponent")]
        [HarmonyPostfix]
        public static void MakeWindowWider(OptionsWindow __instance)
        {
            __instance.Width = 920;
            __instance.MinWidth = 700;
            __instance.MinHeight = 500;
            __instance.ResizeMode = ResizeMode.CanResize;
        }

        [HarmonyPatch("OptionsWindow_Loaded")]
        [HarmonyPostfix]
        public static void ReplaceTabs(OptionsWindow __instance)
        {
            FrostyTabControl tabControl = optionsTabControlRef(__instance);
            List<OptionsExtension> optionDataList = optionDataListRef(__instance);

            // Get the parent grid and the row the tab control sits in
            Grid parentGrid = (Grid)tabControl.Parent;
            int tabControlIndex = parentGrid.Children.IndexOf(tabControl);

            // Hide the original tab control
            tabControl.Visibility = Visibility.Collapsed;

            // Build the replacement layout: sidebar + content
            Grid splitGrid = new Grid();
            Grid.SetRow(splitGrid, 1);

            // Three columns: auto-sized sidebar, draggable splitter, flexible content
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 120, MaxWidth = 350 });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Sidebar ListBox
            ListBox sidebar = new ListBox();
            sidebar.SetResourceReference(Control.BackgroundProperty, "ListBackground");
            sidebar.SetResourceReference(Control.ForegroundProperty, "FontColor");
            sidebar.BorderThickness = new Thickness(0);
            sidebar.Padding = new Thickness(0);
            sidebar.FontSize = 14;
            Grid.SetColumn(sidebar, 0);

            // Populate sidebar from the option data list
            var converter = new OptionsDisplayNameToStringConverter();
            foreach (var optionData in optionDataList)
            {
                string displayName = converter.Convert(optionData, null, null, CultureInfo.CurrentCulture) as string;
                ListBoxItem item = new ListBoxItem
                {
                    Content = displayName,
                    Tag = optionData,
                    Padding = new Thickness(12, 8, 12, 8),
                    Height = double.NaN // override the default 22px to let padding define height
                };
                sidebar.Items.Add(item);
            }

            // Draggable splitter
            GridSplitter splitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#454545")),
                ResizeBehavior = GridResizeBehavior.PreviousAndNext
            };
            Grid.SetColumn(splitter, 1);

            // Content area
            ContentControl contentArea = new ContentControl();
            contentArea.SetResourceReference(Control.BackgroundProperty, "WindowBackground");
            Grid.SetColumn(contentArea, 2);

            // Wire up selection changes
            sidebar.SelectionChanged += (s, e) =>
            {
                if (sidebar.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is OptionsExtension optionData)
                {
                    contentArea.Content = new FrostyPropertyGrid { Object = optionData };
                }
            };

            splitGrid.Children.Add(sidebar);
            splitGrid.Children.Add(splitter);
            splitGrid.Children.Add(contentArea);

            parentGrid.Children.Add(splitGrid);

            // Select the first item
            if (sidebar.Items.Count > 0)
                sidebar.SelectedIndex = 0;
        }
    }
}
