using NUnit.Framework;

namespace Cwipc.Tests
{
    /// <summary>
    /// QueueThreadSafe. No native cwipc needed: NativeMemoryChunk only allocates memory.
    /// </summary>
    public class QueueTests
    {
        /// <summary>
        /// Close() discards what is still in the queue: it must free the queue's reference to each
        /// item, or the items leak (encoded packets have no finalizer). #84.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void CloseFreesQueuedItems(bool dropWhenFull)
        {
            var queue = new QueueThreadSafe("QueueTests", 4, dropWhenFull);
            var item = new NativeMemoryChunk(16);
            item.AddRef(); // our own reference, so we can see what happens to the queue's
            Assert.IsTrue(queue.Enqueue(item), "Enqueue failed");
            queue.Close();
            Assert.AreEqual(0, queue.Count(), "items left in the queue after Close()");
            Assert.AreEqual(0, item.free(), "the queue still holds a reference after Close()");
        }

        /// <summary>
        /// Close() racing with a consumer in TryDequeue(): the consumer may already have taken a slot when
        /// Close() drains the queue. It must get null, not an exception from an empty queue. Probabilistic:
        /// many rounds of enqueue, consume and close at the same time.
        /// </summary>
        [Test]
        public void CloseWhileConsumerDequeuesIsSafe()
        {
            System.Exception failure = null;
            for (int round = 0; round < 2000 && failure == null; round++)
            {
                var queue = new QueueThreadSafe("QueueTests.race", 4);
                var consumer = new System.Threading.Thread(() =>
                {
                    try
                    {
                        while (!queue.IsClosed())
                        {
                            queue.TryDequeue(1)?.free();
                        }
                        queue.TryDequeue(0)?.free();
                    }
                    catch (System.Exception e) { failure = e; }
                });
                consumer.Start();
                queue.Enqueue(new NativeMemoryChunk(16));
                queue.Close();
                consumer.Join();
            }
            Assert.IsNull(failure, "consumer got an exception: " + failure);
        }

        /// <summary>
        /// After Close(), Enqueue() takes ownership and frees the item at once.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void EnqueueOnClosedQueueFreesItem(bool dropWhenFull)
        {
            var queue = new QueueThreadSafe("QueueTests", 4, dropWhenFull);
            queue.Close();
            var item = new NativeMemoryChunk(16);
            item.AddRef();
            Assert.IsFalse(queue.Enqueue(item), "Enqueue on a closed queue succeeded");
            Assert.AreEqual(0, queue.Count(), "item ended up in a closed queue");
            Assert.AreEqual(0, item.free(), "the queue still holds a reference");
        }
    }
}
