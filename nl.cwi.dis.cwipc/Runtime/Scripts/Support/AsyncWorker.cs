using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwipc
{
    /// <summary>
    /// Abstract base class for an asynchronous worker.
    /// The intention is an asynchronous worker is somewhat like a MonoBehaviour, in that its AsyncUpdate() method
    /// is regularly called to see if there is work to do, but it is implemented asynchronously using a separate
    /// thread.
    /// Usually an AsyncWorker will communicate with its source and/or sink through QueueThreadSafe objects, so the
    /// various AsyncWorkers are decoupled.
    /// </summary>
    public abstract class AsyncWorker
    {
        /// <summary>
        /// True if the worker is running.
        /// </summary>
        public bool isRunning => _isRunning;
        // Set on the calling thread, read by the worker thread.
        volatile bool _isRunning;
        System.Threading.Thread thread;
        protected int loopInterval = 1; // How many milliseconds to sleep in the runloop
        protected int joinTimeout = 5000; // How many milliseconds StopAndWait() waits for the thread to end.
        protected const bool debugThreading = true;
        /// <summary>
        /// How long a worker (or helper thread) waits for input at a time: loops never block without a
        /// way out, so Stop() takes effect within this time (cwi-dis/cwipc_unity#82, rule 2).
        /// </summary>
        public const int DequeueTimeoutMs = 100;

        public AsyncWorker()
        {
        }

        /// <summary>
        /// Called by subclasses to indicate the subclass is not interested in calls to AsyncUpdate(), presumably
        /// because it is fully implemented using callbacks or something similar.
        /// </summary>
        protected void NoUpdateCallsNeeded()
        {
            loopInterval = 100;
            Debug.Log($"xxxjack {Name()}: NoUpdateCallsNeeded()");
        }

        public virtual string Name()
        {
            return $"{GetType().Name}";
        }

        /// <summary>
        /// Start the runner thread. Called by the subclass constructor as the very last thing (or later),
        /// once everything AsyncUpdate() and AsyncOnStop() use exists. A subclass that overrides Start()
        /// calls base.Start() last. (cwi-dis/cwipc_unity#82, rule 3.)
        /// </summary>
        protected virtual void Start()
        {
            if (debugThreading) Debug.Log($"{Name()}: starting thread");
            _isRunning = true;
            thread = new System.Threading.Thread(new System.Threading.ThreadStart(AsyncRunner));
            thread.Name = Name();
            // A thread that doesn't stop must not keep the application from quitting (#82, rule 5).
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// Signal the thread to stop, and return at once. Subclasses close their output queues here,
        /// so consumers wake up (#82, rule 1), and call base.Stop().
        /// </summary>
        public virtual void Stop()
        {
            if (debugThreading) Debug.Log($"{Name()}: stopping thread");
            _isRunning = false;
        }

        /// <summary>
        /// Stop the thread and wait (at most joinTimeout) for it to clean up and exit.
        /// A thread that doesn't stop in time is logged as an error and left running, as a background
        /// thread: it is not aborted. Thread.Abort can't interrupt a native call, leaves locks and native
        /// objects in an undefined state, and doesn't exist on IL2CPP (#82, rule 5).
        /// </summary>
        public virtual void StopAndWait()
        {
            if (thread == null)
            {
                Debug.LogWarning($"{Name()}: No thread");
                return;
            }
            Stop();
            if (debugThreading) Debug.Log($"{Name()}: joining thread");
            if (!thread.Join(joinTimeout))
            {
                Debug.LogError($"{Name()}: thread did not stop in {joinTimeout} ms; leaving it running");
                return;
            }
            if (debugThreading) Debug.Log($"{Name()}: thread stopped and joined");
        }

        /// <summary>
        /// Called in the worker thread when the thread is about to stop. Should clean up any resources in use.
        /// When overriding in a subclass call this at the end.
        /// </summary>
        public virtual void AsyncOnStop()
        {
            if (debugThreading) Debug.Log($"{Name()}: thread stopped");
        }

        private void AsyncRunner()
        {
            if (debugThreading) Debug.Log($"{Name()}: thread started");
            try
            {
                while (_isRunning)
                {
                    AsyncUpdate();
                    System.Threading.Thread.Sleep(loopInterval);
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"{Name()}: Update(): Exception: {e}\n{e.StackTrace}");
                Debug.LogError("Error encountered for representation of some participant. This participant will probably seem frozen from now on.");
            }
            _isRunning = false;
            // Clean up in every case, also after an exception (#82, rule 6). In the Editor the exception
            // used to be rethrown before this point, so native objects were never freed there; now it is
            // logged (with its stack trace) in the Editor and in players alike.
            if (debugThreading) Debug.Log($"{Name()}: thread preparing to stop");
            try
            {
                AsyncOnStop();
            }
            catch (System.Exception e)
            {
                Debug.Log($"{Name()}: AsyncOnStop(): Exception: {e}\n{e.StackTrace}");
                Debug.LogError($"Error encountered while cleaning up {Name()}");
            }
            if (debugThreading) Debug.Log($"{Name()}: thread stopped");
        }

        /// <summary>
        /// Method that is called periodically. Must be overridden by subclass.
        /// </summary>
        protected abstract void AsyncUpdate();
    }
}
