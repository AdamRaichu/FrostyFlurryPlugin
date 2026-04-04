using Frosty.Core;
using Frosty.Core.Controls;
using FrostyEditor;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Flurry.Editor.Patches
{
    public static class ExceptionHelper
    {
        public static string BuildFullExceptionText(Exception e)
        {
            if (e == null) return "";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(e.Message);
            sb.AppendLine();
            sb.AppendLine(e.StackTrace);

            Exception inner = e.InnerException;
            int depth = 1;
            while (inner != null)
            {
                sb.AppendLine();
                sb.AppendLine($"--- Inner Exception {depth} ---");
                sb.AppendLine(inner.Message);
                sb.AppendLine();
                sb.AppendLine(inner.StackTrace);

                inner = inner.InnerException;
                depth++;
            }

            return sb.ToString();
        }
    }

    [HarmonyPatch(typeof(FrostyExceptionBox))]
    [HarmonyPatchCategory("flurry.editor")]
    public class ExceptionBoxPatch
    {
        [HarmonyPatch(nameof(FrostyExceptionBox.OnApplyTemplate))]
        [HarmonyPostfix]
        public static void OnApplyTemplate_Postfix(FrostyExceptionBox __instance)
        {
            __instance.Loaded += (s, e) =>
            {
                __instance.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { AddButtons(__instance); }
                    catch { }
                }), DispatcherPriority.Loaded);
            };
        }

        private static void AddButtons(FrostyExceptionBox window)
        {
            StackPanel buttonPanel = FindChild<StackPanel>(window, sp => sp.FlowDirection == FlowDirection.RightToLeft);
            if (buttonPanel == null) return;

            Button reportButton = new Button
            {
                Content = "Copy Crash Report",
                Width = 130,
                Margin = new Thickness(5, 0, 0, 0)
            };
            reportButton.Click += (s, e) =>
            {
                try
                {
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("=== Exception ===");
                    sb.AppendLine(window.ExceptionText ?? "(no exception text)");
                    sb.AppendLine();

                    string logPath = GetCrashLogPath();
                    if (File.Exists(logPath))
                    {
                        sb.AppendLine("=== Crash Log ===");
                        sb.AppendLine(File.ReadAllText(logPath));
                        sb.AppendLine();
                    }

                    string editorLogPath = Path.Combine(
                        Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? AppDomain.CurrentDomain.BaseDirectory,
                        "log.txt");
                    if (File.Exists(editorLogPath))
                    {
                        sb.AppendLine("=== Editor Log (last 200 lines) ===");
                        var lines = File.ReadAllLines(editorLogPath);
                        int start = Math.Max(0, lines.Length - 200);
                        for (int i = start; i < lines.Length; i++)
                            sb.AppendLine(lines[i]);
                        sb.AppendLine();
                    }

                    sb.AppendLine("=== Binary File Hashes ===");
                    sb.AppendLine(FlurryEditorUtils.GetBinaryFileHashes());

                    Clipboard.SetText(sb.ToString());
                    Frosty.Core.App.Logger.Log("Copied full crash report to clipboard.");
                }
                catch (Exception ex)
                {
                    Frosty.Core.App.Logger.LogWarning("Failed to copy crash report: " + ex.Message);
                }
            };

            Button logButton = new Button
            {
                Content = "Go to Log",
                Width = 80,
                Margin = new Thickness(5, 0, 0, 0)
            };
            logButton.Click += (s, e) =>
            {
                string crashLogPath = GetCrashLogPath();
                if (File.Exists(crashLogPath))
                    Process.Start("explorer.exe", $"/select,\"{crashLogPath}\"");
                else
                {
                    string dir = Path.GetDirectoryName(crashLogPath);
                    if (Directory.Exists(dir))
                        Process.Start("explorer.exe", dir);
                }
            };

            buttonPanel.Children.Add(reportButton);
            buttonPanel.Children.Add(logButton);
        }

        private static T FindChild<T>(DependencyObject parent, Func<T, bool> predicate = null) where T : DependencyObject
        {
            if (parent == null) return null;
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T found && (predicate == null || predicate(found)))
                    return found;
                T result = FindChild(child, predicate);
                if (result != null) return result;
            }
            return null;
        }

        private static string GetCrashLogPath()
        {
            string editorDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
                               ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(editorDir, "crashlog.txt");
        }
    }

    [HarmonyPatch(typeof(FrostyExceptionBox))]
    [HarmonyPatchCategory("flurry.editor")]
    public class ExceptionBoxShowPatch
    {
        [HarmonyPatch(nameof(FrostyExceptionBox.Show))]
        [HarmonyPrefix]
        public static bool Show_Prefix(Exception e, string title, ref MessageBoxResult __result)
        {
            FrostyExceptionBox window = new FrostyExceptionBox();
            window.Title = title;
            window.ExceptionText = ExceptionHelper.BuildFullExceptionText(e);

            __result = (window.ShowDialog() == true) ? MessageBoxResult.OK : MessageBoxResult.Cancel;
            return false;
        }
    }

    [HarmonyPatch(typeof(FrostyEditor.App))]
    [HarmonyPatchCategory("flurry.editor")]
    public class CrashLogPatch
    {
        [HarmonyPatch("App_DispatcherUnhandledException")]
        [HarmonyPrefix]
        public static bool CrashHandler_Prefix(FrostyEditor.App __instance, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                if (Application.Current.MainWindow is MainWindow win)
                {
                    FrostyProject project = win.Project;
                    if (project.IsDirty)
                    {
                        string name = project.DisplayName.Replace(".fbproject", "");
                        DateTime timeStamp = DateTime.Now;
                        project.Filename = "Autosave/" + name + "_"
                            + timeStamp.Day.ToString("D2") + timeStamp.Month.ToString("D2") + timeStamp.Year.ToString("D4") + "_"
                            + timeStamp.Hour.ToString("D2") + timeStamp.Minute.ToString("D2") + timeStamp.Second.ToString("D2")
                            + ".fbproject";
                        project.Save();
                    }
                }
            }
            catch { }

            try
            {
                string fullText = ExceptionHelper.BuildFullExceptionText(e.Exception);
                File.WriteAllText("crashlog.txt", fullText);
            }
            catch { }

            FrostyExceptionBox.Show(e.Exception, "Frosty Editor");
            Environment.Exit(0);

            return false;
        }
    }
}
