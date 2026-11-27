using System;
using System.Collections.Generic;
using UnityEngine;
#if !VRT_WITHOUT_STATS
using Statistics = Cwipc.Statistics;
#endif

namespace Cwipc
{
    using Timestamp = System.Int64;
    using Timedelta = System.Int64;
    using EncoderStreamDescription = StreamSupport.EncoderStreamDescription;

    public class AsyncPCEncoder : AbstractPointCloudEncoder
    {
        cwipc.encodergroup encoderGroup;
        cwipc.encoder[] encoderOutputs;
        bool encodersAreBusy = false;
        PCEncoderOutputPusher[] pushers;
        System.IntPtr encoderBuffer;
        QueueThreadSafe inQueue;
        static int instanceCounter = 0;
        int instanceNumber = instanceCounter++;
        Queue<System.DateTime> mostRecentFeedTimes = new Queue<System.DateTime>();
        Queue<Timestamp> mostRecentFeedTimestamps = new Queue<Timestamp>();
        int nParallel = 0;

        EncoderStreamDescription[] outputs;

        public class PCEncoderOutputPusher
        {
            AsyncPCEncoder parent;
            int stream_number;
            cwipc.encoder encoder;
            QueueThreadSafe outQueue;
            NativeMemoryChunk curBuffer = null;
            Timedelta curEncodeDuration;

            public PCEncoderOutputPusher(AsyncPCEncoder _parent, int _stream_number)
            {
                parent = _parent;
                stream_number = _stream_number;
                encoder = parent.encoderOutputs[stream_number];
                outQueue = parent.outputs[stream_number].outQueue;
            }

            public void Close()
            {
                if (outQueue != null && !outQueue.IsClosed()) outQueue.Close();
                outQueue = null;
                encoder = null;
            }

            public bool LockBuffer()
            {
                lock (this)
                {
                    if (curBuffer != null) return true;
                    if (!encoder.available(false)) return false;
                    curBuffer = new NativeMemoryChunk(encoder.get_encoded_size());
                    curBuffer.metadata.timestamp = parent.mostRecentFeedTimestamps.Peek();
                    curEncodeDuration = (Timedelta)(System.DateTime.Now - parent.mostRecentFeedTimes.Peek()).TotalMilliseconds;
                    if (!encoder.copy_data(curBuffer.pointer, curBuffer.length))
                    {
                        Debug.LogError($"Programmer error: PCEncoder#{stream_number}: cwipc_encoder_copy_data returned false");
                    }
                    return true;
                }
            }

            public void PushBuffer()
            {
                lock(this)
                {
                    if (curBuffer == null) return;
                    Timedelta queuedDuration = outQueue.QueuedDuration();
                    // Before Enqueue(): after it the queue or its consumer may have freed the buffer.
                    int nBytes = curBuffer.length;
                    bool dropped = !outQueue.Enqueue(curBuffer);
#if !VRT_WITHOUT_STATS
                    // A failed Enqueue() while stopping is teardown (Stop() closes the queue), not a drop (cwi-dis/VR2Gather#457).
                    parent.stats.statsUpdate(dropped && parent.isRunning, stream_number, nBytes, curEncodeDuration, queuedDuration);
#endif
                    curBuffer = null;
                }
            }
        }

        public AsyncPCEncoder(QueueThreadSafe _inQueue, EncoderStreamDescription[] _outputs) : base()
        {
            nParallel = CwipcConfig.Instance.encoderParallelism;
            if (nParallel > 0) Debug.LogWarning($"{Name()}: As of 2022-11-22 there seem to be problems with encoderParallelism");
            if (_inQueue == null)
            {
                throw new System.Exception($"{Name()}: inQueue is null");
            }
            inQueue = _inQueue;
            outputs = _outputs;
            int nOutputs = outputs.Length;
            encoderOutputs = new cwipc.encoder[nOutputs];
#if !VRT_WITHOUT_STATS
            stats = new Stats(Name());
#endif
            try
            {
                encoderGroup = cwipc.new_encodergroup();
                for (int i = 0; i < nOutputs; i++)
                {
                    var op = outputs[i];
                    cwipc.encoder_params parms = new cwipc.encoder_params
                    {
                        octree_bits = op.octreeBits,
                        do_inter_frame = false,
                        exp_factor = 0,
                        gop_size = 1,
                        jpeg_quality = 75,
                        macroblock_size = 0,
                        tilenumber = op.tileFilter,
                        voxelsize = 0,
                        n_parallel = nParallel
                    };
                    var encoder = encoderGroup.addencoder(parms);
                    encoderOutputs[i] = encoder;

                }
                Start();
            }
            catch (System.Exception e)
            {
                Debug.Log($"{Name()}: Exception during constructor: {e.Message}");
                throw;
            }
        }
      
