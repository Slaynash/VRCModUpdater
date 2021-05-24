using MelonLoader;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using VRCModUpdater.Core.Externs;
using VRCModUpdater.Loader;
using Windef;
using WinGDI;
using Winuser;

namespace VRCModUpdater.Core
{
    class UpdaterWindow
    {
        private static bool lightMode = true;
        internal static bool lemonMode = false;

        internal static IntPtr hWindow;
        public static bool IsOpen { get; private set; }
        public static bool IsWindowClosing { get; private set; }

        private static IntPtr hBackgroudBrush;
        private static IntPtr hHardBackgroudBrush;
        private static IntPtr hProgressbarBrush;

        private static int backgroundColor;
        private static int foregroundColor;

        private static IntPtr hTextFont;
        private static IntPtr hTitleFont;
        private static IntPtr hProgressbarFont;

        public static void CreateWindow()
        {
            if (IsOpen)
                return;

            MelonLogger.Msg("Starting window creation");

            CheckLightTheme();
            CheckLemonTheme();

            IntPtr hInstance = Process.GetCurrentProcess().Handle;
            string szClassName = "VRCModUpdaterWinClass";

            WndClass wc = default;

            wc.lpfnWndProc = HandleWindowEvent;

            wc.hInstance = hInstance;
            wc.lpszClassName = szClassName;

            if (lemonMode)
                //backgroundColor = GDI.RGB(255, 242, 0);
                backgroundColor = GDI.RGB(255, 236, 0);
            else if (lightMode)
                backgroundColor = GDI.RGB(255, 255, 255);
            else
                backgroundColor = GDI.RGB(42, 42, 46);

            hBackgroudBrush = GDI.CreateSolidBrush(backgroundColor);

            wc.hbrBackground = hBackgroudBrush;

            MelonLogger.Msg("Registering window class");
            ushort regResult = User32.RegisterClass(ref wc);

            if (regResult == 0)
            {
                MelonLogger.Warning("Failed to register updater window. Updating in windowless mode");
                GDI.DeleteObject(hBackgroudBrush);
                return;
            }

            MelonLogger.Msg("Creating window");
            hWindow = User32.CreateWindowEx(
                0,                                  // Optional window styles.
                szClassName,                        // Window class
                "VRCModUpdater",                    // Window text
                WindowStyles.WS_POPUPWINDOW,        // Window style

                // Size and position
                100, 100, 600, 250,

                IntPtr.Zero,    // Parent window
                IntPtr.Zero,    // Menu
                hInstance,      // Instance handle
                IntPtr.Zero);   // Additional application data

            if (hWindow == IntPtr.Zero)
            {
                int lastError = Marshal.GetLastWin32Error();
                string errorMessage = new Win32Exception(lastError).Message;
                MelonLogger.Warning("Failed to create updater window. Updating in windowless mode. Error:\n" + errorMessage);
                GDI.DeleteObject(hBackgroudBrush);
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

            if (personalizeKey != null)
            {
                object lightTheme = personalizeKey.GetValue("AppsUseLightTheme");

                if (lightTheme != null)
                    lightMode = (int)lightTheme == 1;
            }
            else
                lightMode = true;

            MelonLogger.Msg("Using light theme: " + lightMode);
        }

        private static void CheckLemonTheme()
        {
            lemonMode = Environment.GetCommandLineArgs().Contains("--lemonloader") || (DateTime.Now.Day == 01 && DateTime.Now.Month == 04);
        }

        private static IntPtr HandleWindowEvent(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WindowMessage.CREATE:
                    
                    if (lemonMode)
                    {
                        hHardBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(244, 220, 20));
                        hProgressbarBrush = GDI.CreateSolidBrush(GDI.RGB(182, 255, 100));

                        //foregroundColor = GDI.RGB(98, 247, 124);
                        foregroundColor = GDI.RGB(86, 223, 13);
                    }
                    else if (lightMode)
                    {
                        hHardBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(224, 224, 224));
                        hProgressbarBrush = GDI.CreateSolidBrush(GDI.RGB(120, 248, 99));

                        foregroundColor = GDI.RGB(0, 0, 0);
                    }
                    else
                    {
                        hHardBackgroudBrush = GDI.CreateSolidBrush(GDI.RGB(54, 57, 63));
                        hProgressbarBrush = GDI.CreateSolidBrush(GDI.RGB(255, 59, 106));

                        foregroundColor = GDI.RGB(220, 221, 222);
                    }

