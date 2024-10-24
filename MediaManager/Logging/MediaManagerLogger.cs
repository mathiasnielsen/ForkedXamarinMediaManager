using System;
using System.Diagnostics;

namespace MediaManager
{
    public class MediaManagerLogger
    {
        private const string LoggingPrefix = "MKN (v.1.0.5)";

        private readonly string _owner;

        public MediaManagerLogger(string owner)
        {
            _owner = owner;
        }

        public static event EventHandler<Log> DidWriteLog;

        public static bool IsTurnedOn { get; set; } = true;

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

            if (IsTurnedOn)
            {
                Trace.WriteLine(logText);
            }
        }
    }
}
