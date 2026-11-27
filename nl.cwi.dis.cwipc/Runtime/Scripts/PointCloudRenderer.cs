using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if !VRT_WITHOUT_STATS
using Statistics = Cwipc.Statistics;
#endif
using Cwipc;

namespace Cwipc
{
    using Timestamp = System.Int64;
    using Timedelta = System.Int64;

    /// <summary>
    /// MonoBehaviour that renders pointclouds.
    /// </summary>
    public class PointCloudRenderer : MonoBehaviour
    {
        // For reasons I don't understand pointclouds need to be mirrored in the X direction.
        // Doing this on the GameObject.transform has the drawback that coordinate systems
        // become mirrored, for example when cropping a pointcloud. Therefore, we mirror here,
        // by adjusting the matrix.
        ComputeBuffer pointBuffer = null;
        int pointCount = 0;
        [Header("Settings")]
        [Tooltip("Source of pointclouds. Can (and must) be empty if set dynamically through script.")]
        public AbstractPointCloudPreparer pointcloudSource;
        public IPointCloudPreparer preparer;
        [Tooltip("Material (to be cloned) to use to render pointclouds")]
        public Material baseMaterial;
        [Tooltip("After how many seconds without data pointcloud becomes ghosted")]
        [SerializeField] protected int timeoutBeforeGhosting = 5; // seconds
        [Tooltip("Multiplication factor for pointSize for this renderer")]
        public float pointSizeFactor = 1f;
        [Tooltip("Maximize point size to this value (if greater than 0)")]
        public float maximumPointSize = 0;
        [Tooltip("Mirror point X axis because they use a right-hand coordinate system (usually true)")]
        [SerializeField] protected bool pcMirrorX = true;
        [Tooltip("Mirror point Z axis because they use a right-hand coordinate system")]
        [SerializeField] protected bool pcMirrorZ = false;
        [Tooltip("Event emitted when the first point cloud is displayed")]
        public UnityEvent started;
        [Tooltip("Event emitted when the last point cloud has been displayed")]
        public UnityEvent finished;
        private bool started_emitted = false;
        private bool finished_emitted = false;
        private bool warned_about_preparer_missing = false;

        [Header("Introspection (for debugging)")]
        [Tooltip("Renderer name (logging and statistics)")]
        [SerializeField] private string _RendererName;
        [Tooltip("Preparer name (logging and statistics)")]
        [SerializeField] private string _PreparerName;
        [Tooltip("Private clone of Material used by this renderer instance")]
        [SerializeField] protected Material material;
        [SerializeField] protected MaterialPropertyBlock block;
        [Tooltip("True if no pointcloud data is being received")]
        [SerializeField] bool dataIsMissing = false;
        [Tooltip("Timestamp of most recent pointcloud (system clock)")]
        [SerializeField] public Timestamp timestampMostRecentReception;
        [Tooltip("Metadata of most recent pointcloud")]
        [SerializeField] public FrameMetadata metadataMostRecentReception;
        [Tooltip("Number of points in most recent pointcloud")]
        [SerializeField] int pointCountMostRecentReception;
        [Tooltip("Number of points in most recent pointcloud")]
        [SerializeField] public float pointSizeMostRecentReception;
        [Tooltip("Renderer temporarily paused by a script")]
        [SerializeField] bool paused = false;
        
        static int instanceCounter = 0;
        int instanceNumber = instanceCounter++;

        public string Name()
        {
            return $"{GetType().Name}#{instanceNumber}";
        }

        public bool isSupported()
        {
            if (baseMaterial != null) return true;
            baseMaterial = Resources.Load<Material>("PointCloudTextured");
            if (baseMaterial == null) {
                Debug.LogError($"{Name()}: no baseMaterial specified and PointCloudTextured (default) not found");
                return false;
            }
            return baseMaterial.shader.isSupported;
        }

        // Start is called before the first frame update
        void Start()
        {
            _RendererName = Name();
            if (!isSupported())
            {
                Debug.LogError($"{Name()}: uses shader that is not supported on this graphics card");
            }
            if (started == null)
            {
                started = new UnityEvent();
            }
            if (finished == null)
            {
                finished = new UnityEvent();
            }
            material = new Material(baseMaterial);
            block = new MaterialPropertyBlock();
#if !VRT_WITHOUT_STATS
            stats = new Stats(Name());
#endif
            pointBuffer = new ComputeBuffer(1, sizeof(float) * 4);
            if (pointcloudSource != null)
            {
                SetPreparer(pointcloudSource);
            }
        }

