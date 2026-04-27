using System;

namespace Zero.Core
{
    public interface ILogService
    {
        bool IsEnabled { get; set; }
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(Exception exception, string context = null);
    }
}
