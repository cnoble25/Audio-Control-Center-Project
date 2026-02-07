using System.ComponentModel;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Text.Json;
using System.Management;
using Audio_Control_Center_Application.Models;
using Audio_Control_Center_Application.Services;



namespace Audio_Control_Center_Application;

public class SliderBuilder
{
    public static SliderBuilder? Instance { get; private set; }
    public static SliderClass[]? sliders;
    public SerialPort? serialPort;
    public VerticalStackLayout[]? VerticalStackLayouts;
    public HorizontalStackLayout? Containers;
    public static string[]? Applications;
    private Action<bool>? _connectionStatusCallback;
    public AppSettings? _settings;
    private NoiseReductionFilter? _noiseFilter;
    
    // Throttling for serial data updates to reduce CPU usage
    private DateTime _lastDataUpdate = DateTime.MinValue;
    private readonly TimeSpan _dataUpdateThrottle = TimeSpan.FromMilliseconds(50); // Max 20 updates per second
    private readonly double _valueChangeThreshold = 0.5; // Only update if value changed by > 0.5%
    private double[]? _lastAudioValues; // Track last sent audio values to avoid unnecessary updates

    public SliderBuilder(HorizontalStackLayout slidersContainer, Action<bool>? connectionStatusCallback = null)
    {
        SingletonCheck();
        
        _connectionStatusCallback = connectionStatusCallback;
        _settings = AppSettings.Load();
        
        Containers = slidersContainer;
        
        // Don't enumerate audio applications on startup - do it lazily when needed
        // This prevents COMExceptions from crashing the app during initialization
        Applications = Array.Empty<string>(); // Start with empty array
        
        // Audio applications will be populated when user opens a picker or when explicitly requested
        // This avoids potential crashes from COMExceptions during app startup
        
        // Don't try to read synchronously - wait for DataReceivedHandler
        // This prevents TimeoutException crashes if device hasn't sent data yet
        sliders = new SliderClass[0];
        
        // Try to open COM port - but don't crash if it fails
        // Run on background thread to avoid blocking UI thread
        Task.Run(() =>
        {
            try
            {
        OpenCOMPort();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OpenCOMPort during initialization: {ex.GetType().Name} - {ex.Message}");
                // Continue - app will work without device connection
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _connectionStatusCallback?.Invoke(false);
                });
            }
        });
        
        // SetupSliderObjects will be called when device sends first data via DataReceivedHandler
        // For now, just set up empty UI
        Setup_SliderUI(slidersContainer);
        
    }

    public string[] FindApplications()
    {
        try
        {
        string[] apps = AudioController.GetAvailableProcesses();

            if (apps == null || apps.Length == 0)
            {
                Debug.WriteLine("No audio applications found. This is normal if no applications are currently playing audio.");
                return Array.Empty<string>();
            }

            // Filter out "Idle" and empty entries
            var filteredApps = apps
                .Where(app => !string.IsNullOrWhiteSpace(app) && !app.Equals("Idle", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToArray();

            Debug.WriteLine($"Found {filteredApps.Length} audio application(s): {JsonSerializer.Serialize(filteredApps)}");
            return filteredApps;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in FindApplications: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public void CreateDefaultSliderPaths()
    {
        try
        {
            if (sliders == null || sliders.Length == 0)
            {
                Debug.WriteLine("CreateDefaultSliderPaths: sliders is null or empty");
                return;
            }
            
            _settings = AppSettings.Load(); // Reload settings in case they changed
            
            for (int i = 0; i < sliders.Length; i++)
            {
                // Ensure slider object exists
                if (sliders[i] == null)
                {
                    sliders[i] = new SliderClass
                    {
                        Value = 0,
                        ApplicationPath = $"ApplicationPath_{i}",
                        ApplicationName = ""
                    };
                }
                
                // Use saved mapping if available
                if (_settings?.SliderToApplicationMapping != null && _settings.SliderToApplicationMapping.ContainsKey(i))
                {
                    string mappedApp = _settings.SliderToApplicationMapping[i];
                    sliders[i].ApplicationName = mappedApp == "System Volume" ? "" : mappedApp;
                }
                else if (Applications != null && i < Applications.Length && !string.IsNullOrWhiteSpace(Applications[i]))
                {
                    // Use default application from available processes
                    sliders[i].ApplicationName = Applications[i];
                }
                else
                {
                    // Default to System Volume
                    sliders[i].ApplicationName = "";
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in CreateDefaultSliderPaths: {ex.GetType().Name} - {ex.Message}");
        }
    }
    
    public void ReloadSettings()
    {
        _settings = AppSettings.Load();
    }
    
    private double InvertValueIfNeeded(double value)
    {
        // Ensure settings are loaded
        if (_settings == null)
        {
            _settings = AppSettings.Load();
        }
        
        if (_settings?.InvertSliders == true)
        {
            double inverted = 100 - value;
            Debug.WriteLine($"Inverting value: {value:F1}% -> {inverted:F1}%");
            return inverted;
        }
        return value;
    }
    
    public void ResetSliders()
    {
        try
        {
            // Unsubscribe all event handlers from existing sliders to prevent memory leaks
            if (sliders != null && sliders.Length > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        for (int i = 0; i < sliders.Length; i++)
                        {
                            if (sliders[i] != null)
                            {
                                // Unsubscribe all event handlers (Picker.Focused, Picker.SelectedIndexChanged, Slider.ValueChanged)
                                sliders[i].UnsubscribeEventHandlers();
                            }
                        }
                        Debug.WriteLine($"Unsubscribed event handlers from {sliders.Length} slider(s)");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error unsubscribing handlers in ResetSliders: {ex.GetType().Name} - {ex.Message}");
                    }
                });
            }
            
            // Clear the sliders array so they will be recreated from device data
            sliders = Array.Empty<SliderClass>();
            VerticalStackLayouts = null;
            
            // Clear UI container if it exists
            if (Containers != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        Containers.Children.Clear();
                        Debug.WriteLine("Reset sliders - cleared UI container");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error clearing container in ResetSliders: {ex.GetType().Name} - {ex.Message}");
                    }
                });
            }
            
            // Reset noise filter when sliders are reset - cleanup old states
            
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ResetSliders: {ex.GetType().Name} - {ex.Message}");
        }
    }
    
    public void RefreshApplications()
    {
        Task.Run(async () =>
        {
            try
            {
                Applications = FindApplications();
                
                // Update pickers if they exist
                if (VerticalStackLayouts != null && sliders != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        try
                        {
                            int maxLength = Math.Min(VerticalStackLayouts.Length, sliders?.Length ?? 0);
                            for (int i = 0; i < maxLength; i++)
                            {
                                // Safety checks
                                if (VerticalStackLayouts[i] == null || i >= sliders.Length || sliders[i] == null)
                                {
                                    continue;
                                }
                                
                                // Find the picker in the card content
                                if (VerticalStackLayouts[i].Children.Count > 0)
                                {
                                    if (VerticalStackLayouts[i].Children[0] is Frame cardFrame && cardFrame.Content is VerticalStackLayout cardContent)
                                    {
                                        // Find the picker (usually the 3rd element after volume label and app name)
                                        foreach (var child in cardContent.Children)
                                        {
                                            if (child is Picker picker && picker.Title == "Select Application")
                                            {
                                                try
                                                {
                                                    // Clear existing items except System Volume
                                                    var selectedItem = picker.SelectedItem?.ToString();
                                                    picker.Items.Clear();
                                                    picker.Items.Add("System Volume");
                                                    
                                                    // Add applications
                                                    if (Applications != null && Applications.Length > 0)
                                                    {
                                                        foreach (var app in Applications.Where(a => !string.IsNullOrWhiteSpace(a)))
                                                        {
                                                            if (!picker.Items.Contains(app))
                                                            {
                                                                picker.Items.Add(app);
                                                            }
                                                        }
                                                    }
                                                    
                                                    // Restore selection or default to System Volume
                                                    if (!string.IsNullOrEmpty(selectedItem) && picker.Items.Contains(selectedItem))
                                                    {
                                                        picker.SelectedItem = selectedItem;
                                                    }
                                                    else
                                                    {
                                                        picker.SelectedItem = "System Volume";
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    Debug.WriteLine($"Error updating picker {i}: {ex.GetType().Name} - {ex.Message}");
                                                }
                                                
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating pickers UI: {ex.GetType().Name} - {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing applications: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        });
    }

    public void RefreshApplicationsAndPickers()
    {
        RefreshApplications(); // Refresh the list of applications
    }

    public void Reconnect()
    {
        try
        {
            // Close existing connection
            Task.Run(() =>
            {
                try
                {
                    if (serialPort != null && serialPort.IsOpen)
                    {
                        serialPort.DataReceived -= DataReceivedHandler;
                        serialPort.Close();
                        serialPort.Dispose();
                    }
                }
                catch
                {
                    // Ignore errors when closing
                }
                finally
                {
                    serialPort = null;
                }
                
                // Reload settings and reconnect
                ReloadSettings();
                Thread.Sleep(500); // Give port time to close
                OpenCOMPort(); // Try to reconnect
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Reconnect: {ex.GetType().Name} - {ex.Message}");
        }
    }
    
    public bool IsPortAvailable()
    {
        try
        {
            _settings = AppSettings.Load();
            string comPort = _settings?.ComPort ?? "COM19";
            
            if (string.IsNullOrEmpty(comPort))
            {
                return false;
            }
            
            var availablePorts = SerialPortService.GetAvailablePorts();
            return availablePorts != null && availablePorts.Length > 0 && availablePorts.Contains(comPort);
        }
        catch
        {
            return false;
        }
    }

    public void SingletonCheck()
    {
        if (Instance != null)
        {
            Console.WriteLine("SliderBuilder instance already exists.");
            return;
        }
        Instance = this;
    }

    public bool CheckSliders(string data)
    {
        if (sliders == null || sliders.Length == 0 || string.IsNullOrEmpty(data))
        {
            return false;
        }
        
        string[] sliderValues = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (sliderValues.Length == 0)
        {
            return false;
        }
        
        if (sliders.Length != sliderValues.Length)
        {
            return false; // Mismatch in slider count
        }
        
        return true;
    }

    
    public void Setup_SliderUI(HorizontalStackLayout SlidersContainer)
    {
        try
        {
            if (sliders == null || sliders.Length == 0)
            {
                Debug.WriteLine("Sliders are not initialized or empty. Device may send data later via DataReceivedHandler.");
                // Don't show error - this is expected if device hasn't sent data yet
                return;
            }
            
            if (SlidersContainer == null)
            {
                Debug.WriteLine("SlidersContainer is null in Setup_SliderUI");
                return;
            }

            // Clear existing sliders to prevent duplicates - must be on main thread
            // Use synchronous invoke to ensure clearing completes before adding new sliders
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    int existingCount = SlidersContainer.Children.Count;
                    SlidersContainer.Children.Clear();
                    if (existingCount > 0)
                    {
                        Debug.WriteLine($"Cleared {existingCount} existing slider(s) before rebuilding");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error clearing slider container: {ex.GetType().Name} - {ex.Message}");
                }
            });
            
            // Create array with exact size
            VerticalStackLayouts = new VerticalStackLayout[sliders.Length];
            
            for (int i = 0; i < sliders.Length; i++)
            {
                // Capture index for closures
                int sliderIndex = i;
                
                // Double-check bounds on each iteration
                if (sliderIndex >= sliders.Length)
                {
                    Debug.WriteLine($"Slider index {sliderIndex} is out of bounds. Skipping.");
                    continue;
                }
                
                if (sliders[sliderIndex] == null)
                {
                    Debug.WriteLine($"Slider index {sliderIndex} is null. Creating default slider.");
                    sliders[sliderIndex] = new SliderClass
                    {
                        Value = 0,
                        ApplicationPath = $"ApplicationPath_{sliderIndex}",
                        ApplicationName = ""
                    };
                }
                
                // Create a card frame for each slider with dark mode support
            Frame cardFrame = new Frame
            {
                HasShadow = true,
                BorderColor = Application.Current.RequestedTheme == AppTheme.Light 
                    ? Color.FromArgb("#E0E0E0") 
                    : Color.FromArgb("#404040"),
                BackgroundColor = Application.Current.RequestedTheme == AppTheme.Light 
                    ? Color.FromArgb("#FFFFFF") 
                    : Color.FromArgb("#1F1F1F"),
                CornerRadius = 16,
                Padding = 20,
                Margin = new Thickness(0, 0, 0, 0),
                MinimumWidthRequest = 180,
                MaximumWidthRequest = 180,
                MinimumHeightRequest = 500  // Ensure frame is tall enough for vertical slider
            };

            // Main container for the slider card
            VerticalStackLayout cardContent = new VerticalStackLayout 
            { 
                Spacing = 16,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                MinimumHeightRequest = 500  // Ensure container has enough height for vertical slider
            };

            // Volume percentage label (above slider)
            // Display inverted value if inversion is enabled
            _settings = AppSettings.Load();
            double initialDisplayValue = _settings?.InvertSliders == true 
                ? 100 - sliders[sliderIndex].Value 
                : sliders[sliderIndex].Value;
            
            Label volumeLabel = new Label
            {
                Text = $"{initialDisplayValue:F0}%",
                FontSize = 28,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#512BD4"), // Primary color
                Margin = new Thickness(0, 0, 0, 8)
            };
            cardContent.Add(volumeLabel);
            sliders[sliderIndex].SetVolumeLabel(volumeLabel);

            // Application picker
            Picker applicationPicker = new Picker
            {
                Title = "Select Application",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                Margin = new Thickness(0, 0, 0, 8),
                BackgroundColor = Application.Current.RequestedTheme == AppTheme.Light 
                    ? Color.FromArgb("#F5F5F5") 
                    : Color.FromArgb("#2A2A2A"),
                TextColor = Application.Current.RequestedTheme == AppTheme.Light 
                    ? Color.FromArgb("#212121") 
                    : Color.FromArgb("#FFFFFF")
            };
            
            // Add "System Volume" as first option
            applicationPicker.Items.Add("System Volume");
            
            // Load applications lazily when picker is opened (avoids COMExceptions on startup)
            // Store handler reference for cleanup
            EventHandler<FocusEventArgs> pickerFocusedHandler = async (sender, e) =>
            {
                try
                {
                    // Refresh applications list when picker is focused
                    if (Applications == null || Applications.Length == 0)
                    {
                        Applications = FindApplications();
                    }
                    
                    // Add available applications if not already added
                    if (Applications != null && Applications.Length > 0)
                    {
                        foreach (var app in Applications.Where(a => !string.IsNullOrWhiteSpace(a)))
                        {
                            if (!applicationPicker.Items.Contains(app))
                            {
                                applicationPicker.Items.Add(app);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading applications for picker: {ex.GetType().Name} - {ex.Message}");
                }
            };
            applicationPicker.Focused += pickerFocusedHandler;
            // Store picker and handler in slider for cleanup
            sliders[sliderIndex].ApplicationPicker = applicationPicker;
            sliders[sliderIndex].SetPickerFocusedHandler(pickerFocusedHandler);
            
            // Add any already-loaded applications (if enumeration completed)
            if (Applications != null && Applications.Length > 0)
            {
                foreach (var app in Applications.Where(a => !string.IsNullOrWhiteSpace(a)))
                {
                    if (!applicationPicker.Items.Contains(app))
                    {
                        applicationPicker.Items.Add(app);
                    }
                }
            }
            
            // Set selected item based on saved mapping or default
            string mappedApp = (_settings?.SliderToApplicationMapping != null && _settings.SliderToApplicationMapping.ContainsKey(sliderIndex))
                ? _settings.SliderToApplicationMapping[sliderIndex] 
                : (sliders[sliderIndex].ApplicationName ?? "System Volume");
            
            if (applicationPicker.Items.Contains(mappedApp))
            {
                applicationPicker.SelectedItem = mappedApp;
            }
            else
            {
                applicationPicker.SelectedItem = "System Volume";
            }
            
            // Handle selection change - use captured sliderIndex
            // Store handler reference for cleanup
            EventHandler pickerSelectedIndexChangedHandler = async (sender, e) =>
            {
                try
                {
                    if (applicationPicker.SelectedItem != null && sliders != null && sliderIndex < sliders.Length && sliders[sliderIndex] != null)
                    {
                        string selectedApp = applicationPicker.SelectedItem.ToString();
                        sliders[sliderIndex].ApplicationName = selectedApp == "System Volume" ? "" : selectedApp;
                        
                        // Update label
                        if (sliders[sliderIndex].ApplicationNameLabel != null)
                        {
                            sliders[sliderIndex].ApplicationNameLabel.Text = selectedApp;
                        }
                        
                        // Save mapping
                        if (_settings != null)
                        {
                            if (_settings.SliderToApplicationMapping == null)
                            {
                                _settings.SliderToApplicationMapping = new Dictionary<int, string>();
                            }
                            _settings.SliderToApplicationMapping[sliderIndex] = selectedApp;
                            _settings.Save();
                        }
                        
                        await NotificationService.ShowInfoAsync($"Slider {sliderIndex + 1} mapped to {selectedApp}");
                    }
                }
                catch (IndexOutOfRangeException ex)
                {
                    Debug.WriteLine($"IndexOutOfRangeException in picker SelectedIndexChanged: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in picker SelectedIndexChanged: {ex.GetType().Name} - {ex.Message}");
                }
            };
            applicationPicker.SelectedIndexChanged += pickerSelectedIndexChangedHandler;
            sliders[sliderIndex].SetPickerSelectedIndexChangedHandler(pickerSelectedIndexChangedHandler);
            
            cardContent.Add(applicationPicker);
            
            // Application name label (hidden, kept for compatibility)
            Label applicationNameLabel = new Label
            {
                Text = mappedApp,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Application.Current.RequestedTheme == AppTheme.Light 
                    ? Color.FromArgb("#212121") 
                    : Color.FromArgb("#FFFFFF"),
                Margin = new Thickness(0, 0, 0, 8),
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation,
                IsVisible = false
            };
            cardContent.Add(applicationNameLabel);
            sliders[sliderIndex].SetApplicationNameLabel(applicationNameLabel);
            sliders[sliderIndex].ApplicationName = mappedApp == "System Volume" ? "" : mappedApp;

            // Standard MAUI Slider (vertical orientation achieved with rotation)
            // When rotated -90 degrees, width and height swap visually
            // So we set WidthRequest to the desired visual height (400) and HeightRequest to thumb width
            // Apply visual inversion if setting is enabled (invert the displayed slider position)
            _settings = AppSettings.Load();
            double sliderDisplayValue = _settings?.InvertSliders == true 
                ? 100 - sliders[sliderIndex].Value 
                : sliders[sliderIndex].Value;
            
            Slider controlledSlider = new Slider
            {
                Value = sliderDisplayValue,  // Display inverted value if setting is enabled
                WidthRequest = 400,  // This becomes the visual height after -90 rotation
                HeightRequest = 50,  // This becomes the visual width after -90 rotation (thumb width)
                Maximum = 100,
                Minimum = 0,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            
            // Rotate slider for vertical orientation
            controlledSlider.Rotation = -90;
            
            // Wrap slider in a container to ensure it's visible when rotated
            // The container needs to be large enough to accommodate the rotated slider's bounding box
            // After -90 rotation, a 400x50 slider still needs a 400x400 container (or larger) to be fully visible
            Grid sliderContainer = new Grid
            {
                WidthRequest = 400,  // Container width must accommodate rotated slider's bounding box
                HeightRequest = 400,  // Container height must accommodate rotated slider's bounding box
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 8, 0, 8)
            };
            sliderContainer.Add(controlledSlider);
            
            // Add animation to slider value changes - use captured sliderIndex and label reference
            // Capture the VolumeLabel reference to avoid null reference issues in closure
            Label? capturedVolumeLabel = sliders[sliderIndex].VolumeLabel;
            
            // Store handler reference for cleanup
            EventHandler<ValueChangedEventArgs> sliderValueChangedHandler = async (sender, e) =>
            {
                try
                {
                    // Check if captured label is still valid
                    if (capturedVolumeLabel == null)
                    {
                        Debug.WriteLine($"VolumeLabel for slider {sliderIndex} is null in ValueChanged handler");
                        return;
                    }
                    
                    // Additional safety: verify slider still exists and label is still attached
                    if (sliders != null && sliderIndex >= 0 && sliderIndex < sliders.Length && sliders[sliderIndex] != null)
                    {
                        var currentLabel = sliders[sliderIndex].VolumeLabel;
                        if (currentLabel == null || currentLabel != capturedVolumeLabel)
                        {
                            Debug.WriteLine($"VolumeLabel changed or became null for slider {sliderIndex}");
                            return;
                        }
                    }
                    
                    // Always reload settings to ensure we have the latest inversion setting
                    _settings = AppSettings.Load();
                    
                    // Display inverted value in label if inversion is enabled
                    double displayValue = _settings?.InvertSliders == true 
                        ? 100 - e.NewValue 
                        : e.NewValue;
                    
                    // Now safe to access - use captured reference
                    await capturedVolumeLabel.ScaleTo(1.2, 100, Easing.SpringOut);
                    capturedVolumeLabel.Text = $"{displayValue:F0}%";
                    await capturedVolumeLabel.ScaleTo(1.0, 100, Easing.SpringIn);
                    
                    // Update slider's stored value
                    if (sliders != null && sliderIndex >= 0 && sliderIndex < sliders.Length && sliders[sliderIndex] != null)
                    {
                        // Always reload settings to ensure we have the latest inversion setting
                        _settings = AppSettings.Load();
                        
                        // If inversion is enabled, the slider displays inverted, so we need to invert back to get actual value
                        double actualValue = _settings?.InvertSliders == true 
                            ? 100 - e.NewValue 
                            : e.NewValue;
                        
                        sliders[sliderIndex].Value = actualValue;
                        
                        // Update audio volume immediately when user manually moves slider
                        // Use the actual (non-inverted) value for audio
                        double audioVolumeValue = actualValue;
                        double audioVolume = Math.Clamp(audioVolumeValue / 100, 0.0, 1.0);
                        
                        // Get application name for this slider
                        string appName = "";
                        if (_settings?.SliderToApplicationMapping != null && _settings.SliderToApplicationMapping.ContainsKey(sliderIndex))
                        {
                            appName = _settings.SliderToApplicationMapping[sliderIndex];
                        }
                        else if (!string.IsNullOrWhiteSpace(sliders[sliderIndex].ApplicationName))
                        {
                            appName = sliders[sliderIndex].ApplicationName ?? "";
                        }
                        
                        string processName = appName == "System Volume" ? "" : appName;
                        
                        // Update audio volume for this specific slider - use optimized routing
                        AudioController.SetVolumes(new[] { processName }, new[] { audioVolume });
                    }
                }
                catch (NullReferenceException ex)
                {
                    Debug.WriteLine($"NullReferenceException in slider ValueChanged for index {sliderIndex}: {ex.Message}\nStack trace: {ex.StackTrace}");
                }
                catch (IndexOutOfRangeException ex)
                {
                    Debug.WriteLine($"IndexOutOfRangeException in slider ValueChanged: {ex.Message}\nStack trace: {ex.StackTrace}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in slider ValueChanged: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
                }
            };
            controlledSlider.ValueChanged += sliderValueChangedHandler;
            sliders[sliderIndex].SetSliderValueChangedHandler(sliderValueChangedHandler);
            
            cardContent.Add(sliderContainer);
            sliders[sliderIndex].SetControlledSlider(controlledSlider);

            // Icon button (optional - can be styled better)
            ImageButton applicationIconButton = new ImageButton
            {
                Source = "discord.png",
                HeightRequest = 48,
                WidthRequest = 48,
                CornerRadius = 24,
                BackgroundColor = Application.Current.RequestedTheme == AppTheme.Light 
                    ? Color.FromArgb("#F5F5F5") 
                    : Color.FromArgb("#2A2A2A"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 8, 0, 0),
                Opacity = 0.8
            };
            cardContent.Add(applicationIconButton);
            sliders[sliderIndex].SetApplicationIconButton(applicationIconButton);

            // Add card content to frame
            cardFrame.Content = cardContent;
            
            // Add entrance animation to card
            cardFrame.Opacity = 0;
            cardFrame.Scale = 0.8;
            
            // Store the frame in the vertical layout for reference - check bounds
            if (sliderIndex < VerticalStackLayouts.Length)
            {
                VerticalStackLayouts[sliderIndex] = new VerticalStackLayout { Padding = 0 };
                VerticalStackLayouts[sliderIndex].Add(cardFrame);
                
                if(SlidersContainer != null)
                {
                    SlidersContainer.Add(VerticalStackLayouts[sliderIndex]);
                }
                
                // Animate card entrance - use captured sliderIndex
                int delayIndex = sliderIndex;
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await Task.Delay(delayIndex * 100); // Stagger animations
                        await cardFrame.FadeTo(1, 300, Easing.CubicOut);
                        await cardFrame.ScaleTo(1, 300, Easing.SpringOut);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error animating card: {ex.GetType().Name} - {ex.Message}");
                    }
                });
            }
            else
            {
                Debug.WriteLine($"VerticalStackLayouts index {sliderIndex} is out of bounds (length: {VerticalStackLayouts.Length})");
            }
        }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Setup_SliderUI: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
        }
    }

    private void OpenCOMPort()
    {
        // Run on background thread to avoid blocking UI and handle errors gracefully
        Task.Run(() =>
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _connectionStatusCallback?.Invoke(false); // Set to disconnected initially
                });
                
                string comPort = _settings?.ComPort ?? "COM19";
                int baudRate = _settings?.BaudRate ?? 31250;
                
                if (string.IsNullOrEmpty(comPort))
                {
                    Debug.WriteLine("COM port not configured in settings");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationService.ShowInfoAsync("COM port not configured. Please check settings. App will continue without device connection.");
                        _connectionStatusCallback?.Invoke(false);
                    });
                    return;
                }
                
                // Check if port exists
                var availablePorts = SerialPortService.GetAvailablePorts();
                if (availablePorts == null || availablePorts.Length == 0 || !availablePorts.Contains(comPort))
                {
                    Debug.WriteLine($"COM port {comPort} not found in available ports list");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationService.ShowInfoAsync($"COM port {comPort} not found. App will continue. You can configure the port in Settings (⚙️ button).");
                        _connectionStatusCallback?.Invoke(false);
                    });
                    return;
                }
                
                // Close existing port if open
                try
                {
                    if (serialPort != null && serialPort.IsOpen)
                    {
            serialPort.Close();
                        serialPort.Dispose();
                        serialPort = null;
                    }
                }
                catch
                {
                    // Ignore errors when closing existing port
                    serialPort = null;
                }
                
                // First attempt to open and close (resets Arduino if needed)
                try
                {
                    using (var testPort = new SerialPort(comPort, baudRate)
                    {
                        DtrEnable = true,
                        RtsEnable = true,
                    })
                    {
                        testPort.Open();
                        Thread.Sleep(100);
                        testPort.Close();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"First connection attempt failed: {ex.GetType().Name} - {ex.Message}");
                    // Continue to second attempt - this is just a reset attempt
                }
                
                // Second attempt with proper configuration
                try
                {
                    serialPort = new SerialPort(comPort, baudRate)
                    {
                        DtrEnable = true,
                        RtsEnable = true,
                        ReadTimeout = 1000 // Increased timeout
                    };
                    
            serialPort.Open();
                    Thread.Sleep(1000); // Wait for initialization
            serialPort.DataReceived += DataReceivedHandler; 
                    
                    // Update connection status to connected
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        _connectionStatusCallback?.Invoke(true);
                        await NotificationService.ShowSuccessAsync($"Connected to {comPort} at {baudRate} baud");
                    });
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine($"Access denied to {comPort}: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationService.ShowInfoAsync($"Access denied to {comPort}. Port may be in use. App will continue without device connection.");
                        _connectionStatusCallback?.Invoke(false);
                    });
                    serialPort = null;
                }
                catch (ArgumentException ex)
                {
                    Debug.WriteLine($"Invalid COM port argument: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationService.ShowInfoAsync($"Invalid COM port: {comPort}. Please check settings. App will continue.");
                        _connectionStatusCallback?.Invoke(false);
                    });
                    serialPort = null;
                }
                catch (System.IO.IOException ex)
                {
                    Debug.WriteLine($"IO error on COM port: {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationService.ShowInfoAsync($"IO error accessing {comPort}. App will continue. Check device connection.");
                        _connectionStatusCallback?.Invoke(false);
                    });
                    serialPort = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error connecting to COM port: {ex.GetType().Name} - {ex.Message}");
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await NotificationService.ShowInfoAsync($"Could not connect to {comPort}. App will continue. Configure port in Settings (⚙️ button).");
                        _connectionStatusCallback?.Invoke(false);
                    });
                    serialPort = null;
                }
        }
        catch (Exception ex)
        {
                Debug.WriteLine($"Unexpected error in OpenCOMPort: {ex.GetType().Name} - {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _connectionStatusCallback?.Invoke(false);
                });
                serialPort = null;
            }
        });
    }

    public async void SetupSliderObjects()
    {
        if (serialPort == null || !serialPort.IsOpen)
        {
            Debug.WriteLine("Serial port is not open. Cannot read data.");
            sliders = new SliderClass[0];
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await NotificationService.ShowErrorAsync("Serial port is not connected. Cannot initialize sliders.");
            });
            return;
        }
        
        try
        {
            // Use ReadExisting or ReadLine with proper timeout handling
            string data = null;
            
            // Check if data is available first to avoid timeout
            // Don't block on ReadLine if no data - device will send data via DataReceivedHandler
            if (serialPort.BytesToRead > 0)
            {
                try
                {
                    // Read available data without blocking
                    data = serialPort.ReadExisting();
                    if (!string.IsNullOrEmpty(data))
                    {
                        // Extract first complete line if available
                        var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0)
                        {
                            data = lines[0].Trim();
                        }
                        else
                        {
                            data = data.Trim();
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Port was closed
                    Debug.WriteLine("Serial port closed during read.");
                    sliders = new SliderClass[0];
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _connectionStatusCallback?.Invoke(false);
                    });
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading from serial port: {ex.Message}");
                    data = null;
                }
            }
            else
            {
                // No data available - device will send data via DataReceivedHandler
                Debug.WriteLine("No data available from serial port yet. Will wait for async data via DataReceivedHandler.");
                sliders = new SliderClass[0];
                return;
            }
            
        if (!string.IsNullOrEmpty(data))
        {
            string[] sliderValue = data.Split('|');
            sliders = new SliderClass[sliderValue.Length];
            for (int i = 0; i < sliderValue.Length; i++)
                {
                    if (double.TryParse(sliderValue[i], out double value))
                    {
                        double rawPercentage = 100 * value / 1030;
                        sliders[i] = new SliderClass
                        {
                            Value = rawPercentage, // Store raw value - no inversion for display
                            ApplicationPath = $"ApplicationPath_{i}",
                            ApplicationName = ""
                        };
                    }
                    else
                    {
                        sliders[i] = new SliderClass
                        {
                            Value = 0,
                            ApplicationPath = $"ApplicationPath_{i}",
                            ApplicationName = ""
                        };
                    }
            }
            CreateDefaultSliderPaths();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await NotificationService.ShowSuccessAsync($"Initialized {sliders.Length} slider(s)");
                });
            }
            else
            {
                // No data received - create empty sliders array but don't show error (device may send data later)
                sliders = new SliderClass[0];
                Debug.WriteLine("No initial data from device. Will wait for data in DataReceivedHandler.");
            }
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("Timeout waiting for device response. Device may send data asynchronously.");
            sliders = new SliderClass[0];
            // Don't show error - device may send data later via DataReceivedHandler
        }
        catch (InvalidOperationException ex)
        {
            // Port was closed
            Debug.WriteLine($"Serial port operation invalid: {ex.Message}");
            sliders = new SliderClass[0];
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _connectionStatusCallback?.Invoke(false);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in SetupSliderObjects: {ex.Message}");
            sliders = new SliderClass[0];
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await NotificationService.ShowErrorAsync($"Error reading from device: {ex.Message}");
            });
        }
    }
    
    public bool CheckContainer(HorizontalStackLayout container)
    {
        if (Containers == null || sliders == null)
        {
            return false;
        }
        
        if (Containers.Children.Count != sliders.Length)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    private async void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                return;
            }
            
            string data = null;
            try
            {
                // Use ReadExisting to avoid blocking/timeout issues
                if (serialPort.BytesToRead > 0)
                {
                    data = serialPort.ReadExisting()?.Trim();
                    // Extract first complete line if multiple lines received
                    if (!string.IsNullOrEmpty(data))
                    {
                        var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0)
                        {
                            data = lines[0].Trim();
                        }
                    }
                }
                else
                {
                    return; // No data available
                }
            }
            catch (TimeoutException)
            {
                Debug.WriteLine("Timeout in DataReceivedHandler");
                return;
            }
            catch (InvalidOperationException)
            {
                // Port was closed
                Debug.WriteLine("Serial port closed during read in DataReceivedHandler");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _connectionStatusCallback?.Invoke(false);
                });
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading data in DataReceivedHandler: {ex.GetType().Name} - {ex.Message}");
                return;
            }
            
            if (string.IsNullOrEmpty(data))
            {
                return;
            }
            
            // Initialize sliders if empty (first data from device)
            if (sliders == null || sliders.Length == 0)
            {
                try
                {
                    string[] sliderValue = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (sliderValue.Length == 0)
                    {
                        Debug.WriteLine("No slider values found in data");
                        return;
                    }
                    
                    sliders = new SliderClass[sliderValue.Length];
                    for (int i = 0; i < sliderValue.Length; i++)
                    {
                        if (double.TryParse(sliderValue[i], out double value))
                        {
                            double rawPercentage = 100 * value / 1030;
                            sliders[i] = new SliderClass
                            {
                                Value = rawPercentage, // Store raw value - no inversion for display
                                ApplicationPath = $"ApplicationPath_{i}",
                                ApplicationName = ""
                            };
                        }
                        else
                        {
                            sliders[i] = new SliderClass
                            {
                                Value = 0,
                                ApplicationPath = $"ApplicationPath_{i}",
                                ApplicationName = ""
                            };
                        }
                    }
                    CreateDefaultSliderPaths();
                    
                    // Now create the UI
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            if (Containers != null)
                            {
                                Setup_SliderUI(Containers);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error setting up slider UI: {ex.GetType().Name} - {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error initializing sliders from data: {ex.GetType().Name} - {ex.Message}");
                    sliders = null;
                }
            }
            
            // Validate slider count matches
            if (!CheckSliders(data))
            {
                try
                {
                    Debug.WriteLine($"Slider count mismatch: expected {sliders?.Length ?? 0}, got {data.Split('|', StringSplitOptions.RemoveEmptyEntries).Length}");
                    // Reinitialize sliders
                    string[] sliderValue = data.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (sliderValue.Length > 0)
                    {
                        sliders = new SliderClass[sliderValue.Length];
                        for (int i = 0; i < sliderValue.Length; i++)
                        {
                            if (double.TryParse(sliderValue[i], out double value))
                            {
                                double rawPercentage = 100 * value / 1030;
                                double displayValue = InvertValueIfNeeded(rawPercentage);
                                sliders[i] = new SliderClass
                                {
                                    Value = displayValue,
                                    ApplicationPath = $"ApplicationPath_{i}",
                                    ApplicationName = ""
                                };
                            }
                            else
                            {
                                sliders[i] = new SliderClass
                                {
                                    Value = 0,
                                    ApplicationPath = $"ApplicationPath_{i}",
                                    ApplicationName = ""
                                };
                            }
                        }
                        CreateDefaultSliderPaths();
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                if (Containers != null)
                                {
                                    Containers.Children.Clear();
                                    Setup_SliderUI(Containers);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error recreating slider UI: {ex.GetType().Name} - {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reinitializing sliders: {ex.GetType().Name} - {ex.Message}");
                }
                return;
            }
            
            // Safety check before processing
            if (sliders == null || sliders.Length == 0)
            {
                Debug.WriteLine("Sliders are null or empty when trying to process data");
                return;
            }

            double[] sliderValues = ConvertStringToDoubleArray(data);
            
            // Throttle updates to reduce CPU usage - skip if too soon since last update
            var now = DateTime.UtcNow;
            var timeSinceLastUpdate = now - _lastDataUpdate;
            if (timeSinceLastUpdate < _dataUpdateThrottle)
            {
                return; // Skip this update - too frequent
            }
            _lastDataUpdate = now;
            
            Debug.WriteLine($"Received data: {data}");
           
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // Safety checks
                    if (sliders == null || sliders.Length == 0)
                    {
                        Debug.WriteLine("Cannot process data: sliders is null or empty");
                        return;
                    }
                    
                    if (sliderValues == null || sliderValues.Length == 0)
                    {
                        Debug.WriteLine("Cannot process data: sliderValues is null or empty");
                        return;
                    }
                    
                    // Update slider values (without animation for serial updates to avoid lag)
                    // Use the minimum of both lengths to avoid index out of bounds
                    int maxIndex = Math.Min(sliderValues.Length, sliders.Length);
                    for (int i = 0; i < maxIndex; i++)
                    {
                        if (sliders[i] == null)
                        {
                            sliders[i] = new SliderClass
                            {
                                Value = 0,
                                ApplicationPath = $"ApplicationPath_{i}",
                                ApplicationName = ""
                            };
                        }
                        
                        double rawPercentage = 100 * sliderValues[i] / 1030;
                        
                        // Apply noise reduction filter to smooth noisy input
                        double filteredPercentage = _noiseFilter?.Filter(i, rawPercentage) ?? rawPercentage;
                        
                        // Only update UI if value changed significantly to reduce CPU usage
                        double currentValue = sliders[i].Value;
                        double valueChange = Math.Abs(filteredPercentage - currentValue);
                        
                        if (valueChange > _valueChangeThreshold)
                        {
                            // Store filtered value - don't invert for display
                            sliders[i].SetValue(filteredPercentage, animate: false); // Don't animate serial updates
                            
                            // Update volume label with inverted display value if inversion is enabled
                            if (sliders[i].VolumeLabel != null)
                            {
                                _settings = AppSettings.Load();
                                double displayValue = _settings?.InvertSliders == true 
                                    ? 100 - filteredPercentage 
                                    : filteredPercentage;
                                
                                sliders[i].VolumeLabel.Text = $"{displayValue:F0}%";
                            }
                        }
                    }

                    // Prepare volume updates - only send if values changed significantly
                    string[] processNames = new string[sliders.Length];
                    double[] volumes = new double[sliders.Length];
                    bool hasSignificantChange = false;
                    
                    // Initialize last audio values array if needed
                    if (_lastAudioValues == null || _lastAudioValues.Length != sliders.Length)
                    {
                        _lastAudioValues = new double[sliders.Length];
                        Array.Fill(_lastAudioValues, -1); // Initialize to -1 to force first update
                    }
                    
                    for (int i = 0; i < sliders.Length; i++)
                    {
                        // Ensure slider exists
                        if (sliders[i] == null)
                        {
                            sliders[i] = new SliderClass
                            {
                                Value = 0,
                                ApplicationPath = $"ApplicationPath_{i}",
                                ApplicationName = ""
                            };
                        }
                        
                        // Use saved mapping if available, otherwise use slider's application name
                        string appName = "";
                        if (_settings?.SliderToApplicationMapping != null && _settings.SliderToApplicationMapping.ContainsKey(i))
                        {
                            appName = _settings.SliderToApplicationMapping[i];
                        }
                        else if (!string.IsNullOrWhiteSpace(sliders[i].ApplicationName))
                        {
                            appName = sliders[i].ApplicationName ?? "";
                        }
                        
                        // "System Volume" means empty string for master volume control
                        processNames[i] = appName == "System Volume" ? "" : appName;
                        
                        // Get slider value (actual value - never inverted for audio)
                        // The Value property stores the actual value, inversion only affects UI display
                        double sliderValue = sliders[i].Value;
                        double audioVolumeScalar = Math.Clamp(sliderValue / 100, 0.0, 1.0);
                        
                        // Only update audio if value changed significantly (reduces CPU usage)
                        double lastAudioValue = _lastAudioValues[i];
                        if (lastAudioValue < 0 || Math.Abs(audioVolumeScalar - lastAudioValue) > (_valueChangeThreshold / 100.0))
                        {
                            volumes[i] = audioVolumeScalar;
                            _lastAudioValues[i] = audioVolumeScalar;
                            hasSignificantChange = true;
                        }
                        else
                        {
                            // Value hasn't changed enough - skip this slider
                            volumes[i] = -1; // Mark as skip
                        }
                    }
                    
                    // Only apply volume changes if there were significant changes (reduces CPU usage)
                    if (hasSignificantChange)
                    {
                        // Filter out skipped sliders (-1 values)
                        var validProcessNames = new List<string>();
                        var validVolumes = new List<double>();
                        
                        for (int i = 0; i < processNames.Length; i++)
                        {
                            if (volumes[i] >= 0) // Only include sliders with significant changes
                            {
                                validProcessNames.Add(processNames[i]);
                                validVolumes.Add(volumes[i]);
                            }
                        }
                        
                        if (validVolumes.Count > 0)
                        {
                            // Apply volume changes - use optimized routing function
                            AudioController.SetVolumes(validProcessNames.ToArray(), validVolumes.ToArray());
                        }
                    }
                }
                catch (IndexOutOfRangeException ex)
                {
                    Debug.WriteLine($"IndexOutOfRangeException in DataReceivedHandler: {ex.Message}\nStack trace: {ex.StackTrace}");
                    await NotificationService.ShowErrorAsync($"Array bounds error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing received data: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
                    await NotificationService.ShowErrorAsync($"Error processing data: {ex.Message}");
                }
            });
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("Timeout reading from serial port");
        }
        catch (InvalidOperationException)
        {
            // Port was closed
            Debug.WriteLine("Serial port was closed");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _connectionStatusCallback?.Invoke(false);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading data: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await NotificationService.ShowErrorAsync($"Communication error: {ex.Message}");
                _connectionStatusCallback?.Invoke(false);
            });
        }
    }

    private static double[] ConvertStringToDoubleArray(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Debug.WriteLine("Input string is empty or null.");
            return Array.Empty<double>();
        }

        try
        {
            return input
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(s =>
                {
                    if (double.TryParse(s.Trim(), out var value))
                    {
                        return value;
                    }
                    else
                    {
                        Debug.WriteLine($"Invalid value in input: {s}");
                        return 0; // Default to 0 for invalid values
                    }
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error converting string to double array: {ex.Message}");
            return Array.Empty<double>();
        }
    }
    
    
}

