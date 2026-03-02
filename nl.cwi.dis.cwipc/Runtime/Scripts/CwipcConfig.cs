using System;
using UnityEngine;

namespace Cwipc
{
    /// <summary>
    /// Configuration variables, in a way that allows saving to json.
    /// </summary>
    [Serializable]
    public class CwipcConfig
    {
        /// <summary>
        /// Log level for cwipc. ERROR=1, WARNING=2, TRACE=3, DEBUG=4.
        /// </summary>
        [Tooltip("Log level for cwipc native code. ERROR=1, WARNING=2, TRACE=3, DEBUG=4.")]
        public int logLevel = (int)cwipc.LogLevel.WARNING;
        /// <summary>
        /// Set to non-empty absolute path to override load path for cwipc native libraries
        /// (on most platforms by prepending to PATH or LD_LIBRARY_PATH).
        /// </summary>
        [Tooltip("Set to non-empty absolute path to override load path for cwipc native libraries (on most platforms by prepending to PATH or LD_LIBRARY_PATH).")]
        public string overrideNativeLoadPath = "";
        /// <summary>
        /// Codec for pointclouds. cwi1 is MPEG Anchor codec, cwi0 is uncompressed
        /// </summary>
        [Tooltip("Codec for pointclouds. cwi1 is MPEG Anchor codec, cwi0 is uncompressed")]
        public string Codec = "cwi1";
        /// <summary>
        /// Cell size for pointclouds received or read that how no explicit cellsize.
        /// </summary>
        [Tooltip("Cell size for pointclouds received or read that how no explicit cellsize.")]
        public float defaultCellSize;
        /// <summary>
        /// Multiply cellSize of pointclouds by this factor before rendering.
        /// </summary>
        [Tooltip("Multiply cellSize of pointclouds by this factor before rendering.")]
        public float cellSizeFactor;
        /// <summary>
        /// For debugging colorize each received pointcloud a little bit.
        /// </summary>
        [Tooltip("For debugging colorize each received pointcloud a little bit.")]
        public bool debugColorize;
        /// <summary>
        /// If no pointclouds are received for this many seconds the pointcloud will be ghosted (displayed with a much smaller pointsize)
        /// </summary>
        [Tooltip("If no pointclouds are received for this many seconds the pointcloud will be ghosted (displayed with a much smaller pointsize)")]
        public float timeoutBeforeGhosting = 5.0f;
        /// <summary>
        /// If non-zero sets decoder queue size.
        /// </summary>
        [Tooltip("If non-zero sets decoder queue size.")]
        public int decoderQueueSizeOverride = 0;
        /// <summary>
        /// If non-zero sets preparer queue size.
        /// </summary>
        [Tooltip("If non-zero sets preparer queue size.")]
        public int preparerQueueSizeOverride = 0;
        /// <summary>
        /// If non-zero sets how many threads can be used in the encoder.
        /// </summary>
        [Tooltip("If non-zero sets how many threads can be used in the encoder.")]
        public int encoderParallelism = 0;
        /// <summary>
        /// If non-zero sets how many threads can be used in the decoder. This will decode subsequent pointclouds
        /// in different threads, potentially leading to higher throughput (but not lower latency).
        ///
        /// This will *not* work for decoders with inter-frame coding.
        /// </summary>
        [Tooltip("If non-zero sets how many threads can be used in the decoder.")]
        public int decoderParallelism = 0;

        //
        // Helper code for singleton access, and for loading config from VR2Gather config.json file.
        static CwipcConfig _Instance;
        public static CwipcConfig Instance
        {
            get
            {
                if (_Instance == null) _Instance = new CwipcConfig();
                return _Instance;
            }
        }

        public static void SetInstance(CwipcConfig newInstance)
        {
            _Instance = newInstance;
        }
    };
}

