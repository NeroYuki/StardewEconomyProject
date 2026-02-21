using StardewModdingAPI;

namespace StardewEconomyProject
{
    /// <summary>Global logging helper so all classes can log via the mod's monitor.</summary>
    public static class LogHelper
    {
        public static IMonitor Monitor { get; set; }

        public static void Trace(string message) => Monitor?.Log(message, LogLevel.Trace);
        public static void Debug(string message) => Monitor?.Log(message, LogLevel.Debug);
        public static void Info(string message) => Monitor?.Log(message, LogLevel.Info);
        public static void Warn(string message) => Monitor?.Log(message, LogLevel.Warn);
        public static void Error(string message) => Monitor?.Log(message, LogLevel.Error);
    }
}
