using MelonLoader;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VRCModUpdater.Utils;

namespace VRCModUpdater
{
    class UpdaterWindow
    {
        private static bool lightMode = true;

        private static IntPtr hWindow;
        public static bool IsOpen { get; private set; }
        public static bool IsWindowClosing { get; private set; }

        private static IntPtr hBackgroudBrush;
        private static IntPtr hHardBackgroudBrush;
        private static IntPtr hMelonredBrush;
        private static IntPtr hMelongreenBrush;

        private static int foregroundColor;

        public static void CreateWindow()
        {
            if (IsOpen)
                return;

            MelonLogger.Msg("Starting window creating");

            CheckLightTheme();


            IntPtr hInstance = Process.GetCurrentProcess().Handle;
            string szClassName = "VRCModUpdaterWinClass";

            WNDCLASS wc = default;

            wc.lpfnWndProc = HandleWindowEvent;

            wc.hInstance = hInstance;
            wc.lpszClassName = szClassName;

            //wc.style = ClassStyles.HorizontalRedraw | ClassStyles.VerticalRedraw;

            MelonLogger.Msg("Registering window class");
            ushort regResult = Externs.RegisterClass(ref wc);

            if (regResult == 0)
            {
                MelonLogger.Warning("Failed to register updater window. Updating in windowless mode");
                return;
            }

            /*
            wc.cbClsExtra = 0;
            wc.cbWndExtra = 0;
            wc.hIcon = Win32.LoadIcon(IntPtr.Zero, new IntPtr((int)SystemIcons.IDI_APPLICATION));
            wc.hCursor = Win32.LoadCursor(IntPtr.Zero, (int)IdcStandardCursors.IDC_ARROW);
            wc.hbrBackground = Win32.GetStockObject(StockObjects.WHITE_BRUSH);
            wc.lpszMenuName = null;
            wc.lpszClassName = szAppName;
            */

            MelonLogger.Msg("Creating window");
            hWindow = Externs.CreateWindowEx(
                0,                                  // Optional window styles.
                szClassName,                        // Window class
                "VRCModUpdater",                    // Window text
                WindowStyles.WS_POPUPWINDOW,        // Window style

                // Size and position
                100, 100, 600, 300,

                IntPtr.Zero,    // Parent window
                IntPtr.Zero,    // Menu
                hInstance,      // Instance handle
                IntPtr.Zero);   // Additional application data

            if (hWindow == IntPtr.Zero)
            {
                int lastError = Marshal.GetLastWin32Error();
                string errorMessage = new Win32Exception(lastError).Message;
                MelonLogger.Warning("Failed to create updater window. Updating in windowless mode. Error:\n" + errorMessage);
                return;
            }

            MelonLogger.Msg("Showing window");
            Externs.ShowWindow(hWindow, ShowWindowCommands.Normal);

            IsOpen = true;
        }

        internal static void RedrawWindow()
        {
            if (IsOpen)
                Externs.InvalidateRect(hWindow, IntPtr.Zero, false);
        }

        internal static void DestroyWindow()
        {
            MelonLogger.Msg("Destroying window. IsOpen: " + IsOpen);
            if (IsOpen)
            {
                Externs.DestroyWindow(hWindow);
                IsWindowClosing = true;
            }
        }



        private static void CheckLightTheme()
        {
            RegistryKey personalizeKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
            object lightTheme = personalizeKey.GetValue("AppsUseLightTheme");

            if (lightTheme != null)
                lightMode = (int)lightTheme == 1;

            MelonLogger.Msg("Using light theme: " + lightMode);
        }

        private static IntPtr HandleWindowEvent(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch ((WM)msg)
            {
                case WM.CREATE:
                    
                    if (lightMode)
                    {
                        hBackgroudBrush = Externs.CreateSolidBrush(Externs.RGB(255, 255, 255));
                        hHardBackgroudBrush = Externs.CreateSolidBrush(Externs.RGB(224, 224, 224));

                        foregroundColor = Externs.RGB(0, 0, 0);
                    }
                    else
                    {
                        hBackgroudBrush = Externs.CreateSolidBrush(Externs.RGB(42, 42, 46));
                        hHardBackgroudBrush = Externs.CreateSolidBrush(Externs.RGB(54, 57, 63));

                        foregroundColor = Externs.RGB(220, 221, 222);
                    }

                    hMelonredBrush = Externs.CreateSolidBrush(Externs.RGB(255, 59, 106));
                    hMelongreenBrush = Externs.CreateSolidBrush(Externs.RGB(120, 248, 99));

                    return IntPtr.Zero;

                case WM.DESTROY:
                    Externs.PostQuitMessage(0);

                    Externs.DeleteObject(hBackgroudBrush);
                    Externs.DeleteObject(hHardBackgroudBrush);

                    Externs.DeleteObject(hMelonredBrush);
                    Externs.DeleteObject(hMelongreenBrush);

                    IsOpen = false;

                    return IntPtr.Zero;

                case WM.NCHITTEST: // Drag'n'Drop everywhere
                    IntPtr hit = Externs.DefWindowProc(hWnd, (WM)msg, wParam, lParam);
                    if (hit.ToInt32() == 1) hit = new IntPtr(2);
                    return hit;

                case WM.CLOSE:
                    return IntPtr.Zero;

                case WM.PAINT:
                    PAINTSTRUCT ps;
                    RECT rect;

                    // Begin paint
                    IntPtr hdc = Externs.BeginPaint(hWnd, out ps);
                    Externs.SetBkMode(hdc, Externs.TRANSPARENT);
                    Externs.SetTextColor(hdc, foregroundColor);

                    // Background
                    Externs.FillRect(hdc, ref ps.rcPaint, hBackgroudBrush);

                    Externs.GetClientRect(hWnd, out rect);


                    DrawProgressBar(hdc, VRCModUpdaterPlugin.progressTotal, VRCModUpdaterPlugin.currentStatus, 40, rect.Bottom - 100, rect.Right - 40, rect.Bottom - 70);
                    DrawProgressBar(hdc, VRCModUpdaterPlugin.progressDownload, null, 40, rect.Bottom - 60, rect.Right - 40, rect.Bottom - 30);

                    // Text
                    RECT titleRect = new RECT(ps.rcPaint.Left, ps.rcPaint.Top, ps.rcPaint.Right, ps.rcPaint.Top + 100);
                    Externs.DrawText(hdc, "VRCModUpdater v1.0.0", -1, ref titleRect, Externs.DT_SINGLELINE | Externs.DT_CENTER | Externs.DT_VCENTER);

                    // End paint
                    Externs.EndPaint(hWnd, ref ps);

                    return IntPtr.Zero;
            }

            return Externs.DefWindowProc(hWnd, (WM)msg, wParam, lParam);
        }

        private static void DrawProgressBar(IntPtr hdc, int progress, string text, int left, int top, int right, int bottom)
        {
            RECT outterRect = new RECT(left, top, right, bottom);
            Externs.FillRect(hdc, ref outterRect, hHardBackgroudBrush);

            int progressPosition = (int) (progress * 0.01 * (right - left) + left);

            if (progressPosition != left)
            {
                RECT innerRectLeft = new RECT(left, top, progressPosition, bottom);
                Externs.FillRect(hdc, ref innerRectLeft, lightMode ? hMelongreenBrush : hMelonredBrush);
            }

            Externs.DrawText(hdc, text ?? (progress + "%"), -1, ref outterRect, Externs.DT_SINGLELINE | Externs.DT_CENTER | Externs.DT_VCENTER);
        }
    }
}
