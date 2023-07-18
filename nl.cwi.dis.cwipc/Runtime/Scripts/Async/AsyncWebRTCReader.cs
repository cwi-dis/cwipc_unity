using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Cwipc;
#if VRT_WITH_STATS
using Statistics = Cwipc.Statistics;
#endif

namespace Cwipc
{
    using Timestamp = System.Int64;
    using Timedelta = System.Int64;
   
    /// <summary>
    /// Implementation of AsyncReader that connects to a TCP socket on a remote machine and receives
    /// frames from that socket using a very simple protocol:
    /// Each frame starts with a 16-byte header: 4 bytes 4CC, 4 bytes frame data length and 8 bytes timestamp. No
    /// endianness conversion is done. After that we simply transmit the frame data bytes.
    /// <seealso cref="AsyncTCPWriter"/>
    /// </summary>
    public class AsyncWebRTCReader : AsyncReader
    {
        // xxxjack The next two types have to be replaced with whatever identifies our
        // outgoing connection to our peer and whetever identifies the thing that we use to transmit a
        // sequence of frames over
        protected class XxxjackPeerConnection { };
        protected class XxxjackTrackOrStream { };

        protected Uri url;
        protected class ReceiverInfo
        {
            public QueueThreadSafe outQueue;
            public XxxjackTrackOrStream trackOrStream;
            public object tileDescriptor;
            public int tileNumber = -1;
            public uint fourcc;
        }
        protected XxxjackPeerConnection peerConnection;
        protected ReceiverInfo[] receivers;
   
        static int instanceCounter = 0;
        int instanceNumber = instanceCounter++;

        // xxxjack Unsure whether we need a pull-thread for WebRTC. Maybe the package gives us per-stream
        // callbacks, then we don't need a thread.
        // But we should make sure (eventually) that the callbacks don't happen on the main thread, where they
        // will interfere with the update loop.
        protected class WebRTCPullThread
        {
            AsyncWebRTCReader parent;
            bool stopping = false;
            int thread_index;
            ReceiverInfo receiverInfo;
            System.Threading.Thread myThread;
            
            public WebRTCPullThread(AsyncWebRTCReader _parent, int _thread_index, ReceiverInfo _receiverInfo)
            {
                parent = _parent;
                thread_index = _thread_index;
                receiverInfo = _receiverInfo;
                myThread = new System.Threading.Thread(run);
                myThread.Name = Name();
#if VRT_WITH_STATS
                stats = new Stats(Name());
#endif
                Debug.Log($"{Name()}: ready to receive");
            }

            public string Name()
            {
                return $"{parent.Name()}.{thread_index}";
            }

            public void Start()
            {
                myThread.Start();
            }

            public void Stop() {
                stopping = true;
                Debug.Log($"{Name()}: Should stop receiving");
               

            }

            public void Join()
            {
                myThread.Join();
            }

            protected void run()
            {
                try
                {
                    while (!stopping)
                    {
                        //
                        // First check whether we should terminate, and otherwise whether we have nay work to do currently.
                        //
                        if (receiverInfo == null || receiverInfo.outQueue.IsClosed())
                        {
                            return;
                        }
                        Debug.Log($"{Name()}: should receive WebRTC data, delay one second in stead");
                        Thread.Sleep(1000);
                        int dataSize = 0;
                        Timestamp timestamp = 0;
                        byte[] data = new byte[dataSize];

                        NativeMemoryChunk mc = new NativeMemoryChunk(dataSize);
                        mc.metadata.timestamp = timestamp;
                        System.Runtime.InteropServices.Marshal.Copy(data, 0, mc.pointer, dataSize);
                        bool ok = receiverInfo.outQueue.Enqueue(mc);                        
                    }
                }
#pragma warning disable CS0168
                catch (System.Exception e)
                {
                    if (!stopping)
                    {
#if UNITY_EDITOR
                        throw;
#else
                        Debug.Log($"{Name()}: Exception: {e.Message} Stack: {e.StackTrace}");
                        Debug.LogError("Error while receiving visual representation or audio from another participant");
#endif
                    }
                }

            }

#if VRT_WITH_STATS
            protected class Stats : Statistics
            {
                public Stats(string name) : base(name) { }

