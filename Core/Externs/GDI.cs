using System;
using System.Runtime.InteropServices;
using Windef;

namespace VRCModUpdater.Core.Externs
{
    public static class GDI
    {
        [DllImport("gdi32.dll")]
        public static extern int SetBkMode(IntPtr hdc, BackgroundMode iBkMode);

        [DllImport("gdi32.dll")]
        public static extern int SetTextColor(IntPtr hdc, int color);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateSolidBrush(int crColor);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public static int RGB(byte r, byte g, byte b)
        {
            return r | g << 8 | b << 16;
        }
    }
}
