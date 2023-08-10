using AOT;
using System.Runtime.InteropServices;
using System;
using UnityEngine;

public class DebugCPP : MonoBehaviour
{
    [Tooltip("Set to an absolute pathname to enable WebRTCConnector logging")]
    public string logFileName;
    [Tooltip("Enable for more messages")]
    public bool debug;
    [DllImport("WebRTCConnector")]
    static extern void set_logging(string log_directory, bool debug_mode);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    static extern void RegisterDebugCallback(debugCallback cb);
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

    // Use this for initialization
    public void OnEnable()
    {
        RegisterDebugCallback(OnDebugCallback);
        if (logFileName != null && logFileName != "")
        {
            set_logging(logFileName, debug);
        }

    }
}
