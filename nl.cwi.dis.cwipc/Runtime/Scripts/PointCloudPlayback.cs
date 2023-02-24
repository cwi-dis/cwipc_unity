using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Cwipc
{
    /// <summary>
    /// Play a sequence of prerecorded pointclouds (think: volumetric video)
    /// </summary>
    public class PointCloudPlayback : MonoBehaviour
    {
        public PrerecordedPointCloudReader pc_reader;
        public PointCloudRenderer pc_renderer;
        public bool playOnStart = false;
        public bool preload = false;
        public int loopCount = 0;
        public string dirName = "";
        public UnityEvent started;
        public UnityEvent finished;

        public string Name()
        {
            return $"{GetType().Name}";
        }

        private void Awake()
        {
            if (pc_reader == null)
            {
                pc_reader = GetComponentInChildren<PrerecordedPointCloudReader>();
            }
            if (pc_reader == null)
            {
                Debug.LogError($"{Name()}: no pc_reader found");
            }
            if (pc_renderer == null)
            {
                pc_renderer = GetComponentInChildren<PointCloudRenderer>();
            }
            if (pc_renderer == null)
            {
                Debug.LogError($"{Name()}: no pc_renderer found");
            }
            pc_reader.gameObject.SetActive(false);
            pc_renderer.gameObject.SetActive(false);
        }

        // Start is called before the first frame update
        void Start()
        {
            if (playOnStart)
            {
                Play(dirName);
            }
        }

        public void Play(string _dirName)
        {
            Debug.Log($"{Name()}: Play({dirName})");
            dirName = _dirName;
            pc_reader.dirName = dirName;
            pc_reader.loopCount = loopCount;
            pc_reader.gameObject.SetActive(true);
            pc_renderer.gameObject.SetActive(true);
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void RendererStarted()
        {
            Debug.Log($"{Name()}: Renderer started");
            started.Invoke();
        }

        public void RendererFinished()
        {
            Debug.Log($"{Name()}: Renderer finished");
            finished.Invoke();
            pc_reader.Stop();
        }
    }
}
