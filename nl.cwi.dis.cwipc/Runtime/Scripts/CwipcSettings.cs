using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cwipc
{
    public class CwipcSettings : MonoBehaviour
    {
        [Tooltip("CWIPC log level")]
        public cwipc.LogLevel logLevel = cwipc.LogLevel.WARNING;
        // Start is called before the first frame update
        void Awake()
        {
            cwipc.install_logger(logLevel);
        }
    }
}
