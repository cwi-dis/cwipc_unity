using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using Cwipc;

namespace Cwipc
{
    using IncomingTileDescription = Cwipc.StreamSupport.IncomingTileDescription;

    public class AsyncWebRTCPCReader : AsyncWebRTCReader
    {
        public AsyncWebRTCPCReader(string _url, string fourcc, IncomingTileDescription[] _tileDescriptors)
        : base(_url)
        {
            lock (this)
            {
                int nTiles = _tileDescriptors.Length;
                receivers = new ReceiverInfo[nTiles];
                for (int ti = 0; ti < nTiles; ti++)
                {
                    ReceiverInfo ri = new ReceiverInfo();
                    ri.tileNumber = ti;
                    ri.trackOrStream = new XxxjackTrackOrStream();
                    IncomingTileDescription td = _tileDescriptors[ti];
                    ri.tileDescriptor = td;
                    ri.outQueue = _tileDescriptors[ti].outQueue;
                    ri.fourcc = StreamSupport.VRT_4CC(fourcc[0], fourcc[1], fourcc[2], fourcc[3]);
                    receivers[ti] = ri;
                }
                Start();
            }
        }

        public void setTileQualityIndex(int tileIndex, int qualityIndex)
        {
            Debug.Log($"{Name()}: setTileQualityIndex({tileIndex},{qualityIndex})");
            int portOffset = qualityIndex * receivers.Length;
            Debug.LogWarning($"{Name()}: setTileQuanlityIndex not yet implemented");
        }
    }
}