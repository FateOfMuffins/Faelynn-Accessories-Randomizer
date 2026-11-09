using System;
using System.Runtime.InteropServices;

namespace FaelynnRandomizer
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X, Y;
            internal Point(int x, int y) { X = x; Y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Bounds
        {
            internal int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Blend
        {
            internal byte Operation, Flags, Opacity, AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfo
        {
            internal uint Size;
            internal int Width, Height;
            internal ushort Planes, BitCount;
            internal uint Compression, ImageSize;
            internal int XPixelsPerMeter, YPixelsPerMeter;
            internal uint ColorsUsed, ColorsImportant;
        }

        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Bounds bounds);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after,
            int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(
            IntPtr window, IntPtr dstDc, ref Point destination, ref Point size, IntPtr srcDc,
            ref Point source, uint colorKey, ref Blend blend, uint flags);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(
            IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    }
}
