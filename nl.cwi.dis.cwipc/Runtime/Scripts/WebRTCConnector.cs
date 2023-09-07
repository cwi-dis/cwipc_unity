using AOT;
using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;
using System.Runtime.InteropServices.ComTypes;

namespace Cwipc
{
    /// <summary>
    /// MonoBehaviour that controls the WebRTC peer process and the native unmanaged plugin that interfaces to it.
    /// </summary>
    public class WebRTCConnector : MonoBehaviour
    {
        public static WebRTCConnector Instance;

        [Tooltip("Path to WebRTC peer executable")]
        public string peerExecutablePath;
        [Tooltip("Run peer in a window (windows-only)")]
        public bool peerInWindow = false;
        [Tooltip("Don't close window when peer terminates (windows-only")]
        public bool peerWindowDontClose = false;
        [Tooltip("UDP Port this peer will use to communicate with this connector instance")]
        public int peerUDPPort = 8000;

        [Tooltip("Set to a pathname to enable WebRTCConnector plugin logging")]
        public string logFileDirectory;
        [Tooltip("Enable for more messages")]
        public bool debug;
        [Tooltip("(introspection) connected to peer")]
        public bool peerConnected = false;
        [Tooltip("(introspection) SFU that peer is connected to")]
        public string peerSFUAddress;

        private Process peerProcess;

        [DllImport("WebRTCConnector")]
        static extern void set_logging(string log_directory, bool debug_mode);
        [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
        static extern void RegisterDebugCallback(debugCallback cb);
        [DllImport("WebRTCConnector")]
        static extern int connect_to_proxy(string ip_send, UInt32 port_send, string ip_recv, UInt32 port_recv, UInt32 number_of_tiles);

        // Create string param callback delegate
        delegate void debugCallback(IntPtr request, int color, int size);
        enum Color { red, green, blue, black, white, yellow, orange };
        [MonoPInvokeCallback(typeof(debugCallback))]
        static void OnDebugCallback(IntPtr request, int color, int size)
        {
            // Ptr to string
            string debug_string = Marshal.PtrToStringAnsi(request, size);
            // Add specified color
            debug_string =
                String.Format("WebRTCConnector: {0}{1}{2}{3}{4}",
                "<color=",
                ((Color)color).ToString(), ">", debug_string, "</color>");
            // Log the string
            Debug.Log(debug_string);
        }

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("WebRTCConnector: Instance already set, there should only be one in the scene");
            }
            Instance = this;
        }

        public void Initialize(string _peerExecutablePath)
        {
            if (!string.IsNullOrEmpty(_peerExecutablePath)) {
                peerExecutablePath = _peerExecutablePath;
            }
        }
        
        public void AllConnectionsDone()
        {

        }

        // Use this for initialization
        public void OnEnable()
        {
            Debug.Log($"WebRTCConnector: installing message callback");
            RegisterDebugCallback(OnDebugCallback);
            if (logFileDirectory != null && logFileDirectory != "")
            {
                set_logging(logFileDirectory, debug);
            }
        }

        public void OnDestroy()
        {
            if (peerProcess != null)
            {
                Debug.Log("WebRTCConnector: Terminating peer");
                peerProcess.Kill();
            }
            // xxxjack Can we unload the DLL?
        }

        public void Update()
        {
            // xxxjack Check that peerProcess is still running
            if (peerProcess != null && peerProcess.HasExited)
            {
                Debug.LogError($"WebRTCConnector: peer process has exited with exit status {peerProcess.ExitCode}");
                peerProcess = null;
            }
        }

        public void StartWebRTCPeer(Uri url)
        {
            string mySFUAddress = $"{url.Host}:{url.Port}";
            if (peerProcess != null)
            {
                // A peer has already been started. Double-check it's for the correct SFU.
                if (mySFUAddress != peerSFUAddress)
                {
                    Debug.LogError($"WebRTCConnector: want peer for SFU {mySFUAddress} but already have one for {peerSFUAddress}");
                }
                return;
            }
            peerSFUAddress = mySFUAddress;
            // xxxjack this is not correct for built Unity players.
            string appPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            peerExecutablePath = System.IO.Path.Combine(appPath, peerExecutablePath);
            peerProcess = new Process();
            peerProcess.StartInfo.FileName = peerExecutablePath;
            peerProcess.StartInfo.Arguments = $"-p :{peerUDPPort} -i -o -sfu {peerSFUAddress}";
            peerProcess.StartInfo.CreateNoWindow = !peerInWindow;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (peerInWindow && peerWindowDontClose)
            {
                // xxxjack this will fail if there are spaces in the pathname. But escaping them is impossible on Windows.
                peerProcess.StartInfo.Arguments = $"/K {peerProcess.StartInfo.FileName} {peerProcess.StartInfo.Arguments}";
                peerProcess.StartInfo.FileName = "CMD.EXE";
            }
#endif
            Debug.Log($"WebRTCConnector: Start {peerProcess.StartInfo.FileName} {peerProcess.StartInfo.Arguments}");
            try
            {
                if (!peerProcess.Start())
                {
                    Debug.LogError($"WebRTCConnector: Cannot start peer");
                    peerProcess = null;
                }
            }
            catch(System.Exception e)
            {
                Debug.LogError($"WebRTCConnector: Cannot start peer: {e.Message}");
                peerProcess = null;
            }
          
            /*
            // Create a process
            process_writer = new System.Diagnostics.Process();            

            // Set the StartInfo of process
            process_writer.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
            process_writer.StartInfo.FileName = "D:\\Nextcloud\\Internship\\GoWebRTCPeer\\peer.exe";
            process_writer.StartInfo.Arguments = "-p :8000 -i";

            // Start the process
            process_writer.Start();
            // process.WaitForExit();

            // Create a process
            process_reader = new System.Diagnostics.Process();

            // Set the StartInfo of process
            process_reader.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
            process_reader.StartInfo.FileName = "D:\\Nextcloud\\Internship\\GoWebRTCPeer\\peer.exe";
            process_reader.StartInfo.Arguments = "-p :8001 -o -n";

            // Start the process
            process_reader.Start();
            // process.WaitForExit();
            */

        }

        public void ConnectToPeer(int nThreads)
        {
            // xxxjack is worried about nThreads/number_of_tiles. Does this suppose a symmetry
            // between sender and receiver (i.e. between two instances of VR2Gather)?
            if (peerConnected) return;
            //Thread.Sleep(2000);
            connect_to_proxy("127.0.0.1", (uint)peerUDPPort, "127.0.0.1", (uint)peerUDPPort, (uint)nThreads);
            //Thread.Sleep(1000);
            peerConnected = true;
        }

    }
}
