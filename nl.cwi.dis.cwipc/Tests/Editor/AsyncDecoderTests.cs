using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Cwipc.Tests
{
    /// <summary>
    /// AsyncPCDecoder on its worker thread. Needs cwipc installed, like NativeLibraryTests.
    /// </summary>
    public class AsyncDecoderTests
    {
        const int NPoints = 10000;

        static NativeMemoryChunk EncodedSyntheticPointcloud()
        {
            cwipc.source src = cwipc.synthetic(0, NPoints);
            Assert.IsTrue(src.start(), "synthetic source did not start");
            Assert.IsTrue(src.available(true), "synthetic source has no point cloud available");
            cwipc.pointcloud pc = src.get();
            src.stop();
            src.free();
            cwipc.encoder enc = cwipc.new_encoder(new cwipc.encoder_params
            {
                do_inter_frame = false,
                gop_size = 1,
                exp_factor = 0,
                octree_bits = 9,
                jpeg_quality = 85,
                macroblock_size = 16,
                tilenumber = 0,
                voxelsize = 0,
                n_parallel = 1
            });
            try
            {
                enc.feed(pc);
                Assert.IsTrue(enc.available(true), "encoder produced no data");
                int size = enc.get_encoded_size();
                var mc = new NativeMemoryChunk(size);
                Assert.IsTrue(enc.copy_data(mc.pointer, size), "encoder copy_data failed");
                return mc;
            }
            finally
            {
                enc.free();
                pc.free();
            }
        }

        /// <summary>
        /// A decoder whose output queue is closed (its consumer is being torn down, as when a
        /// streaming scene is unloaded) must drop what it decodes without touching the freed
        /// point cloud: #79, "BaseMemoryChunk.pointer: refCount=0" from AsyncPCDecoder.AsyncUpdate.
        /// The worker thread catches exceptions and logs them, so look for error logs from any thread.
        /// </summary>
        [Test]
        public void DecoderWithClosedOutputQueueLogsNoError()
        {
            var errors = new List<string>();
            Application.LogCallback onLog = (message, stackTrace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception)
                {
                    lock (errors) errors.Add(message);
                }
            };
            var inQueue = new QueueThreadSafe("AsyncDecoderTests.in");
            var outQueue = new QueueThreadSafe("AsyncDecoderTests.out");
            Application.logMessageReceivedThreaded += onLog;
            AsyncPCDecoder decoder = null;
            try
            {
                outQueue.Close();
                decoder = new AsyncPCDecoder(inQueue, outQueue);
                Assert.IsTrue(inQueue.Enqueue(EncodedSyntheticPointcloud()), "could not feed the decoder");
                // Wait until the decoder has taken the packet, then give it time to decode and deliver.
                var timer = Stopwatch.StartNew();
                while (inQueue.Count() > 0 && timer.ElapsedMilliseconds < 5000) Thread.Sleep(10);
                Assert.AreEqual(0, inQueue.Count(), "decoder did not take the packet");
                Thread.Sleep(500);
            }
            finally
            {
                decoder?.StopAndWait();
                Application.logMessageReceivedThreaded -= onLog;
            }
            lock (errors)
            {
                Assert.That(errors, Is.Empty, "errors logged:\n" + string.Join("\n", errors));
            }
        }
    }
}