        public void PausePlayback(bool _paused)
        {
            paused = _paused;
        }

        public void SetPreparer(IPointCloudPreparer _preparer)
        {
            if (_preparer == null)
            {
                Debug.LogError($"Programmer error: {Name()}: attempt to set null preparer");
            }
            if (preparer != null)
            {
                Debug.LogError($"Programmer error: {Name()}: attempt to set second preparer");
            }
            preparer = _preparer;
        }

        private void Update()
        {
            if (preparer == null) {
                if (!warned_about_preparer_missing)
                {
                    Debug.LogWarning($"{Name()}: Update() called but no preparer set");
                    warned_about_preparer_missing = true;
                }
                return;
			}
            _PreparerName = preparer.Name();
            preparer.Synchronize();
        }

        private void LateUpdate()
        {
            if (preparer == null) return;
            if (paused) return;
            bool fresh = preparer.LatchFrame();
            float pointSize = 0;
            System.TimeSpan sinceEpoch = System.DateTime.UtcNow - new System.DateTime(1970, 1, 1);
            Timestamp now = (Timestamp)sinceEpoch.TotalMilliseconds;

            if (fresh)
            {
                if (!started_emitted)
                {
                    started_emitted = true;
                    started.Invoke();
                }
                timestampMostRecentReception = now;
                metadataMostRecentReception = preparer.currentMetadata;
                if (dataIsMissing)
                {
#if CWIPC_WITH_LOGGING
                    Debug.Log($"{Name()}: Data received again, set pointsize=1");
#endif
                    // Was missing previously. Reset pointsize.
                    block.SetFloat("_PointSizeFactor", 1.0f);
                }
                dataIsMissing = false;
                pointCount = preparer.GetComputeBuffer(ref pointBuffer);
                pointCountMostRecentReception = pointCount;
                pointSize = preparer.GetPointSize() * pointSizeFactor;
                pointSizeMostRecentReception = pointSize;
                if (maximumPointSize > 0 && pointSize > maximumPointSize)
                {
                    pointSize = maximumPointSize;
                }
                if (pointSize == 0)
                {
                    Debug.LogWarning($"{Name()}: pointSize == 0");
                }
                if (pointBuffer == null || !pointBuffer.IsValid())
                {
                    Debug.LogError($"{Name()}: Invalid pointBuffer");
                    return;
                }
                block.SetBuffer("_PointBuffer", pointBuffer);
                block.SetFloat("_PointSize", pointSize);
            } 
            else
            {
                if (!finished_emitted && preparer.EndOfData())
                {
                    finished_emitted = true;
                    finished.Invoke();
                }
                if (now > timestampMostRecentReception + (int)(CwipcConfig.Instance.timeoutBeforeGhosting*1000) && !dataIsMissing)
                {
#if CWIPC_WITH_LOGGING
                    Debug.Log($"{Name()}: No pointcloud received for {timeoutBeforeGhosting} seconds, ghosting with pointsize=0.2");
#endif
                    block.SetFloat("_PointSizeFactor", 0.2f);
                    dataIsMissing = true;
                }
            }
            if (pointBuffer == null || !pointBuffer.IsValid())
            {
                Debug.Log($"{Name()}: xxxjack invalid pointBuffer");
                return;
            }
#if xxxjack_silly
            if (pointCount <= 0 || pointSize <= 0)
            {
                Debug.Log($"{Name()}: pointCount={pointCount}, framenum={UnityEngine.Time.frameCount}, pointSize={pointSize}");
            }
#endif
            Matrix4x4 pcMatrix = transform.localToWorldMatrix;
            if (pcMirrorX)
            {
                pcMatrix = pcMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));
            }
            if (pcMirrorZ)
            {
                pcMatrix = pcMatrix * Matrix4x4.Scale(new Vector3(1, 1, -1));
            }
            block.SetMatrix("_Transform", pcMatrix);
            Graphics.DrawProcedural(material, new Bounds(transform.position, Vector3.one * 2), MeshTopology.Points, pointCount, 1, null, block);
#if !VRT_WITHOUT_STATS
            stats.statsUpdate(pointCount, pointSize, preparer.currentTimestamp, preparer.getQueueDuration(), fresh);
#endif
        }

        protected virtual void OnDestroy()
        {
            if (pointBuffer != null) { pointBuffer.Release(); pointBuffer = null; }
            material = null;
#if !VRT_WITHOUT_STATS
            stats?.Final();
#endif
        }


