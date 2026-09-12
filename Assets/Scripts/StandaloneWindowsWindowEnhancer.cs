#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Windows スタンドアロンでタイトルバーの最大化が無効化されるケースへの対策。
/// プロセスのメインウィンドウに WS_MAXIMIZEBOX / WS_MINIMIZEBOX を付与する。
/// </summary>
internal static class StandaloneWindowsWindowEnhancer
{
    private const int GwlStyle = -16;

    private const uint WsMaximizebox = 0x00010000;
    private const uint WsMinimizebox = 0x00020000;

    private static readonly IntPtr HwndTop = IntPtr.Zero;

    private const uint SwpNomove = 0x0002;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpFramechanged = 0x0020;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        GameObject host = new GameObject(nameof(StandaloneWindowsWindowEnhancer));
        host.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<ApplyOnceRunner>();
    }

    private sealed class ApplyOnceRunner : MonoBehaviour
    {
        private int _frames;

        private void Update()
        {
            // MainWindowHandle が有効になるまで数フレーム待つ。
            if (++_frames < 5)
            {
                return;
            }

            TryApply();
            Destroy(gameObject);
        }
    }

    private static void TryApply()
    {
        IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        long style = GetWindowLongPtr(hWnd, GwlStyle);
        if (style == 0)
        {
            return;
        }

        long newStyle = style | WsMaximizebox | WsMinimizebox;
        if (newStyle == style)
        {
            return;
        }

        SetWindowLongPtr(hWnd, GwlStyle, (IntPtr)newStyle);
        SetWindowPos(hWnd, HwndTop, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNozorder | SwpFramechanged);
    }

    private static long GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, nIndex);
        }

        return GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }

        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern long GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
#endif
