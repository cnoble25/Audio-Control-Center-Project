#if WINDOWS
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using MauiWindow = Microsoft.Maui.Controls.Window;

namespace Audio_Control_Center_Application.Services
{
    public class SystemTrayService : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 1;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_CONTEXTMENU = 0x007B;
        private const int WM_COMMAND = 0x0111;
        private const int TPM_RIGHTBUTTON = 0x0002;
        private const int TPM_LEFTALIGN = 0x0000;
        private const int TPM_RETURNCMD = 0x0100;
        private const uint WM_USER_TRAYICON = WM_USER + 1;
        private const uint ID_TRAY_SHOW = WM_USER + 2;
        private const uint ID_TRAY_EXIT = WM_USER + 3;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool InsertMenu(IntPtr hMenu, int uPosition, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern IntPtr RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public int cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        private const uint CS_HREDRAW = 0x0002;
        private const uint CS_VREDRAW = 0x0001;
        private const uint IDI_APPLICATION = 32512;
        private const uint IDC_ARROW = 32512;

        private IntPtr _windowHandle;
        private IntPtr _iconHandle;
        private bool _disposed = false;
        private MauiWindow? _mainWindow;
        private const string WINDOW_CLASS = "AudioControlCenterTrayWindow";
        private static bool _windowClassRegistered = false;
        private static readonly object _lock = new object();

        public event EventHandler? ShowWindowRequested;
        public event EventHandler? ExitRequested;

        private static IntPtr WindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            if (uMsg == WM_TRAYICON)
            {
                var instance = GetInstanceFromHandle(hWnd);
                if (instance != null)
                {
                    if (lParam.ToInt32() == WM_LBUTTONDBLCLK)
                    {
                        // Double-click - show window
                        instance.ShowWindowRequested?.Invoke(instance, EventArgs.Empty);
                        return IntPtr.Zero;
                    }
                    else if (lParam.ToInt32() == WM_RBUTTONUP || lParam.ToInt32() == WM_CONTEXTMENU)
                    {
                        // Right-click - show context menu
                        instance.ShowContextMenu();
                        return IntPtr.Zero;
                    }
                }
            }
            else if (uMsg == WM_COMMAND)
            {
                var instance = GetInstanceFromHandle(hWnd);
                if (instance != null)
                {
                    var commandId = wParam.ToInt32();
                    if (commandId == ID_TRAY_SHOW)
                    {
                        instance.ShowWindowRequested?.Invoke(instance, EventArgs.Empty);
                    }
                    else if (commandId == ID_TRAY_EXIT)
                    {
                        instance.ExitRequested?.Invoke(instance, EventArgs.Empty);
                    }
                }
            }

            return DefWindowProc(hWnd, uMsg, wParam, lParam);
        }

        private static SystemTrayService? GetInstanceFromHandle(IntPtr hWnd)
        {
            // Use GetWindowLongPtr to store instance pointer, or use a static dictionary
            // For simplicity, we'll use a static dictionary approach
            return _instances.TryGetValue(hWnd, out var instance) ? instance : null;
        }

        private static readonly Dictionary<IntPtr, SystemTrayService> _instances = new();
        private static readonly WeakReference<Func<IntPtr, uint, IntPtr, IntPtr, IntPtr>> _windowProcDelegateRef = new(null!);

