namespace MediaManager
{
    public class Log
    {
        public Log(LogTypesEnum logType, string text)
        {
            LogType = logType;
            Text = text;
        }

        public enum LogTypesEnum
        {
            Debug,
            Info,
            Warning,
        }

        public LogTypesEnum LogType { get; }

        public string Text { get; }
    }
}