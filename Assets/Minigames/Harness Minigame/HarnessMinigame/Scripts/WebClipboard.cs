using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Copy text to the system clipboard. In the editor / desktop this uses Unity's clipboard;
// in a WebGL build it calls the browser (Plugins/WebGL/WebClipboard.jslib).
public static class WebClipboard
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void WebClipboard_Copy(string text);
#endif

    public static void Copy(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebClipboard_Copy(text ?? "");
#else
        GUIUtility.systemCopyBuffer = text ?? "";
#endif
    }
}