        protected override void Start()
        {
            int nPushers = encoderOutputs.Length;
            pushers = new PCEncoderOutputPusher[nPushers];
            for (int i = 0; i < nPushers; i++)
            {
                pushers[i] = new PCEncoderOutputPusher(this, i);
            }
            // Last: the worker thread uses the pushers (#82, rule 3).
            base.Start();
        }

        public override void Stop()
        {
            base.Stop();
            // Close our output queues at once, so their consumers wake up (#82, rule 1).
            foreach (var o in outputs)
            {
                if (o.outQueue != null && !o.outQueue.IsClosed()) o.outQueue.Close();
            }
        }

        public override void AsyncOnStop()
        {
            // Signal end-of-data
            encoderGroup.close();
            // Wait for each pusherThread to see this and terminate
            foreach (var t in pushers)
            {
                t.Close();
            }
#if !VRT_WITHOUT_STATS
            // After the pushers have stopped: they call statsUpdate (#89).
            stats.Final();
#endif
            // Clear our encoderGroup to signal the Update thread
            var tmp = encoderGroup;
            encoderGroup = null;
            // Clear the encoderGroup including all of its encoders
            tmp?.free();
            foreach (var eo in encoderOutputs)
            {
                eo.free();
            }
            // xxxjack is encoderBuffer still used? Think not...
            if (encoderBuffer != System.IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(encoderBuffer); 
                encoderBuffer = System.IntPtr.Zero;
            }
            base.AsyncOnStop();
        }

        protected override void AsyncUpdate()
        {
            // If we do multi-threaded encoding we always try to obtain results
            // and we always try to feed.
            if(encodersAreBusy || nParallel > 1)
            {
                // See if encoders are done and we can feed the transmitters
                bool allDone = true;
                foreach(var t in pushers)
                {
                    if (!t.LockBuffer()) allDone = false;
                }
                if (allDone)
                {
                    foreach(var t in pushers)
                    {
                        t.PushBuffer();
                    }
                    encodersAreBusy = false;
                    mostRecentFeedTimes.Dequeue();
                    mostRecentFeedTimestamps.Dequeue();
                }
            }
            if (!encodersAreBusy || nParallel > 1)
            {
                // See if we can start encoding.
                cwipc.pointcloud pc = (cwipc.pointcloud)inQueue.TryDequeue(DequeueTimeoutMs);
                if (pc != null)
                {
                    if (encoderGroup != null)
                    {
                        // Not terminating yet
                        mostRecentFeedTimes.Enqueue(System.DateTime.Now);
                        mostRecentFeedTimestamps.Enqueue(pc.timestamp());
                        encoderGroup.feed(pc);
                        encodersAreBusy = true;
                    }
                    pc.free();
                }
            }
        }


#if !VRT_WITHOUT_STATS
        protected class Stats : Statistics {
            public Stats(string name) : base(name) { }

            double statsTotalPointclouds = 0;
            double statsTotalDropped = 0;
            double statsTotalEncodeDuration = 0;
            double statsTotalQueuedDuration = 0;
            double statsTotalBytes = 0;
            int statsAggregatePackets = 0;

            public void statsUpdate(bool dropped, int stream, int nBytes, Timedelta encodeDuration, Timedelta queuedDuration) {
                lock (statsLock)
                {
                    statsTotalPointclouds++;
                    statsAggregatePackets++;
                    statsTotalEncodeDuration += encodeDuration;
                    statsTotalQueuedDuration += queuedDuration;
                    // Dropped ones included: their bytes appear in no writer record (#96).
                    statsTotalBytes += nBytes;
                    if (dropped)
                    {
                        statsTotalDropped++;
                        OutputDrop(DropReason.Full, stream.ToString());
                    }
                    MaybeOutput();
                }
            }

            protected override void OutputAggregate()
            {
                OutputFields(
                    Rate("fps", statsTotalPointclouds),
                    Rate("fps_dropped", statsTotalDropped),
                    Average("encoder_ms", statsTotalEncodeDuration, statsTotalPointclouds),
                    Average("transmitter_queue_ms", statsTotalQueuedDuration, statsTotalPointclouds, "F0"),
                    Average("bytes_per_packet", statsTotalBytes, statsTotalPointclouds, "F0"),
                    Field("aggregate_packets", statsAggregatePackets),
                    Field("n_pointclouds", statsTotalPointclouds),
                    Field("n_dropped", statsTotalDropped),
                    Field("bytes", statsTotalBytes));
                statsTotalPointclouds = 0;
                statsTotalDropped = 0;
                statsTotalEncodeDuration = 0;
                statsTotalQueuedDuration = 0;
                statsTotalBytes = 0;
            }
        }

        protected Stats stats;
#endif
    }
}