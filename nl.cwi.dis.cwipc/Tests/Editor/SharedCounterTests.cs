using System.Diagnostics;
using System.Threading;
using NUnit.Framework;

namespace Cwipc.Tests
{
    /// <summary>
    /// SharedCounter, the barrier that keeps the tile readers of prerecorded playback in step
    /// (cwi-dis/cwipc_unity#82). No native cwipc needed.
    /// </summary>
    public class SharedCounterTests
    {
        /// <summary>
        /// A reader that stops (Unsubscribe) must release the others waiting at the barrier.
        /// Before #82 a tile reader that stopped left the others waiting for ever.
        /// </summary>
        [Test]
        public void UnsubscribeReleasesWaiters()
        {
            var counter = new SharedCounter();
            counter.Subscribe();
            counter.Subscribe();
            bool got = false;
            var waiter = new Thread(() => { got = counter.TryWaitAndGet(() => true, out _); });
            waiter.IsBackground = true;
            waiter.Start();
            Thread.Sleep(200);
            counter.Unsubscribe(); // the other reader stops
            Assert.IsTrue(waiter.Join(2000), "the waiting reader was not released");
            Assert.IsTrue(got, "the waiting reader gave up instead of getting a frame index");
        }

        /// <summary>
        /// A reader that is stopped while waiting gives up promptly, and its arrival is taken back:
        /// afterwards the barrier still needs every subscriber (no frame index skipped or shared wrongly).
        /// </summary>
        [Test]
        public void StoppedWaiterGivesUpCleanly()
        {
            var counter = new SharedCounter();
            counter.Subscribe();
            counter.Subscribe();
            var timer = Stopwatch.StartNew();
            Assert.IsFalse(counter.TryWaitAndGet(() => timer.ElapsedMilliseconds < 300, out _), "should have given up");
            Assert.Less(timer.ElapsedMilliseconds, 1000, "giving up took too long");

            // Now both arrive: both must get the same index, and only once both are there.
            long a = -1, b = -1;
            bool aDone = false;
            var ta = new Thread(() => { counter.TryWaitAndGet(() => true, out a); aDone = true; });
            ta.IsBackground = true;
            ta.Start();
            Thread.Sleep(300);
            Assert.IsFalse(aDone, "one reader passed the barrier alone: the earlier arrival wasn't taken back");
            Assert.IsTrue(counter.TryWaitAndGet(() => true, out b));
            Assert.IsTrue(ta.Join(2000));
            Assert.AreEqual(a, b, "the two readers got different frame indices");
        }
    }
}
