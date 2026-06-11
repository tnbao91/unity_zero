using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.Services.Log;

namespace Zero.Tests.EditMode
{
    // Phase 6 spec: the logging path itself must never throw — it is the place
    // other error paths report into ("validate inputs at service boundaries").
    [TestFixture]
    public sealed class LogServiceTests
    {
        [Test]
        public void ErrorNullException_DoesNotThrow()
        {
            var log = new LogService();
            Assert.DoesNotThrow(() => log.Error((Exception)null));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ErrorNullException_WithContext_LogsContextOnly()
        {
            var log = new LogService();
            LogAssert.Expect(LogType.Error, "boom context");
            log.Error(null, "boom context");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
