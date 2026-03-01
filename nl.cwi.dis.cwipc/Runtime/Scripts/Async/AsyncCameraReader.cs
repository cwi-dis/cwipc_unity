using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwipc
{
    public class AsyncCameraReader : AsyncPointCloudReader
    {

        public AsyncCameraReader(string _configFilename, float _voxelSize, float _frameRate, QueueThreadSafe _outQueue, QueueThreadSafe _out2Queue = null) : base(_outQueue, _out2Queue)
        {
            voxelSize = _voxelSize;
            if (_frameRate > 0)
            {
                frameInterval = System.TimeSpan.FromSeconds(1 / _frameRate);
            }
            reader = cwipc.capturer(_configFilename);
            if (reader == null)
            {
                throw new System.Exception($"{Name()}: cwipc_capturer could not be created, but no CwipcException raised?"); // Should not happen, should throw exception
            }
            
            Start();
            Debug.Log($"{Name()}: Started.");

        }
    }
}
