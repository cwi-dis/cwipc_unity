using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace Cwipc.Tests
{
    /// <summary>
    /// Point cloud readers (AsyncPointCloudReader subclasses) on their worker threads.
    /// Need cwipc installed, like NativeLibraryTests.
    /// </summary>
    public class AsyncReaderTests
    {
        /// <summary>
        /// The "Networked" source has no native reader of its own (it reads through a TCP reader
        /// and a decoder), which AsyncPointCloudReader.Start() didn't allow for (#83). Nothing
        /// listens on the port, so its TCP reader only logs connection warnings; it must stop
        /// well within AsyncWorker's join timeout (5 s), without errors.
        /// </summary>
        [Test]
        public void NetworkCaptureReaderStartsAndStops()
        {
            var errors = new List<string>();
            Application.LogCallback onLog = (message, stackTrace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception)
                {
                    lock (errors) errors.Add(message);
                }
            };
            var outQueue = new QueueThreadSafe("AsyncReaderTests.out");
            Application.logMessageReceivedThreaded += onLog;
            AsyncNetworkCaptureReader reader = null;
            long stopMs;
            try
            {
                reader = new AsyncNetworkCaptureReader("tcp://127.0.0.1:9", false, outQueue);
                System.Threading.Thread.Sleep(200);
            }
            finally
            {
                var timer = Stopwatch.StartNew();
                reader?.StopAndWait();
                stopMs = timer.ElapsedMilliseconds;
                Application.logMessageReceivedThreaded -= onLog;
            }
            Assert.Less(stopMs, 3000, "StopAndWait() took too long: the worker thread didn't wake up");
            lock (errors)
            {
                Assert.That(errors, Is.Empty, "errors logged:\n" + string.Join("\n", errors));
            }
        }
    }
}
