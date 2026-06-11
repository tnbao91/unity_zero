using System;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.Log
{
    public sealed class LogService : ILogService
    {
        public bool IsEnabled { get; set; } = true;

        public void Info(string message)
        {
            if (!IsEnabled) return;
            Debug.Log(message);
        }

        public void Warn(string message)
        {
            if (!IsEnabled) return;
            Debug.LogWarning(message);
        }

        public void Error(string message)
        {
            if (!IsEnabled) return;
            Debug.LogError(message);
        }

        public void Error(Exception exception, string context = null)
        {
            if (!IsEnabled) return;
            if (!string.IsNullOrEmpty(context))
            {
                Debug.LogError(context);
            }
            // Boundary guard: the logging path itself must never throw or forward
            // a null into Debug.LogException. Context (if any) was still logged.
            if (exception != null)
            {
                Debug.LogException(exception);
            }
        }
    }
}
