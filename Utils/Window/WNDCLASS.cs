using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct WNDCLASS
{
    public ClassStyles style;
    [MarshalAs(UnmanagedType.FunctionPtr)]
    public WndProc lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    public string lpszMenuName;
    public string lpszClassName;
}