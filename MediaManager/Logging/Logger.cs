using System;

namespace MediaManager
{
    public class Logger
    {
        private const string LoggingPrefix = "MKN";

        private readonly string _owner;

        public Logger(string owner)
        {
            _owner = owner;
        }

        public static event EventHandler<Log> DidWriteLog;

        public void Debug(string message)
        {
            WriteLog(Log.LogTypesEnum.Debug, message);
        }

        public void Info(string message)
        {
            WriteLog(Log.LogTypesEnum.Info, message);
        }

        public void Warning(string message)
        {
            WriteLog(Log.LogTypesEnum.Warning, message);
        }

        private void WriteLog(Log.LogTypesEnum logType, string message)
        {
            var logText = $"{LoggingPrefix} [{_owner}]: {message}";
            var log = new Log(logType, logText);

            DidWriteLog?.Invoke(null, log);
            Console.WriteLine(logText);
        }
    }
}