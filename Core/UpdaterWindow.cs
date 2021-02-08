using MelonLoader;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VRCModUpdater.Core.Externs;
using Windef;
using Winuser;

namespace VRCModUpdater.Core
{
    class UpdaterWindow
    {
        private static bool lightMode = true;

        internal static IntPtr hWindow;
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

            MelonLogger.Msg("Starting window creation");

            CheckLightTheme();


            IntPtr hInstance = Process.GetCurrentProcess().Handle;
            string szClassName = "VRCModUpdaterWinClass";

            WndClass wc = default;

            wc.lpfnWndProc = HandleWindowEvent;

            wc.hInstance = hInstance;
            wc.lpszClassName = szClassName;

            //wc.style = ClassStyles.HorizontalRedraw | ClassStyles.VerticalRedraw;

            MelonLogger.Msg("Registering window class");
            ushort regResult = User32.RegisterClass(ref wc);

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
            hWindow = User32.CreateWindowEx(
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
            User32.ShowWindow(hWindow, ShowWindowCommands.Normal);

            IsOpen = true;
        }

        internal static void RedrawWindow()
        {
            if (IsOpen)
                User32.InvalidateRect(hWindow, IntPtr.Zero, false);
        }

        internal static void DestroyWindow()
        {
            MelonLogger.Msg("Destroying window. IsOpen: " + IsOpen);
            if (IsOpen)
            {
                User32.DestroyWindow(hWindow);
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
            switch ((WindowMessage)msg)
            {
                case WindowMessage.CREATE:
                    
                    if (lightMode)
                    {
                        hBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(255, 255, 255));
                        hHardBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(224, 224, 224));

                        foregroundColor = GDI.RGB(0, 0, 0);
                    }
                    else
                    {
                        hBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(42, 42, 46));
                        hHardBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(54, 57, 63));

                        foregroundColor = GDI.RGB(220, 221, 222);
                    }

                    hMelonredBrush = GDI.CreateSolidBrush(GDI.RGB(255, 59, 106));
                    hMelongreenBrush = GDI.CreateSolidBrush(GDI.RGB(120, 248, 99));

                    return IntPtr.Zero;

                case WindowMessage.DESTROY:
                    User32.PostQuitMessage(0);

                    GDI.DeleteObject(hBackgroudBrush);
                    GDI.DeleteObject(hHardBackgroudBrush);

                    GDI.DeleteObject(hMelonredBrush);
                    GDI.DeleteObject(hMelongreenBrush);

                    IsOpen = false;

                    return IntPtr.Zero;

                case WindowMessage.NCHITTEST: // Drag'n'Drop everywhere
                    IntPtr hit = User32.DefWindowProc(hWnd, (WindowMessage)msg, wParam, lParam);
                    if (hit.ToInt32() == 1) hit = new IntPtr(2);
                    return hit;

                case WindowMessage.CLOSE:
                    return IntPtr.Zero;

                case WindowMessage.PAINT:
                    PaintStruct ps;
                    Rect rect;

                    // Begin paint
                    IntPtr hdc = User32.BeginPaint(hWnd, out ps);
                    GDI.SetBkMode(hdc, BackgroundMode.TRANSPARENT);
                    GDI.SetTextColor(hdc, foregroundColor);

                    // Background
                    User32.FillRect(hdc, ref ps.rcPaint, hBackgroudBrush);

                    User32.GetClientRect(hWnd, out rect);


                    DrawProgressBar(hdc, VRCModUpdaterCore.progressTotal, VRCModUpdaterCore.currentStatus, 40, rect.Bottom - 100, rect.Right - 40, rect.Bottom - 70);
                    DrawProgressBar(hdc, VRCModUpdaterCore.progressDownload, null, 40, rect.Bottom - 60, rect.Right - 40, rect.Bottom - 30);

                    // Text
                    Rect titleRect = new Rect(ps.rcPaint.Left, ps.rcPaint.Top, ps.rcPaint.Right, ps.rcPaint.Top + 100);
                    User32.DrawText(hdc, "VRCModUpdater v" + VRCModUpdaterCore.VERSION, -1, ref titleRect, DrawText.SINGLELINE | DrawText.CENTER | DrawText.VCENTER);

                    // End paint
                    User32.EndPaint(hWnd, ref ps);

                    return IntPtr.Zero;
            }

            return User32.DefWindowProc(hWnd, (WindowMessage)msg, wParam, lParam);
        }

        private static void DrawProgressBar(IntPtr hdc, int progress, string text, int left, int top, int right, int bottom)
        {
            Rect outterRect = new Rect(left, top, right, bottom);
            User32.FillRect(hdc, ref outterRect, hHardBackgroudBrush);

            int progressPosition = (int) (progress * 0.01 * (right - left) + left);

            if (progressPosition != left)
            {
                Rect innerRectLeft = new Rect(left, top, progressPosition, bottom);
                User32.FillRect(hdc, ref innerRectLeft, lightMode ? hMelongreenBrush : hMelonredBrush);
            }

            User32.DrawText(hdc, text ?? (progress + "%"), -1, ref outterRect, DrawText.SINGLELINE | DrawText.CENTER | DrawText.VCENTER);
        }
    }
}