        public SystemTrayService()
        {
            try
            {
                // Register window class if not already registered
                RegisterWindowClass();

                // Get application icon
                _iconHandle = LoadIcon(IntPtr.Zero, new IntPtr(IDI_APPLICATION));

                // Create message-only window
                _windowHandle = CreateWindowEx(
                    0,
                    WINDOW_CLASS,
                    "Audio Control Center Tray Window",
                    0,
                    0, 0, 0, 0,
                    new IntPtr(-3), // HWND_MESSAGE - message-only window
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (_windowHandle == IntPtr.Zero)
                {
                    throw new Exception($"Failed to create window. Error: {Marshal.GetLastWin32Error()}");
                }

                // Store instance in dictionary
                _instances[_windowHandle] = this;

                // Add icon to system tray
                AddTrayIcon();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating system tray icon: {ex.Message}");
                throw;
            }
        }

        private static void RegisterWindowClass()
        {
            lock (_lock)
            {
                if (_windowClassRegistered) return;

                // Create delegate and keep reference to prevent GC
                Func<IntPtr, uint, IntPtr, IntPtr, IntPtr> windowProcDelegate = WindowProc;
                
                var wc = new WNDCLASS
                {
                    style = CS_HREDRAW | CS_VREDRAW,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(windowProcDelegate),
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = IntPtr.Zero,
                    hIcon = LoadIcon(IntPtr.Zero, new IntPtr(IDI_APPLICATION)),
                    hCursor = IntPtr.Zero, // Will use default
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null,
                    lpszClassName = WINDOW_CLASS
                };

                if (RegisterClass(ref wc) == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 1410) // ERROR_CLASS_ALREADY_EXISTS
                    {
                        throw new Exception($"Failed to register window class. Error: {error}");
                    }
                }

                _windowClassRegistered = true;
            }
        }

        public void SetMainWindow(MauiWindow? window)
        {
            _mainWindow = window;
        }

        private void AddTrayIcon()
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = (int)WM_TRAYICON,
                hIcon = _iconHandle,
                szTip = "Audio Control Center"
            };

            Shell_NotifyIcon(NIM_ADD, ref data);
        }

        private void ShowContextMenu()
        {
            try
            {
                GetCursorPos(out POINT point);

                var menu = CreatePopupMenu();
                if (menu == IntPtr.Zero)
                {
                    Debug.WriteLine("Failed to create popup menu");
                    return;
                }

                try
                {
                    AppendMenu(menu, 0, new IntPtr(ID_TRAY_SHOW), "Show Window");
                    AppendMenu(menu, 0x0800, IntPtr.Zero, null); // Separator
                    AppendMenu(menu, 0, new IntPtr(ID_TRAY_EXIT), "Exit");

                    // Set foreground window to ensure menu appears on top
                    SetForegroundWindow(_windowHandle);

                    TrackPopupMenuEx(
                        menu,
                        TPM_RIGHTBUTTON | TPM_LEFTALIGN | TPM_RETURNCMD,
                        point.x,
                        point.y,
                        _windowHandle,
                        IntPtr.Zero);
                }
                finally
                {
                    DestroyMenu(menu);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing context menu: {ex.Message}");
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public void ShowNotification(string title, string message)
        {
            try
            {
                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _windowHandle,
                    uID = 1,
                    uFlags = NIF_INFO,
                    szInfo = message,
                    szInfoTitle = title,
                    dwInfoFlags = 1 // NIIF_INFO
                };

                Shell_NotifyIcon(NIM_MODIFY, ref data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing notification: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    // Remove icon from system tray
                    var data = new NOTIFYICONDATA
                    {
                        cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                        hWnd = _windowHandle,
                        uID = 1
                    };

                    Shell_NotifyIcon(NIM_DELETE, ref data);

                    // Remove from instances dictionary
                    lock (_lock)
                    {
                        _instances.Remove(_windowHandle);
                        
                        // If no more instances, we could potentially unregister the window class
                        // But it's safer to leave it registered in case new instances are created
                    }

                    // Clear window reference
                    _mainWindow = null;
                    
                    // Clear icon handle
                    _iconHandle = IntPtr.Zero;

                    // Destroy window
                    if (_windowHandle != IntPtr.Zero)
                    {
                        DestroyWindow(_windowHandle);
                        _windowHandle = IntPtr.Zero;
                    }
                    
                    // Unsubscribe from events
                    ShowWindowRequested = null;
                    ExitRequested = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing system tray service: {ex.Message}");
                }

                _disposed = true;
            }
        }
    }
}
#endif
