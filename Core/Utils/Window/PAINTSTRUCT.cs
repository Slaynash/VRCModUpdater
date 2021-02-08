using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct PAINTSTRUCT
{
    public IntPtr hdc;
    public bool fErase;
    public RECT rcPaint;
    public bool fRestore;
    public bool fIncUpdate;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
}