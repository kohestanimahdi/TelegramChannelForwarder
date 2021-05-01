using System;
using System.IO;

namespace TelegramForwarder
{
    public static class ApplicationHelpers
    {
        private static object lockObject = new object();
        private static string filePath
        {
            get
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "log.txt");
            }
        }
        public static void CreateIfNotExists()
        {
            if (!File.Exists(filePath))
                File.Create(filePath);
        }

        public static void LogException(Exception exception)
        {
            if (!Program.SaveLog)
                return;

            lock (lockObject)
            {
                var content = $"{DateTime.Now.ToLongTimeString()}{Environment.NewLine}";
                content += $"{exception.Message}{Environment.NewLine}";
                content += $"-------------------------------------------------------{Environment.NewLine}";
                content += $"{exception.StackTrace}{Environment.NewLine}";
                content += $"***********************************************************************************************{Environment.NewLine}";
                File.AppendAllText(filePath, content);
            }

        }
    }
}
