using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VRCModUpdater.Utils
{
    public static class Externs
    {

        // GameAssembly

        [DllImport("GameAssembly", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static IntPtr il2cpp_resolve_icall(string name);

        // user32.dll

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
        public static extern ushort RegisterClass([In] ref WNDCLASS lpwcx);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

        [DllImport("user32.dll")]
        public static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, WM uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern int DrawText(IntPtr hdc, string lpchText, int cchText, ref RECT lprc, uint format);

        [DllImport("user32.dll")]
        public static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool EndPaint(IntPtr hWnd, [In] ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        public static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, IntPtr hbr);

        [DllImport("user32.dll")]
        public static extern bool UpdateWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        // gdi32.dll

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateSolidBrush(int crColor);

        [DllImport("gdi32.dll")]
        public static extern int SetBkMode(IntPtr hdc, int iBkMode);

        [DllImport("gdi32.dll")]
        public static extern int SetTextColor(IntPtr hdc, int color);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        // winmm.dll

        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);

        // kernel32.dll

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);



        public static int RGB(byte r, byte g, byte b)
        {
            return r | g << 8 | b << 16;
        }


        public const int DT_TOP                    = 0x00000000;
        public const int DT_LEFT                   = 0x00000000;
        public const int DT_CENTER                 = 0x00000001;
        public const int DT_RIGHT                  = 0x00000002;
        public const int DT_VCENTER                = 0x00000004;
        public const int DT_BOTTOM                 = 0x00000008;
        public const int DT_WORDBREAK              = 0x00000010;
        public const int DT_SINGLELINE             = 0x00000020;
        public const int DT_EXPANDTABS             = 0x00000040;
        public const int DT_TABSTOP                = 0x00000080;
        public const int DT_NOCLIP                 = 0x00000100;
        public const int DT_EXTERNALLEADING        = 0x00000200;
        public const int DT_CALCRECT               = 0x00000400;
        public const int DT_NOPREFIX               = 0x00000800;
        public const int DT_INTERNAL               = 0x00001000;
        public const int DT_EDITCONTROL            = 0x00002000;
        public const int DT_PATH_ELLIPSIS          = 0x00004000;
        public const int DT_END_ELLIPSIS           = 0x00008000;
        public const int DT_MODIFYSTRING           = 0x00010000;
        public const int DT_RTLREADING             = 0x00020000;
        public const int DT_WORD_ELLIPSIS          = 0x00040000;
        public const int DT_NOFULLWIDTHCHARBREAK   = 0x00080000;
        public const int DT_HIDEPREFIX             = 0x00100000;
        public const int DT_PREFIXONLY             = 0x00200000;

        public const int TRANSPARENT   = 1;
        public const int OPAQUE        = 2;
    }
}
