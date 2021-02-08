using System;
using System.Runtime.InteropServices;
using Windef;
using Winuser;

namespace VRCModUpdater.Core.Externs
{
    public static class User32
    {
        // Window

        [DllImport("user32.dll")]
        public static extern ushort RegisterClass([In] ref WndClass lpwcx);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            WindowStylesEx dwExStyle,
            string lpClassName,
            string lpWindowName,
            WindowStyles dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

        // Window Messages

        [DllImport("user32.dll")]
        public static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern bool GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage([In] ref Msg lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage([In] ref Msg lpmsg);

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, WindowMessage uMsg, IntPtr wParam, IntPtr lParam);

        // Window render

        [DllImport("user32.dll")]
        public static extern IntPtr BeginPaint(IntPtr hwnd, out PaintStruct lpPaint);

        [DllImport("user32.dll")]
        public static extern bool EndPaint(IntPtr hWnd, [In] ref PaintStruct lpPaint);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        public static extern int DrawText(IntPtr hdc, string lpchText, int cchText, ref Rect lprc, DrawText format);

        [DllImport("user32.dll")]
        public static extern int FillRect(IntPtr hDC, [In] ref Rect lprc, IntPtr hbr);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hwnd);

        // MessageBox

        [DllImport("User32.dll")]
        public static extern int MessageBox(IntPtr nWnd, string text, string title, uint type);
    }
}
