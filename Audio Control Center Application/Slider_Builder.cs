namespace Audio_Control_Center_Application;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Text.Json;


public class Slider_Builder
{
    public static Slider[] sliders;
    public SerialPort serialPort;
    
    public Slider_Builder()
    {
        string temp_data = COM_Port_Communication();
        if (temp_data != "")
        {
            string[] slider_values = temp_data.Split('|');
            sliders = new Slider[slider_values.Length];
            for (int i = 0; i < slider_values.Length; i++)
            {
                    sliders[i] = new Slider();
                    sliders[i].Value = double.Parse(slider_values[i]);
                    sliders[i].ApplicationPath = $"ApplicationPath_{i}"; // Placeholder for application path
                    sliders[i].ApplicationName = $"ApplicationName_{i}"; // Placeholder for application name
                
            }
        }
        else
        {
            sliders = new Slider[0]; // No data received, initialize with empty array
        }
        
        
    }
    
    private string COM_Port_Communication()
        {
               serialPort = new SerialPort("COM9", 9600);
                serialPort.Open();
                Thread.Sleep(2000); // Wait for the serial port to initialize
                serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
               return serialPort.ReadLine(); // Read a line from the serial port and trim whitespace
        }
    
     private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
     {
         try
         {
             string data = serialPort.ReadLine();
             double[] sliderValues = ConvertStringToDoubleArray(data);
             Debug.WriteLine(data);
             MainThread.BeginInvokeOnMainThread(() =>
             {
                 
                for (int i = 0; i < sliderValues.Length && i < sliders.Length; i++)
                {
                    sliders[i].SetValue(sliderValues[i]);
                }
                 
    
                 
                
    
             });
         }
         catch (Exception ex)
         {
             Debug.WriteLine($"Error reading data: {ex.Message}");
         }
    
    
     }
    
    public static double[] ConvertStringToDoubleArray(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new double[sliders.Length];
    
        // Split the string by '|' and convert each part to int
        return input
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.TryParse(s, out var n) ? n : 0)
            .ToArray();
    }
}