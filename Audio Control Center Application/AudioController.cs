namespace Audio_Control_Center_Application;
using System;
using System.Diagnostics; // Required for Process.GetProcessById
using CSCore.CoreAudioAPI; // Required for Core Audio APIs from CSCore library

public static class AudioController
{
    /// <summary>
    /// Sets the master volume level for a specific application's audio session.
    /// This directly affects the volume slider for that application in the Windows Volume Mixer.
    /// </summary>
    /// <param name="processName">The name of the process (e.g., "chrome", "spotify", "vlc"). Case-insensitive.</param>
    /// <param name="volumeScalar">The desired volume level as a scalar (float) between 0.0f (mute) and 1.0f (max volume).</param>
    public static void SetApplicationVolume(string processName, double volumeScalar)
        {
            // Run the method on an MTA thread
            var thread = new Thread(() =>
            {
                // Clamp the volumeScalar to ensure it's within the valid range [0.0f, 1.0f]
                volumeScalar = Math.Clamp(volumeScalar, 0.0f, 1.0f);

                // Create an MMDeviceEnumerator to enumerate audio devices
                using (var enumerator = new MMDeviceEnumerator())
                {
                    // Get the default audio rendering (playback) endpoint device
                    using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                    {
                        if (string.IsNullOrWhiteSpace(processName))
                        {
                            // Control the system's master volume
                            using (var endpointVolume = AudioEndpointVolume.FromDevice(device))
                            {
                                endpointVolume.MasterVolumeLevelScalar = Convert.ToSingle(volumeScalar);
                                Console.WriteLine($"System master volume set to {volumeScalar * 100:F0}%.");
                            }
                        }
                        else
                        {
                            // Get the AudioSessionManager2 for the selected device
                            using (var sessionManager = AudioSessionManager2.FromMMDevice(device))
                            {
                                // Get an enumerator for all active audio sessions
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
                                                                Console.WriteLine($"Successfully set volume for '{process.ProcessName}' (PID: {processId}) to {volumeScalar * 100:F0}%.");
                                                                return;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                // Log any errors
                                            }
                                        }
                                    }
                                }
                            }
                            Console.WriteLine($"Application '{processName}' not found or its volume could not be controlled.");
                        }
                    }
                }
            });

            // Set the thread to MTA
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
    }
    public static string[] GetAvailableProcesses()
        {
            string[] result = null;

            // Create a new thread to run the method in an MTA thread
            var thread = new Thread(() =>
            {
                var processList = new List<string>();

                using (var enumerator = new MMDeviceEnumerator())
                {
                    using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
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
                                                    continue; // Skip invalid or terminated processes
                                                }

                                                if (process != null && !string.IsNullOrWhiteSpace(process.ProcessName))
                                                {
                                                    processList.Add(process.ProcessName);
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // Ignore errors for individual sessions
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                result = processList.Distinct().ToArray(); // Remove duplicates and store the result
            });

            // Set the thread to MTA
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            thread.Join(); // Wait for the thread to complete

            return result;
        }
        
}

   

