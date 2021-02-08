using System;
using System.Runtime.InteropServices;
using Windef;
using WinGDI;

namespace VRCModUpdater.Core.Externs
{
    public static class GDI
    {
        [DllImport("gdi32.dll")]
        public static extern int SetBkMode(IntPtr hdc, BackgroundMode iBkMode);

        [DllImport("gdi32.dll")]
        public static extern int SetTextColor(IntPtr hdc, int color);

        [DllImport("gdi32.dll")]
        public static extern int SetBkColor(IntPtr hdc, int color);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateSolidBrush(int crColor);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFont(int nHeight, int nWidth, int nEscapement, int nOrientation,
            FontWeight fnWeight, uint fdwItalic, uint fdwUnderline,
            uint fdwStrikeOut, FontLanguageCharSet fdwCharSet, FontPrecision fdwOutputPrecision,
            FontClipPrecision fdwClipPrecision, FontQuality fdwQuality, FontPitch fdwPitchAndFamily, string lpszFace);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject([In] IntPtr hdc, [In] IntPtr hgdiobj);

        public static int RGB(byte r, byte g, byte b)
        {
            return r | g << 8 | b << 16;
        }
    }
}