#if !VRT_WITHOUT_STATS
        protected class Stats : Statistics
        {
            public Stats(string name) : base(name) { }

            double statsTotalPointcloudCount = 0;
            double statsTotalDisplayCount = 0;
            double statsTotalPointCount = 0;
            double statsTotalDisplayPointCount = 0;
            double statsTotalPointSize = 0;
            double statsTotalQueueDuration = 0;
            // latency_ms: lowest arrival age (fresh point clouds only). latency_max_ms: highest display age
            // (every display, re-displays included), so it includes the time a point cloud stays on screen (#57).
            int statsArrivalLatencyCount = 0;
            int statsDisplayLatencyCount = 0;
            Timedelta statsMinLatency = 0;
            Timedelta statsMaxLatency = 0;
            int lastFrameNumber = 0;
            Timestamp lastTimestamp = 0;

            public void statsUpdate(int pointCount, float pointSize, Timestamp timestamp, Timedelta queueDuration, bool fresh)
            {
                lock (statsLock)
                {
                    statsTotalDisplayPointCount += pointCount;
                    statsTotalDisplayCount += 1;
                    lastFrameNumber = UnityEngine.Time.frameCount;
                    lastTimestamp = timestamp;
                    // A re-display of a previously received point cloud only counts as a display. The
                    // record is still written, so the display rate shows when nothing new arrives (#89).
                    if (fresh)
                    {
                        statsTotalPointcloudCount += 1;
                        statsTotalPointCount += pointCount;
                        statsTotalPointSize += pointSize;
                        statsTotalQueueDuration += queueDuration;
                    }
                    if (timestamp > 0)
                    {
                        System.TimeSpan sinceEpoch = System.DateTime.UtcNow - new System.DateTime(1970, 1, 1);
                        Timedelta age = (Timestamp)sinceEpoch.TotalMilliseconds - timestamp;
                        if (age > 1000000)
                        {
                            if (fresh) Debug.LogWarning($"{name}: Preposterous latency {age} ms. timestamp={timestamp}.");
                        }
                        else
                        {
                            if (fresh)
                            {
                                if (age < statsMinLatency || statsArrivalLatencyCount == 0) statsMinLatency = age;
                                statsArrivalLatencyCount++;
                            }
                            if (age > statsMaxLatency || statsDisplayLatencyCount == 0) statsMaxLatency = age;
                            statsDisplayLatencyCount++;
                        }
                    }
                    MaybeOutput();
                }
            }

            protected override void OutputAggregate()
            {
                OutputFields(
                    Rate("fps", statsTotalPointcloudCount),
                    statsArrivalLatencyCount > 0 ? Field("latency_ms", statsMinLatency) : null,
                    statsDisplayLatencyCount > 0 ? Field("latency_max_ms", statsMaxLatency) : null,
                    Rate("fps_display", statsTotalDisplayCount),
                    Average("points_per_cloud", statsTotalPointCount, statsTotalPointcloudCount, "F0"),
                    Average("points_per_display", statsTotalDisplayPointCount, statsTotalDisplayCount, "F0"),
                    Average("avg_pointsize", statsTotalPointSize, statsTotalPointcloudCount, "G4"),
                    Average("renderer_queue_ms", statsTotalQueueDuration, statsTotalPointcloudCount, "F0"),
                    Field("framenumber", lastFrameNumber),
                    Field("timestamp", lastTimestamp),
                    Field("n_pointclouds", statsTotalPointcloudCount),
                    Field("n_displays", statsTotalDisplayCount));
                statsTotalPointcloudCount = 0;
                statsTotalDisplayCount = 0;
                statsTotalDisplayPointCount = 0;
                statsTotalPointCount = 0;
                statsTotalPointSize = 0;
                statsTotalQueueDuration = 0;
                statsArrivalLatencyCount = 0;
                statsDisplayLatencyCount = 0;
                statsMinLatency = 0;
                statsMaxLatency = 0;
            }
        }

        protected Stats stats;
#endif
    }
}
