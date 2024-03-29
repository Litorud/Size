﻿using System;
using System.Runtime.InteropServices;

namespace Size
{
    static class Api
    {
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int DWMWA_CLOAKED = 14;
        private const int MONITOR_DEFAULTTOPRIMARY = 1;
        private const int MDT_EFFECTIVE_DPI = 0;

        [DllImport("user32.dll")]
        private static extern int GetWindowRect(IntPtr hWnd, out Rect lpRECT);

        [DllImport("dwmapi.dll")]
        private static extern bool DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out Rect rect, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern bool DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out bool cloaked, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, int dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("User32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

        public static Rect GetWindowRect(IntPtr windowHandle)
        {
            GetWindowRect(windowHandle, out var windowRect);
            return windowRect;
        }

        public static Rect GetExtendedFrameBounds(IntPtr windowHandle)
        {
            DwmGetWindowAttribute(windowHandle, DWMWA_EXTENDED_FRAME_BOUNDS, out Rect extendedFrameBounds, Marshal.SizeOf(typeof(System.Windows.Rect)));
            return extendedFrameBounds;
        }

        /// <summary>
        /// 指定したウィンドウが “cloaked” 状態なら true、そうでなければ false を返します。
        /// </summary>
        public static bool IsCloaked(IntPtr windowHandle)
        {
            DwmGetWindowAttribute(windowHandle, DWMWA_CLOAKED, out bool cloaked, Marshal.SizeOf(typeof(bool)));
            return cloaked;
        }

        public static IntPtr MonitorFromWindow(IntPtr windowHandle)
        {
            return MonitorFromWindow(windowHandle, MONITOR_DEFAULTTOPRIMARY);
        }

        public static (uint dpiX, uint dpiY) GetDpiForMonitor(IntPtr monitorHandle)
        {
            GetDpiForMonitor(monitorHandle, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY);
            return (dpiX, dpiY);
        }

        public static void MoveWindow(IntPtr windowHandle, int x, int y, int width, int height)
        {
            MoveWindow(windowHandle, x, y, width, height, true);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
