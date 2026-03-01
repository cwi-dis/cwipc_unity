using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwipc
{
    public class CwipcSettings : MonoBehaviour
    {
        [Tooltip("CWIPC log level")]
        public cwipc.LogLevel logLevel = cwipc.LogLevel.WARNING;
        [Tooltip("Override from configuration")]
        public bool overrideFromConfiguration = false;
        // Start is called before the first frame update
        void Awake()
        {
            if (overrideFromConfiguration)
            {
                logLevel = (cwipc.LogLevel)CwipcConfig.Instance.logLevel;
            }
            cwipc.install_logger(logLevel);
        }
    }
}
