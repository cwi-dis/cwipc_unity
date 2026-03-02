using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwipc
{
    public class CwipcSettings : MonoBehaviour
    {
        [Tooltip("Configuration for cwipc")]
        public CwipcConfig config;
        [Tooltip("CWIPC log level")]
        // Start is called before the first frame update
        void Awake()
        {
            CwipcConfig.SetInstance(config);
        }
    }
}
