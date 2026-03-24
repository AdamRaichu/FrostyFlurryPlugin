using Frosty.Core;
using HarmonyLib;

namespace Flurry.Editor
{
    /// <summary>
    /// Lightweight logging helper for source control.
    /// Verbose messages go to Harmony's FileLog.Debug (harmony.log.txt on Desktop).
    /// Important messages go to the Frosty editor log.
    /// </summary>
    internal static class SCLog
    {
        /// <summary>Always logged to Frosty's editor log.</summary>
        public static void Log(string message)
        {
            App.Logger.Log("[SourceControl] " + message);
        }

        /// <summary>Logged to Harmony's debug log file (harmony.log.txt).</summary>
        public static void Verbose(string message)
        {
            FileLog.Debug("[SourceControl] " + message);
        }

        /// <summary>Always logged to Frosty's editor log.</summary>
        public static void Warn(string message)
        {
            App.Logger.LogWarning("[SourceControl] " + message);
        }

        /// <summary>Always logged to Frosty's editor log.</summary>
        public static void Error(string message)
        {
            App.Logger.LogError("[SourceControl] " + message);
        }
    }
}
