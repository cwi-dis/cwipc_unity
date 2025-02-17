using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

namespace Cwipc
{
    using Timestamp = System.Int64;

    /// <summary>
    /// Structure with metadata for a frame.
    ///
    /// Currently very much modeled after what Dash implementation in VRTogether needed.
    /// </summary>
    [Serializable]
    public class FrameMetadata
    {
        /// <summary>
        /// Presentation timestamp (milliseconds).
        /// </summary>
        public Timestamp timestamp;
        /// <summary>
        /// Per-frame metadata carried by Dash packets.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] dsi;
        /// <summary>
        /// Length of dsi.
        /// </summary>
        public int dsi_size;
        /// <summary>
        /// For pointclouds read from file: the filename it was read from.
        /// </summary>
        public string filename;
    }

    /// <summary>
    /// Class that tries to keep some reference of how many objects are allocated, to help with debugging
    /// memory leaks.
    /// </summary>
    public class BaseMemoryChunkReferences
    {
        static List<BaseMemoryChunk> types = new List<BaseMemoryChunk>();
        public static void AddReference(BaseMemoryChunk _type)
        {
#if !VRT_WITHOUT_MEMDEBUG
            StackTrace stackTrace = new System.Diagnostics.StackTrace();
            StackFrame frame = null;
            for (int i = 1; i < stackTrace.FrameCount; i++)
            {
                frame = stackTrace.GetFrame(i);
                // We are not interested in BaseMemoryChunk methods/creators or those of its subclasses
                var methodType = frame.GetMethod().DeclaringType;
                if (methodType != typeof(BaseMemoryChunk) && !methodType.IsSubclassOf(typeof(BaseMemoryChunk)))
                    break;
            }
            _type.constructedBy = frame.GetMethod().DeclaringType.ToString() + "." + frame.GetMethod().Name;
            lock (types)
            {
                types.Add(_type);
            }
#endif
        }
        public static void DeleteReference(BaseMemoryChunk _type)
        {
#if !VRT_WITHOUT_MEMDEBUG
            lock (types)
            {
                types.Remove(_type);
            }
#endif
        }

        public static void ShowTotalRefCount()
        {
#if !VRT_WITHOUT_MEMDEBUG
            lock (types)
            {
                if (types.Count == 0) return;
                UnityEngine.Debug.LogError($"BaseMemoryChunkReferences: {types.Count} leaked BaseMemoryChunk objects. See log for details.");
                UnityEngine.Debug.Log($"BaseMemoryChunkReferences: {types.Count} leaked BaseMemoryChunk objects:");
                for (int i = 0; i < types.Count; ++i)
                    UnityEngine.Debug.Log($"BaseMemoryChunkReferences: [{i}] --> {types[i]} size={types[i].length} creator={types[i].constructedBy}");
            }
#endif
        }
    }

    /// <summary>
    /// Abstract class representing a buffer in native memory. Used throughout to forestall copying
    /// data from native buffers to C# arrays only to copy it back to native buffers after a short while.
    ///
    /// These objects are explicitly refcounted, becaue the code cannot know when ownership of the object has passed
    /// from C# to some native dynamic library.
    ///
    /// Usually the buffer will hold a frame (of pointcloud, video or audio data) but this class is also used as the base class
    /// of the various cwipc objects.
    /// </summary>
    public abstract class BaseMemoryChunk
    {

        protected IntPtr _pointer;
        int refCount;
        /// <summary>
        /// Frame metadata, if this is a media frame.
        /// </summary>
        public FrameMetadata metadata;
        public int length { get; protected set; }

#if !VRT_WITHOUT_MEMDEBUG
        public string constructedBy;
#endif
        protected BaseMemoryChunk(IntPtr _pointer)
        {
            if (_pointer == IntPtr.Zero) throw new Exception("BaseMemoryChunk: constructor called with null pointer");
            this._pointer = _pointer;
            this.metadata = new FrameMetadata();
            refCount = 1;
            BaseMemoryChunkReferences.AddReference(this);
        }

        protected BaseMemoryChunk()
        {
            // _pointer will be set later, in the subclass constructor. Not a pattern I'm happy with but difficult to
            refCount = 1;
            BaseMemoryChunkReferences.AddReference(this);
        }

        /// <summary>
        /// Increase reference count on this object.
        /// </summary>
        /// <returns>The object itself</returns>
        public BaseMemoryChunk AddRef()
        {
            lock (this)
            {
                refCount++;
                return this;
            }
        }

        /// <summary>
        /// Get the native pointer for this object.
        /// The caller is responsible for ensuring that the reference count on the object cannot go to
        /// zero while the pointer is in use.
        /// </summary>
        public IntPtr pointer
        {
            get
            {
                lock (this)
                {
                    if (refCount <= 0)
                    {
                        throw new Exception($"BaseMemoryChunk.pointer: refCount={refCount}");
                    }
                    return _pointer;
                }
            }
        }

        /// <summary>
        /// Decrement the reference count on this object, and free it when it reaches zero.
        /// </summary>
        /// <returns>The new reference count.</returns>
        public int free()
        {
            lock (this)
            {
                if (--refCount < 1)
                {
                    if (refCount < 0)
                    {
#if !VRT_WITHOUT_MEMDEBUG
                        UnityEngine.Debug.LogError($"{this.GetType()}.free: refCount={refCount}. Constructor was {constructedBy}");
#else
                        UnityEngine.Debug.LogError($"{this.GetType()}.free: refCount={refCount}. Constructor was unknown, undefine VRT_WITHOUT_MEMDEBUG to get more information.");
#endif
                        return 0;
                    }
                    if (_pointer != IntPtr.Zero)
                    {
                        refCount = 1;   // Temporarily increase refcount so onfree() can use pointer.
                        onfree();
                        refCount = 0;
                        _pointer = IntPtr.Zero;
                        BaseMemoryChunkReferences.DeleteReference(this);
                    }
                }
                return refCount;
            }
        }

        /// <summary>
        /// Method called when the underlying native memory object should be freed. Must be implemented by subclasses.
        /// </summary>
        protected abstract void onfree();
    }
}