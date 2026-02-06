using System.Diagnostics;
using Audio_Control_Center_Application;
using System.Management;
using System.IO.Ports;
using Audio_Control_Center_Application.Views;

namespace Audio_Control_Center_Application
{
    public partial class MainPage : ContentPage
    {
        private SliderBuilder? _sliderBuilder;
        private EventHandler? _disappearingHandler;

        public MainPage()
        {
            try
            {
                InitializeComponent();
                
                // Wait a moment for XAML to fully initialize before accessing elements
                this.Loaded += (s, e) =>
                {
                    try
                    {
                        // Initialize the slider builder which will set up the UI
                        // Wrap in try-catch to prevent crashes during initialization
                        try
                        {
                            _sliderBuilder = new SliderBuilder(Container, UpdateConnectionStatus);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error initializing SliderBuilder: {ex.GetType().Name} - {ex.Message}");
                            // Continue without slider builder - user can restart app
                        }
                        
                        // Update connection status initially
                        UpdateConnectionStatus(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in MainPage Loaded event: {ex.GetType().Name} - {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in MainPage constructor: {ex.GetType().Name} - {ex.Message}");
                // Try to continue anyway
            }
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                var settingsPage = new SettingsPage();
                
                // Use modal to know when settings page is closed
                // Store handler reference for potential cleanup
                _disappearingHandler = (s, args) =>
                {
                    try
                    {
                        // Clear existing sliders first to prevent duplicates
                        if (Container != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try
                                {
                                    Container.Children.Clear();
                                    System.Diagnostics.Debug.WriteLine("Cleared container children when returning from settings");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error clearing container: {ex.GetType().Name} - {ex.Message}");
                                }
                            });
                        }
                        
                        // Reload settings and potentially reconnect
                        if (_sliderBuilder != null)
                        {
                            _sliderBuilder.ReloadSettings();
                            // Reset sliders - they will be recreated when device sends data
                            _sliderBuilder.ResetSliders();
                            // Optionally refresh applications and reconnect
                            if (Container != null)
                            {
                                _sliderBuilder.RefreshApplicationsAndPickers();
                                _sliderBuilder.Reconnect();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in settings page Disappearing event: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
                        // Don't rethrow - just log the error
                    }
                };
                settingsPage.Disappearing += _disappearingHandler;
                
                await Navigation.PushAsync(settingsPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening settings: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public void UpdateConnectionStatus(bool isConnected)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        // Check if elements exist before accessing them
                        if (ConnectionIndicator == null || ConnectionStatusLabel == null || ReconnectButton == null)
                        {
                            System.Diagnostics.Debug.WriteLine("Connection UI elements not yet initialized");
                            return;
                        }

                        if (isConnected)
                        {
                            ConnectionIndicator.Fill = new SolidColorBrush(Color.FromArgb("#4CAF50")); // Green
                            ConnectionStatusLabel.Text = "Connected to Device";
                            ReconnectButton.IsVisible = false;
                        }
                        else
                        {
                            ConnectionIndicator.Fill = new SolidColorBrush(Color.FromArgb("#FF9800")); // Orange
                            ConnectionStatusLabel.Text = "Disconnected";
                            ReconnectButton.IsVisible = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating connection status UI: {ex.GetType().Name} - {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateConnectionStatus: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private async void OnReconnectClicked(object sender, EventArgs e)
        {
            try
            {
                if (ReconnectButton == null) return;
                
                ReconnectButton.IsEnabled = false;
                ReconnectButton.Text = "Checking...";

                // Single check if port is available
                bool portAvailable = false;
                string comPort = "COM19";
                
                try
                {
                    var settings = Models.AppSettings.Load();
                    comPort = settings?.ComPort ?? "COM19";
                    
                    var availablePorts = Services.SerialPortService.GetAvailablePorts();
                    portAvailable = availablePorts != null && availablePorts.Length > 0 && availablePorts.Contains(comPort);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking port: {ex.GetType().Name} - {ex.Message}");
                }
                
                if (!portAvailable)
                {
                    await Services.NotificationService.ShowErrorAsync(
                        $"COM port {comPort} not found.\n\nPlease:\n1. Check device is connected\n2. Open Settings (⚙️) to configure port\n3. Click Fix Connection again");
                    
                    ReconnectButton.IsEnabled = true;
                    ReconnectButton.Text = "🔄 Fix Connection";
                    return;
                }

                ReconnectButton.Text = "Reconnecting...";

                // Try to reconnect
                if (_sliderBuilder != null)
                {
                    _sliderBuilder.Reconnect();
                }
                else
                {
                    // SliderBuilder doesn't exist - try to create it
                    try
                    {
                        _sliderBuilder = new SliderBuilder(Container, UpdateConnectionStatus);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error recreating SliderBuilder: {ex.GetType().Name} - {ex.Message}");
                        await Services.NotificationService.ShowErrorAsync($"Error: {ex.Message}");
                    }
                }
                
                // Wait a bit to see if connection succeeds
                await Task.Delay(2500);
                
                ReconnectButton.IsEnabled = true;
                ReconnectButton.Text = "🔄 Fix Connection";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnReconnectClicked: {ex.GetType().Name} - {ex.Message}");
                await Services.NotificationService.ShowErrorAsync($"Error: {ex.Message}");
                
                if (ReconnectButton != null)
                {
                    ReconnectButton.IsEnabled = true;
                    ReconnectButton.Text = "🔄 Fix Connection";
                }
            }
        }
    }
}
