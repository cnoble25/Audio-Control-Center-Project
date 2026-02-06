namespace Audio_Control_Center_Application;
using System;
using System.Diagnostics; // Required for Process.GetProcessById
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CSCore.CoreAudioAPI; // Required for Core Audio APIs from CSCore library
//bruh
public static class AudioController
{
    private static readonly object audioControlLock = new object();
    private static MMDevice? cachedDevice = null;
    private static AudioEndpointVolume? cachedEndpointVolume = null;
    private static DateTime lastSystemVolumeUpdate = DateTime.MinValue;
    private static double lastSystemVolumeValue = -1.0;
    private static readonly TimeSpan SystemVolumeUpdateThrottle = TimeSpan.FromMilliseconds(50); // Throttle to max 20 updates per second
    private static readonly double SystemVolumeChangeThreshold = 0.01; // Only update if change is > 1%
    private static DateTime lastCacheCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CacheCleanupInterval = TimeSpan.FromMinutes(5); // Clean up cache every 5 minutes
    
    /// <summary>
    /// Cleanup cached audio objects to free memory.
    /// Should be called periodically or when application is shutting down.
    /// </summary>
    public static void CleanupCache()
    {
        lock (audioControlLock)
        {
            try
            {
                cachedEndpointVolume?.Dispose();
                cachedDevice?.Dispose();
            }
            catch { }
            finally
            {
                cachedDevice = null;
                cachedEndpointVolume = null;
            }
        }
    }
    
