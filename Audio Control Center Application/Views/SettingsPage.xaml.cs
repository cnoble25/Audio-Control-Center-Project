using Audio_Control_Center_Application.Models;
using Audio_Control_Center_Application.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Audio_Control_Center_Application.Views
{
    public partial class SettingsPage : ContentPage, INotifyPropertyChanged
    {
        private AppSettings _settings;
        private string? _selectedComPort;
        private int _selectedBaudRate;
        private bool _invertSliders;

        public SettingsPage()
        {
            try
            {
                InitializeComponent();
                BindingContext = this;
                
                _settings = AppSettings.Load();
                
                LoadAvailablePorts();
                LoadAvailableBaudRates();
                
                SelectedComPort = _settings.ComPort;
                SelectedBaudRate = _settings.BaudRate;
                InvertSliders = _settings.InvertSliders;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing SettingsPage: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
                // Create a simple fallback UI if initialization fails
                Content = new Label { Text = $"Error loading settings: {ex.Message}", Padding = 20 };
            }
        }

        private ObservableCollection<string> _availablePorts = new();
        public ObservableCollection<string> AvailablePorts
        {
            get => _availablePorts;
            set
            {
                _availablePorts = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<int> _availableBaudRates = new();
        public ObservableCollection<int> AvailableBaudRates
        {
            get => _availableBaudRates;
            set
            {
                _availableBaudRates = value;
                OnPropertyChanged();
            }
        }

        public string? SelectedComPort
        {
            get => _selectedComPort;
            set
            {
                _selectedComPort = value;
                OnPropertyChanged();
            }
        }

        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set
            {
                _selectedBaudRate = value;
                OnPropertyChanged();
            }
        }

        public bool InvertSliders
        {
            get => _invertSliders;
            set
            {
                _invertSliders = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshPortsCommand => new Command(async () =>
        {
            LoadAvailablePorts();
            await NotificationService.ShowInfoAsync("COM ports refreshed");
        });

        public ICommand SaveSettingsCommand => new Command(async () =>
        {
            try
            {
                _settings.ComPort = SelectedComPort ?? "COM19";
                _settings.BaudRate = SelectedBaudRate;
                _settings.InvertSliders = InvertSliders;
                _settings.SaveImmediate(); // Save immediately when user explicitly clicks save
                
                await NotificationService.ShowSuccessAsync("Settings saved successfully!");
                await Task.Delay(500);
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await NotificationService.ShowErrorAsync($"Failed to save settings: {ex.Message}");
            }
        });

        private void LoadAvailablePorts()
        {
            try
            {
                var ports = SerialPortService.GetAvailablePorts();
                AvailablePorts.Clear();
                foreach (var port in ports)
                {
                    AvailablePorts.Add(port);
                }
                
                // If the saved port is not in the list, add it
                if (!string.IsNullOrEmpty(_settings?.ComPort) && !AvailablePorts.Contains(_settings.ComPort))
                {
                    AvailablePorts.Add(_settings.ComPort);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ports: {ex.GetType().Name} - {ex.Message}");
                // Initialize with empty collection if error
                AvailablePorts.Clear();
            }
        }

        private void LoadAvailableBaudRates()
        {
            try
            {
                var baudRates = SerialPortService.GetCommonBaudRates();
                AvailableBaudRates.Clear();
                foreach (var rate in baudRates)
                {
                    AvailableBaudRates.Add(rate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading baud rates: {ex.GetType().Name} - {ex.Message}");
                // Initialize with empty collection if error
                AvailableBaudRates.Clear();
            }
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected new virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