                    hTextFont = GDI.CreateFont(14, 0, 0, 0,
                        FontWeight.Medium, 0, 0, 0,
                        FontLanguageCharSet.Default, FontPrecision.Outline, FontClipPrecision.Default, FontQuality.Cleartype, FontPitch.Variable, "Segoe UI");

                    hTitleFont = GDI.CreateFont(26, 0, 0, 0,
                        FontWeight.Medium, 0, 0, 0,
                        FontLanguageCharSet.Default, FontPrecision.Outline, FontClipPrecision.Default, FontQuality.Cleartype, FontPitch.Variable, "Consolas");

                    hProgressbarFont = GDI.CreateFont(16, 0, 0, 0,
                        FontWeight.Medium, 0, 0, 0,
                        FontLanguageCharSet.Default, FontPrecision.Outline, FontClipPrecision.Default, FontQuality.Cleartype, FontPitch.Variable, "Segoe UI");

                    return IntPtr.Zero;

                case WindowMessage.DESTROY:
                    User32.PostQuitMessage(0);

                    GDI.DeleteObject(hBackgroudBrush);
                    GDI.DeleteObject(hHardBackgroudBrush);

                    GDI.DeleteObject(hProgressbarBrush);

                    GDI.DeleteObject(hTextFont);
                    GDI.DeleteObject(hTitleFont);
                    GDI.DeleteObject(hProgressbarFont);

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
                    GDI.SetBkColor(hdc, backgroundColor);
                    GDI.SetTextColor(hdc, foregroundColor);
                    GDI.SelectObject(hdc, hTextFont);

                    User32.GetClientRect(hWnd, out rect);

                    // Progress bars

                    GDI.SetBkMode(hdc, BackgroundMode.TRANSPARENT);
                    GDI.SelectObject(hdc, hProgressbarFont);
                    DrawProgressBar(hdc, VRCModUpdaterCore.progressTotal, VRCModUpdaterCore.currentStatus, 40, rect.Bottom - 100, rect.Right - 40, rect.Bottom - 70);
                    DrawProgressBar(hdc, VRCModUpdaterCore.progressDownload, null, 40, rect.Bottom - 60, rect.Right - 40, rect.Bottom - 30);

                    // Text
                    GDI.SetBkMode(hdc, BackgroundMode.OPAQUE);
                    Rect titleRect = new Rect(ps.rcPaint.Left + 5, ps.rcPaint.Top + 5, ps.rcPaint.Right - 5, ps.rcPaint.Top + 125 - 5);
                    GDI.SelectObject(hdc, hTitleFont);
                    User32.DrawText(hdc, "VRCModUpdater", -1, ref titleRect, DrawText.SINGLELINE | DrawText.CENTER | DrawText.VCENTER);

                    GDI.SelectObject(hdc, hTextFont);
                    User32.DrawText(hdc, $"Loader v{VRCModUpdaterPlugin.VERSION}\nCore v{VRCModUpdaterCore.VERSION}", -1, ref titleRect, DrawText.LEFT | DrawText.TOP);
                    User32.DrawText(hdc, $"MelonLoader {BuildInfo.Version}\nVRChat {UnityEngine.Application.version}", -1, ref titleRect, DrawText.RIGHT | DrawText.TOP);

                    // End paint
                    User32.EndPaint(hWnd, ref ps);

                    return IntPtr.Zero;
            }

            return User32.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void DrawProgressBar(IntPtr hdc, int progress, string text, int left, int top, int right, int bottom)
        {
            Rect outterRect = new Rect(left, top, right, bottom);
            User32.FillRect(hdc, ref outterRect, hHardBackgroudBrush);

            int progressPosition = (int) (progress * 0.01 * (right - left) + left);

            if (progressPosition != left)
            {
                Rect innerRectLeft = new Rect(left, top, progressPosition, bottom);
                User32.FillRect(hdc, ref innerRectLeft, hProgressbarBrush);
            }

            User32.DrawText(hdc, text ?? (progress + "%"), -1, ref outterRect, DrawText.SINGLELINE | DrawText.CENTER | DrawText.VCENTER);
        }
    }
}
