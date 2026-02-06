using System.Diagnostics;
#if WINDOWS
using Audio_Control_Center_Application.Services;
using WinUIWindow = Microsoft.UI.Xaml.Window;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
#endif

namespace Audio_Control_Center_Application
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
#if WINDOWS
        private SystemTrayService? _systemTrayService;
        private Microsoft.Maui.Controls.Window? _mainWindow;
#endif

        private System.UnhandledExceptionEventHandler? _unhandledExceptionHandler;
        private EventHandler<System.Threading.Tasks.UnobservedTaskExceptionEventArgs>? _unobservedTaskExceptionHandler;
#if WINDOWS
        private EventHandler? _processExitHandler;
        private Windows.Foundation.TypedEventHandler<AppWindow, AppWindowClosingEventArgs>? _appWindowClosingHandler;
#endif

        public App()
        {
            InitializeComponent();
            
            // Handle unhandled exceptions globally to prevent crashes
            _unhandledExceptionHandler = (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                Debug.WriteLine($"Unhandled Exception (Terminating: {args.IsTerminating}): {exception?.GetType().Name} - {exception?.Message}");
                if (exception != null)
                {
                    Debug.WriteLine($"Stack trace: {exception.StackTrace}");
                }
                // Log but try to continue if possible
            };
            AppDomain.CurrentDomain.UnhandledException += _unhandledExceptionHandler;
            
            _unobservedTaskExceptionHandler = (sender, args) =>
            {
                Debug.WriteLine($"Unobserved Task Exception: {args.Exception.GetType().Name} - {args.Exception.Message}");
                args.SetObserved(); // Mark as handled to prevent crash
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += _unobservedTaskExceptionHandler;

#if WINDOWS
            // Initialize system tray service (Windows only)
            InitializeSystemTray();
#endif
        }
        
#if WINDOWS
        // Cleanup on app termination via ProcessExit handler
        private void CleanupOnExit()
        {
            if (_processExitHandler != null)
            {
                AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
                _processExitHandler = null;
            }
            
            // Unsubscribe window closing handler
            if (_mainWindow?.Handler?.PlatformView is WinUIWindow winUIWindow && _appWindowClosingHandler != null)
            {
                try
                {
                    winUIWindow.AppWindow.Closing -= _appWindowClosingHandler;
                }
                catch { }
                _appWindowClosingHandler = null;
            }
            
            // Cleanup exception handlers
            if (_unhandledExceptionHandler != null)
            {
                AppDomain.CurrentDomain.UnhandledException -= _unhandledExceptionHandler;
                _unhandledExceptionHandler = null;
            }
            
            if (_unobservedTaskExceptionHandler != null)
            {
                System.Threading.Tasks.TaskScheduler.UnobservedTaskException -= _unobservedTaskExceptionHandler;
                _unobservedTaskExceptionHandler = null;
            }
            
            // Dispose system tray service
            _systemTrayService?.Dispose();
            _systemTrayService = null;
            _mainWindow = null;
            
            // Cleanup audio resources
            AudioController.CleanupCache();
        }
#endif

#if WINDOWS
        private void InitializeSystemTray()
        {
            try
            {
                // Windows Forms requires STA thread, so initialize on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _systemTrayService = new SystemTrayService();
                        _systemTrayService.ShowWindowRequested += OnShowWindowRequested;
                        _systemTrayService.ExitRequested += OnExitRequested;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error initializing system tray: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing system tray: {ex.Message}");
            }
        }

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
#endif

        private void OnShowWindowRequested(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_mainWindow != null && _mainWindow.Handler?.PlatformView is WinUIWindow window)
                    {
                        try
                        {
                            var windowId = window.AppWindow.Id;
                            var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(windowId);
                            
                            // Show and restore window
                            ShowWindow(hwnd, SW_RESTORE);
                            SetForegroundWindow(hwnd);
                            window.Activate();
                            window.AppWindow.Show();
                        }
                        catch (Exception ex2)
                        {
                            // Fallback: just activate window
                            Debug.WriteLine($"Error bringing window to front: {ex2.Message}");
                            try
                            {
                                window.Activate();
                                window.AppWindow.Show();
                            }
                            catch
                            {
                                // Ignore activation errors
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error showing window: {ex.Message}");
                }
            });
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _systemTrayService?.Dispose();
                Quit();
            });
        }
#endif

        protected override Microsoft.Maui.Controls.Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                var window = new Microsoft.Maui.Controls.Window(new AppShell());
#if WINDOWS
                _mainWindow = window;
                
                // Set main window in system tray service
                _systemTrayService?.SetMainWindow(window);

                // Handle window closing - minimize to tray instead of closing
                if (window.Handler?.PlatformView is WinUIWindow winUIWindow)
                {
                    // Store closing handler reference for cleanup
                    _appWindowClosingHandler = (s, e) =>
                    {
                        e.Cancel = true; // Cancel the close
                        // Hide window instead
                        try
                        {
                            winUIWindow.AppWindow.Hide();
                            
                            // Show notification
                            _systemTrayService?.ShowNotification(
                                "Audio Control Center",
                                "Application minimized to system tray. Double-click the tray icon to restore."
                            );
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error hiding window: {ex.Message}");
                        }
                    };
                    winUIWindow.AppWindow.Closing += _appWindowClosingHandler;
                }
                
                // Cleanup on app termination
                _processExitHandler = (s, e) =>
                {
                    CleanupOnExit();
                };
                AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
#endif
                
                // Handle window-level exceptions
                window.Created += (s, e) =>
                {
                    Debug.WriteLine("Window created successfully");
                };
                
                return window;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating window: {ex.GetType().Name} - {ex.Message}");
                // Return a basic window to prevent complete failure
                return new Microsoft.Maui.Controls.Window(new AppShell());
            }
        }
    }
}
