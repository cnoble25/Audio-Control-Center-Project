using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Audio_Control_Center_Application.Models
{
    public class AppSettings
    {
        public string ComPort { get; set; } = "COM19";
        public int BaudRate { get; set; } = 31250;
        public bool InvertSliders { get; set; } = false;
        public Dictionary<int, string> SliderToApplicationMapping { get; set; } = new();
        
        private static readonly string SettingsFilePath = Path.Combine(
            FileSystem.AppDataDirectory, 
            "appsettings.json");
        
        private static readonly object saveLock = new object();
        private static CancellationTokenSource? saveCancellationTokenSource = null;
        private static DateTime lastSaveTime = DateTime.MinValue;
        private static readonly TimeSpan SaveThrottleDelay = TimeSpan.FromMilliseconds(500); // Wait 500ms before saving
        
        private static readonly JsonSerializerOptions saveOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            }
            
            return new AppSettings();
        }

        public void Save()
        {
            SaveAsync(); // Fire and forget - don't block
        }
        
        public void SaveImmediate()
        {
            lock (saveLock)
            {
                SaveInternalUnlocked();
            }
        }
        
        private void SaveInternalUnlocked()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, saveOptions);
                File.WriteAllText(SettingsFilePath, json);
                lastSaveTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        
        private void SaveAsync()
        {
            lock (saveLock)
            {
                // Cancel any pending save
                saveCancellationTokenSource?.Cancel();
                saveCancellationTokenSource?.Dispose();
                
                // Create new cancellation token
                saveCancellationTokenSource = new CancellationTokenSource();
                var token = saveCancellationTokenSource.Token;
                
                // Check if we should save immediately or throttle
                var timeSinceLastSave = DateTime.UtcNow - lastSaveTime;
                
                Task.Run(async () =>
                {
                    try
                    {
                        // If enough time has passed since last save, save immediately
                        if (timeSinceLastSave >= SaveThrottleDelay)
                        {
                            if (!token.IsCancellationRequested)
                            {
                                lock (saveLock)
                                {
                                    if (!token.IsCancellationRequested)
                                    {
                                        SaveInternalUnlocked();
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Wait for the throttle delay, then save
                            var delay = SaveThrottleDelay - timeSinceLastSave;
                            await Task.Delay(delay, token);
                            
                            if (!token.IsCancellationRequested)
                            {
                                lock (saveLock)
                                {
                                    if (!token.IsCancellationRequested)
                                    {
                                        SaveInternalUnlocked();
                                    }
                                }
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Save was cancelled - this is expected when multiple saves happen quickly
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in async save: {ex.Message}");
                    }
                }, token);
            }
        }
    }
}