    /// <summary>
    /// Periodically cleanup cache if it's been a while since last cleanup.
    /// </summary>
    private static void CleanupCacheIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - lastCacheCleanup > CacheCleanupInterval)
        {
            CleanupCache();
            lastCacheCleanup = now;
        }
    }
    /// <summary>
    /// Sets the master volume level for a specific application's audio session.
    /// This directly affects the volume slider for that application in the Windows Volume Mixer.
    /// </summary>
    /// <param name="processName">The name of the process (e.g., "chrome", "spotify", "vlc"). Case-insensitive.</param>
    /// <param name="volumeScalar">The desired volume level as a scalar (float) between 0.0f (mute) and 1.0f (max volume).</param>
    
    /// <summary>
    /// Sets the system/master volume level. Fast and optimized - no session enumeration.
    /// Runs on dedicated thread without blocking others.
    /// </summary>
    public static void SetSystemVolume(double volumeScalar)
    {
        if (volumeScalar < 0.0 || volumeScalar > 1.0)
        {
            Console.WriteLine($"Invalid volume scalar: {volumeScalar}. Must be between 0.0 and 1.0.");
            return;
        }

        // Use thread pool with proper thread management
        Task.Run(() =>
        {
            lock (audioControlLock)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = now - lastSystemVolumeUpdate;
                    var volumeChanged = Math.Abs(volumeScalar - lastSystemVolumeValue) > SystemVolumeChangeThreshold;

                    // Periodic cache cleanup
                    CleanupCacheIfNeeded();
                    
                    // Throttle and change detection
                    if (volumeChanged && (timeSinceLastUpdate >= SystemVolumeUpdateThrottle || lastSystemVolumeValue < 0))
                    {
                        // Get or cache the device and endpoint volume
                        if (cachedDevice == null || cachedEndpointVolume == null)
                        {
                            try
                            {
                                cachedDevice?.Dispose();
                                cachedEndpointVolume?.Dispose();
                            }
                            catch { }

                            var enumerator = new MMDeviceEnumerator();
                            cachedDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                            if (cachedDevice != null)
                            {
                                cachedEndpointVolume = AudioEndpointVolume.FromDevice(cachedDevice);
                            }
                            else
                            {
                                Console.WriteLine("Could not get default audio endpoint for system volume.");
                                return;
                            }
                        }

                        if (cachedEndpointVolume != null)
                        {
                            cachedEndpointVolume.MasterVolumeLevelScalar = Convert.ToSingle(volumeScalar);
                            lastSystemVolumeValue = volumeScalar;
                            lastSystemVolumeUpdate = now;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Reset cache on error
                    try
                    {
                        cachedEndpointVolume?.Dispose();
                        cachedDevice?.Dispose();
                    }
                    catch { }
                    cachedDevice = null;
                    cachedEndpointVolume = null;
                    Console.WriteLine($"Error setting system volume: {ex.Message}");
                }
            }
        });
    }

    /// <summary>
    /// Sets volume for a single application. Executes synchronously - called from Parallel.ForEach which manages threading.
    /// No Task.Run here to avoid nested threads - Parallel.ForEach handles thread allocation.
    /// </summary>
    private static void SetSingleApplicationVolume(string processName, double volumeScalar)
    {
        if (string.IsNullOrWhiteSpace(processName) || volumeScalar < 0.0 || volumeScalar > 1.0)
        {
            return;
        }

        try
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    if (device == null)
                    {
                        return;
                    }

                    using (var sessionManager = AudioSessionManager2.FromMMDevice(device))
                    {
                        if (sessionManager == null)
                        {
                            return;
                        }

                        using (var sessionEnumerator = sessionManager.GetSessionEnumerator())
                        {
                            if (sessionEnumerator == null)
                            {
                                return;
                            }

                            foreach (var session in sessionEnumerator)
                            {
                                using (session)
                                {
                                    try
                                    {
                                        var sessionControl2 = session.QueryInterface<AudioSessionControl2>();
                                        if (sessionControl2 != null)
                                        {
                                            var processId = sessionControl2.ProcessID;
                                            Process? process = null;

                                            try
                                            {
                                                process = Process.GetProcessById((int)processId);
                                            }
                                            catch (ArgumentException)
                                            {
                                                continue; // Skip invalid or terminated processes
                                            }

                                            if (process != null && process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                using (var simpleVolume = session.QueryInterface<SimpleAudioVolume>())
                                                {
                                                    if (simpleVolume != null)
                                                    {
                                                        simpleVolume.MasterVolume = Convert.ToSingle(volumeScalar);
                                                        return; // Found and updated, exit early
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Ignore individual session errors
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting volume for application '{processName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Sets volume levels for specific applications. Optimized for parallel execution - one thread per application.
    /// Uses proper thread limits (e.g., 3 threads for 3 applications, not 50).
    /// </summary>
    public static void SetApplicationVolumes(string[] processNames, double[] volumeScalars)
    {
        if (processNames == null || volumeScalars == null)
        {
            Console.WriteLine("Process names or volume scalars cannot be null.");
            return;
        }

        if (processNames.Length != volumeScalars.Length)
        {
            Console.WriteLine("The length of processNames and volumeScalars must match.");
            return;
        }

        // Validate and filter out empty/whitespace (system volume should use SetSystemVolume instead)
        var appUpdates = new List<(string processName, double volume)>();
        for (int i = 0; i < processNames.Length && i < volumeScalars.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(processNames[i]))
            {
                if (volumeScalars[i] < 0.0 || volumeScalars[i] > 1.0)
                {
                    Console.WriteLine($"Invalid volume scalar at index {i}: {volumeScalars[i]}. Must be between 0.0 and 1.0.");
                    continue;
                }
                appUpdates.Add((processNames[i], volumeScalars[i]));
            }
        }

        if (appUpdates.Count == 0)
        {
            return; // No application volumes to set
        }

        // Process in background thread to avoid blocking UI
        // Process sequentially to avoid COM threading issues - multiple threads creating COM objects can deadlock
        Task.Run(() =>
        {
            // Process each application update sequentially in background thread (fast, no blocking)
            foreach (var update in appUpdates)
            {
                SetSingleApplicationVolume(update.processName, update.volume);
            }
        });
    }

    /// <summary>
    /// Sets volumes for both system and applications. Routes to appropriate optimized functions.
    /// All operations are non-blocking and run in background threads.
    /// System volume runs on one dedicated thread, application volumes run in parallel (one thread per application).
    /// Uses proper thread limits: 3 threads for 3 applications, not 50 threads.
    /// </summary>
    public static void SetVolumes(string[] processNames, double[] volumeScalars)
    {
        if (processNames == null || volumeScalars == null || processNames.Length != volumeScalars.Length)
        {
            Console.WriteLine("Invalid parameters for SetVolumes.");
            return;
        }

        // Separate system and application volumes
        double? systemVolume = null;
        var appNames = new List<string>();
        var appVolumes = new List<double>();

        for (int i = 0; i < processNames.Length && i < volumeScalars.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(processNames[i]))
            {
                systemVolume = volumeScalars[i];
            }
            else
            {
                appNames.Add(processNames[i]);
                appVolumes.Add(volumeScalars[i]);
            }
        }

        // Set system volume if needed (non-blocking, runs in background)
        if (systemVolume.HasValue)
        {
            SetSystemVolume(systemVolume.Value);
        }

        // Set application volumes if any (non-blocking, runs in background)
        if (appNames.Count > 0)
        {
            SetApplicationVolumes(appNames.ToArray(), appVolumes.ToArray());
        }
    }
    
    public static void SetApplicationVolume(string processName, double volumeScalar)
{
    if(processName == null)
    {
        return;
    }
    Task.Run(() =>
    {
        lock (audioControlLock)
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        using (var endpointVolume = AudioEndpointVolume.FromDevice(device))
                        {
                            endpointVolume.MasterVolumeLevelScalar = Convert.ToSingle(volumeScalar);
                        }
                    }
                    else
                    {
                        using (var sessionManager = AudioSessionManager2.FromMMDevice(device))
                        {
                            using (var sessionEnumerator = sessionManager.GetSessionEnumerator())
                            {
                                foreach (var session in sessionEnumerator)
                                {
                                    using (session)
                                    {
                                        try
                                        {
                                            var sessionControl2 = session.QueryInterface<AudioSessionControl2>();
                                            if (sessionControl2 != null)
                                            {
                                                var processId = sessionControl2.ProcessID;
                                                Process process = null;

                                                try
                                                {
                                                    process = Process.GetProcessById((int)processId);
                                                }
                                                catch (ArgumentException)
                                                {
                                                    continue;
                                                }

                                                if (process != null && process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    using (var simpleVolume = session.QueryInterface<SimpleAudioVolume>())
                                                    {
                                                        if (simpleVolume != null)
                                                        {
                                                            simpleVolume.MasterVolume = Convert.ToSingle(volumeScalar);
                                                            return;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Error controlling volume for process: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                        Console.WriteLine($"Application '{processName}' not found or its volume could not be controlled.");
                    }
                }
            }
        }
    });
    }
    public static string[] GetAvailableProcesses()
        {
            string[] result = Array.Empty<string>();

            // Create a new thread to run the method in an MTA thread
            // Wrap entire thread body in try-catch to prevent any unhandled exceptions
            var thread = new Thread(() =>
            {
                try
                {
                    var processList = new List<string>();

                    try
                    {
                        MMDeviceEnumerator enumerator = null;
                        MMDevice device = null;
                        AudioSessionManager2 sessionManager = null;
                        AudioSessionEnumerator sessionEnumerator = null;
                        
                        try
                        {
                            enumerator = new MMDeviceEnumerator();
                            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                            
                            if (device == null)
                            {
                                Debug.WriteLine("No default audio endpoint found");
                                result = Array.Empty<string>();
                                return;
                            }
                            
                            sessionManager = AudioSessionManager2.FromMMDevice(device);
                            
                            if (sessionManager == null)
                            {
                                Debug.WriteLine("Could not get session manager");
                                result = Array.Empty<string>();
                                return;
                            }
                            
                            sessionEnumerator = sessionManager.GetSessionEnumerator();
                            
                            if (sessionEnumerator == null)
                            {
                                Debug.WriteLine("Could not get session enumerator");
                                result = Array.Empty<string>();
                                return;
                            }
                            
                            foreach (var session in sessionEnumerator)
                            {
                                try
                                {
                                    using (session)
                                    {
                                        try
                                        {
                                            var sessionControl2 = session.QueryInterface<AudioSessionControl2>();
                                            if (sessionControl2 != null)
                                            {
                                                var processId = sessionControl2.ProcessID;
                                                Process process = null;

                                                try
                                                {
                                                    process = Process.GetProcessById((int)processId);
                                                }
                                                catch (ArgumentException)
                                                {
                                                    continue; // Skip invalid or terminated processes
                                                }
                                                catch (InvalidOperationException)
                                                {
                                                    continue; // Process may have exited
                                                }

                                                if (process != null && !string.IsNullOrWhiteSpace(process.ProcessName))
                                                {
                                                    processList.Add(process.ProcessName);
                                                }
                                            }
                                        }
                                        catch (System.Runtime.InteropServices.COMException)
                                        {
                                            // Ignore COM exceptions for individual sessions (session may be invalid)
                                            continue;
                                        }
                                        catch (Exception)
                                        {
                                            // Ignore other errors for individual sessions
                                            continue;
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignore errors when disposing session
                                    continue;
                                }
                            }
                        }
                        finally
                        {
                            // Dispose resources in reverse order
                            try { sessionEnumerator?.Dispose(); } catch { }
                            try { sessionManager?.Dispose(); } catch { }
                            try { device?.Dispose(); } catch { }
                            try { enumerator?.Dispose(); } catch { }
                        }

                        result = processList.Distinct().ToArray(); // Remove duplicates and store the result
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        // Audio system may not be ready or no audio endpoint available
                        Debug.WriteLine($"COM Exception in GetAvailableProcesses inner: {ex.Message}");
                        result = Array.Empty<string>();
                    }
                    catch (Exception ex)
                    {
                        // Handle any other errors gracefully
                        Debug.WriteLine($"Error in GetAvailableProcesses inner: {ex.GetType().Name} - {ex.Message}");
                        result = Array.Empty<string>();
                    }
                }
                catch (Exception ex)
                {
                    // Catch any exception that might escape the inner try-catch
                    Debug.WriteLine($"Critical error in GetAvailableProcesses thread: {ex.GetType().Name} - {ex.Message}");
                    result = Array.Empty<string>();
                }
            });

            // Set the thread to MTA
            try
            {
                thread.SetApartmentState(ApartmentState.MTA);
                thread.IsBackground = true; // Make it a background thread so it doesn't prevent app shutdown
                thread.Start();
                
                // Use timeout to prevent hanging - max 2 seconds
                if (!thread.Join(TimeSpan.FromSeconds(2)))
                {
                    Debug.WriteLine("Timeout waiting for GetAvailableProcesses thread (2s). Returning empty array - this is normal.");
                    return Array.Empty<string>();
                }
            }
            catch (ThreadStateException ex)
            {
                Debug.WriteLine($"Thread state error in GetAvailableProcesses: {ex.Message}");
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetAvailableProcesses thread handling: {ex.GetType().Name} - {ex.Message}");
                return Array.Empty<string>();
            }

            return result ?? Array.Empty<string>();
        }
        
}

   

