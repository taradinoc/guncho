using Guncho;
using System;

namespace Guncho.WebHost.Services
{
    /// <summary>
    /// Simple console-based logger for development.
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly object _lock = new object();

        public void LogMessage(LogLevel level, string text)
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var levelStr = level switch
                {
                    LogLevel.Spam => "[SPAM]   ",
                    LogLevel.Verbose => "[INFO]   ",
                    LogLevel.Notice => "[NOTICE] ",
                    LogLevel.Warning => "[WARN]   ",
                    LogLevel.Error => "[ERROR]  ",
                    _ => "[UNKNOWN]"
                };

                Console.WriteLine($"{levelStr} {timestamp} - {text}");
            }
        }
    }
}