                System.DateTime statsConnectionStartTime;
                double statsTotalBytes;
                double statsTotalDuration;
                double statsTotalPackets;
                int statsAggregatePackets;
                double statsDroppedPackets;
                
                public void statsUpdate(int nBytes, Timedelta duration, bool dropped)
                {
                    statsTotalBytes += nBytes;
                    statsTotalDuration += duration;
                    statsTotalPackets++;
                    statsAggregatePackets++;
                    if (dropped) statsDroppedPackets++;
                    if (ShouldOutput())
                    {
                        Output($"fps={statsTotalPackets / Interval():F2}, fps_dropped={statsDroppedPackets / Interval():F2}, receive_ms={(int)(statsTotalDuration/statsTotalPackets)}, receive_bandwidth={(int)(statsTotalBytes/Interval())}, bytes_per_packet={(int)(statsTotalBytes / statsTotalPackets)}, aggregate_packets={statsAggregatePackets}");
                        Clear();
                        statsTotalBytes = 0;
                        statsTotalDuration = 0;
                        statsTotalPackets = 0;
                        statsDroppedPackets = 0;
                    }
                }
            }

            protected Stats stats;
#endif
        }
        WebRTCPullThread[] threads;

        SyncConfig.ClockCorrespondence clockCorrespondence; // Allows mapping stream clock to wall clock

        /// <summary>
        /// Constructor that can be used by subclasses to create a multi-tile receiver.
        /// The URL should be of the form tcp://host:port. Subsequent streams will increment the port number.
        /// The subclass could initialize the receivers array and call Start().
        /// </summary>
        /// <param name="_url">The base URL for the streams</param>
        protected AsyncWebRTCReader(string _url) : base()
        {
            NoUpdateCallsNeeded();
            lock (this)
            {
                joinTimeout = 20000;

                if (_url == "" || _url == null)
                {
                    throw new System.Exception($"{Name()}: TCP transport requires tcp://host:port/ URL, but no URL specified");
                }
                url = new Uri(_url);
                

            }
        }

        /// <summary>
        /// Create a WebRTC reader (client).
        /// The URL should be of the form tcp://host:post.
        /// </summary>
        /// <param name="_url">The server to connect to</param>
        /// <param name="fourcc">The 4CC of the frames expected on the stream</param>
        /// <param name="outQueue">The queue into which received frames will be deposited</param>
        public AsyncWebRTCReader(string _url, string fourcc, QueueThreadSafe outQueue) : this(_url)
        {
            lock (this)
            {
                receivers = new ReceiverInfo[]
                {
                    new ReceiverInfo()
                    {
                        outQueue = outQueue,
                        trackOrStream = new XxxjackTrackOrStream(),
                        fourcc = StreamSupport.VRT_4CC(fourcc[0], fourcc[1], fourcc[2], fourcc[3])
                    },
                };
                Start();

            }
        }

        public override void Stop()
        {
            base.Stop();
            _closeQueues();
        }

        protected override void Start()
        {
            base.Start();
            InitThreads();
        }

        public override void AsyncOnStop()
        {
            if (debugThreading) Debug.Log($"{Name()}: Stopping threads");
            foreach(var t in threads)
            {
                t.Stop();
                t.Join();
            }
            base.AsyncOnStop();
        }

        protected override void AsyncUpdate()
        {
        }

        protected void InitThreads()
        {
            lock (this)
            {
                int threadCount = receivers.Length;
                threads = new WebRTCPullThread[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    threads[i] = new WebRTCPullThread(this, i, receivers[i]);
                    string msg = $"pull_thread={threads[i].Name()}";
                    if (receivers[i].tileNumber >= 0)
                    {
                        msg += $", tile={receivers[i].tileNumber}";
                    }
#if VRT_WITH_STATS
                    Statistics.Output(base.Name(), msg);
#endif
                }
                foreach (var t in threads)
                {
                    t.Start();
                }
            }
        }
        private void _closeQueues()
        {
            foreach (var r in receivers)
            {
                var oq = r.outQueue;
                if (!oq.IsClosed()) oq.Close();
            }
        }
    }
}

