﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StardewModdingAPI;

namespace StardewEconomyProject.source.utils
{
    internal class LogHelper
    {
        public static IMonitor Monitor;

        public static void Verbose(string str)
        {
            LogHelper.Monitor.VerboseLog(str);
        }

        public static void Trace(string str)
        {
            LogHelper.Monitor.Log(str, LogLevel.Trace);
        }

        public static void Debug(string str)
        {
            LogHelper.Monitor.Log(str, LogLevel.Debug);
        }

        public static void Info(string str)
        {
            LogHelper.Monitor.Log(str, LogLevel.Info);
        }

        public static void Warn(string str)
        {
            LogHelper.Monitor.Log(str, LogLevel.Warn);
        }

        public static void Error(string str)
        {
            LogHelper.Monitor.Log(str, LogLevel.Error);
        }
    }
}