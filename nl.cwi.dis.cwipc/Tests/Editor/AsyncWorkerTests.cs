using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Cwipc.Tests
{
    /// <summary>
    /// The AsyncWorker lifecycle rules (cwi-dis/cwipc_unity#82). No native cwipc needed.
    /// </summary>
    public class AsyncWorkerTests
    {
        class ThrowingWorker : AsyncWorker
        {
            public volatile bool cleanedUp;
            public ThrowingWorker() { Start(); }
            protected override void AsyncUpdate() { throw new System.Exception("AsyncWorkerTests: deliberate"); }
            public override void AsyncOnStop() { cleanedUp = true; base.AsyncOnStop(); }
        }

        class StuckWorker : AsyncWorker
        {
            public readonly ManualResetEventSlim release = new ManualResetEventSlim(false);
            public StuckWorker() { joinTimeout = 200; Start(); }
            // Ignores Stop(): stands for a thread stuck somewhere it can't be woken.
            protected override void AsyncUpdate() { release.Wait(); }
        }

        /// <summary>
        /// Rule 6: AsyncOnStop() runs also when AsyncUpdate() throws (in the Editor it used to be skipped).
        /// </summary>
        [Test]
        public void AsyncOnStopRunsAfterException()
        {
            LogAssert.ignoreFailingMessages = true; // the worker logs the exception as an error, as it should
            var worker = new ThrowingWorker();
            var timer = Stopwatch.StartNew();
            while (worker.isRunning && timer.ElapsedMilliseconds < 5000) Thread.Sleep(10);
            Thread.Sleep(100);
            Assert.IsFalse(worker.isRunning, "worker still running after its AsyncUpdate threw");
            Assert.IsTrue(worker.cleanedUp, "AsyncOnStop() did not run after the exception");
        }

        /// <summary>
        /// Rule 2: a worker whose input queue stays open (its producer is still running) must still stop
        /// promptly: it may not wait in a blocking Dequeue(). AsyncPCEncoder did (needs native cwipc).
        /// </summary>
        [Test]
        public void EncoderStopsWithOpenInputQueue()
        {
            var inQueue = new QueueThreadSafe("AsyncWorkerTests.encoderIn");
            var outQueue = new QueueThreadSafe("AsyncWorkerTests.encoderOut");
            var encoder = new AsyncPCEncoder(inQueue, new StreamSupport.EncoderStreamDescription[]
            {
                new StreamSupport.EncoderStreamDescription { octreeBits = 9, tileFilter = 0, outQueue = outQueue }
            });
            Thread.Sleep(200); // let the worker reach its input wait
            var timer = Stopwatch.StartNew();
            encoder.StopAndWait();
            long ms = timer.ElapsedMilliseconds;
            inQueue.Close();
            Assert.Less(ms, 1000, "the encoder didn't stop promptly while its input queue was open");
            Assert.IsFalse(encoder.isRunning, "encoder still running");
        }

        /// <summary>
        /// Rule 1: Stop() closes the worker's output queues at once (not only later, in AsyncOnStop()),
        /// so their consumers wake up; it leaves its input queue to the producer.
        /// </summary>
        [Test]
        public void EncoderStopClosesOutputsNotInput()
        {
            var inQueue = new QueueThreadSafe("AsyncWorkerTests.encoderIn");
            var outQueue = new QueueThreadSafe("AsyncWorkerTests.encoderOut");
            var encoder = new AsyncPCEncoder(inQueue, new StreamSupport.EncoderStreamDescription[]
            {
                new StreamSupport.EncoderStreamDescription { octreeBits = 9, tileFilter = 0, outQueue = outQueue }
            });
            try
            {
                encoder.Stop();
                Assert.IsTrue(outQueue.IsClosed(), "output queue not closed by Stop()");
                Assert.IsFalse(inQueue.IsClosed(), "Stop() closed the input queue, which belongs to the producer");
            }
            finally
            {
                encoder.StopAndWait();
                inQueue.Close();
            }
        }

        /// <summary>
        /// Rule 5: StopAndWait() on a thread that doesn't stop returns after joinTimeout, logs an error,
        /// and leaves the (background) thread running instead of aborting it.
        /// </summary>
        [Test]
        public void StopAndWaitDoesNotHangOrAbort()
        {
            LogAssert.ignoreFailingMessages = true; // "thread did not stop" is logged as an error, as it should
            var worker = new StuckWorker();
            Thread.Sleep(50);
            var timer = Stopwatch.StartNew();
            worker.StopAndWait();
            long ms = timer.ElapsedMilliseconds;
            try
            {
                Assert.Less(ms, 2000, "StopAndWait() waited far longer than joinTimeout");
                Assert.GreaterOrEqual(ms, 150, "StopAndWait() returned before joinTimeout");
            }
            finally
            {
                worker.release.Set(); // let the thread end
            }
        }
    }
}
