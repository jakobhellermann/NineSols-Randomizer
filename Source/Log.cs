using System.Collections.Generic;
using BepInEx.Logging;

namespace Randomizer;

internal static class Log {
    private static ManualLogSource? logSource;
    private static List<(LogLevel, object)> preInitQueue = [];


    internal static void Init(ManualLogSource logSource) {
        Log.logSource = logSource;
        foreach (var (level, data) in preInitQueue) DoLog(level, data);
    }


    internal static void Debug(object data) => DoLog(LogLevel.Warning, data);

    internal static void Info(object data) => DoLog(LogLevel.Warning, data);

    internal static void Warning(object data) => DoLog(LogLevel.Warning, data);

    internal static void Error(object data) => DoLog(LogLevel.Warning, data);

    internal static void Fatal(object data) => DoLog(LogLevel.Warning, data);

    internal static void Message(object data) => DoLog(LogLevel.Warning, data);

    private static void DoLog(LogLevel level, object data) {
        if (logSource != null)
            logSource.Log(level, data);
        else
            preInitQueue.Add((level, data));
    }
}