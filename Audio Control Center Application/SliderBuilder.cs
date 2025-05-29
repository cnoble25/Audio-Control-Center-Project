using System.ComponentModel;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Text.Json;
using Syncfusion.Maui.Sliders;


namespace Audio_Control_Center_Application;

public class SliderBuilder
{
    public static SliderBuilder Instance { get; private set; }
    public static SliderClass[] sliders;
    public SerialPort serialPort;
    public VerticalStackLayout[] VerticalStackLayouts;
    public HorizontalStackLayout Containers;

    public SliderBuilder(HorizontalStackLayout slidersContainer)
    {
        SingletonCheck();
        
        Containers = slidersContainer;
        
        OpenCOMPort();
        
        SetupSliderObjects();

        Setup_SliderUI(slidersContainer);
        
    }

    public void SingletonCheck()
    {
        if (Instance != null)
        {
            Debug.WriteLine("SliderBuilder instance already exists.");
            return;
        }
        Instance = this;
    }

    public bool CheckSliders(string data)
    {
        string[] sliderValues = data.Split('|');
        if (sliders.Length != sliderValues.Length)
        {
            return false; // Mismatch in slider count
        }
        else
        {
            return true;
        }
    }

    
    public void Setup_SliderUI(HorizontalStackLayout SlidersContainer)
    {
        if(sliders.Length == 0)
        {
            Debug.WriteLine("No sliders to set up.");
            return;
        }
        VerticalStackLayouts = new VerticalStackLayout[sliders.Length];
        for (int i = 0; i < sliders.Length; i++)
        {
            VerticalStackLayouts[i] = new VerticalStackLayout { Padding = 0 };

            // Label
            Label applicationNameLabel = new Label
            {
                Text = sliders[i].ApplicationName,
                FontSize = 20,
                Margin = 0
            };
            VerticalStackLayouts[i].Add(applicationNameLabel);
            sliders[i].SetApplicationNameLabel(applicationNameLabel);

            // Slider
            SfSlider controlledSlider = new SfSlider
            {
                Value = sliders[i].Value,
                Maximum = 100,
                Minimum = 0,
                Margin = 0,
                Interval = 10,
                ShowTicks = true,
                Orientation = SliderOrientation.Vertical
            };
            VerticalStackLayouts[i].Add(controlledSlider);
            sliders[i].SetControlledSlider(controlledSlider);

            // Image Button
            ImageButton applicationIconButton = new ImageButton
            {
                Source = "discord.png",
                MaximumHeightRequest = 50,
                MaximumWidthRequest = 50,
                Margin = 0
            };
            VerticalStackLayouts[i].Add(applicationIconButton);
            sliders[i].SetApplicationIconButton(applicationIconButton);
            if(SlidersContainer == null)
            {
                Debug.WriteLine("SlidersContainer is null.");
                return;
            }
            SlidersContainer.Add(VerticalStackLayouts[i]);
        }
    }

    private void OpenCOMPort()
    {
        try
        {
            serialPort = new SerialPort("COM9", 20000)
            {
                 // Set a timeout to avoid indefinite blocking
            };
            serialPort.Open();
            Thread.Sleep(2000); // Wait for initialization
            serialPort.DataReceived += DataReceivedHandler; 
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in COM_Port_Communication: {ex.Message}");
            return;
        }
    }

    public void SetupSliderObjects()
    {
        string data = serialPort.ReadLine();
        if (!string.IsNullOrEmpty(data))
        {
            string[] sliderValue = data.Split('|');
            sliders = new SliderClass[sliderValue.Length];
            for (int i = 0; i < sliderValue.Length; i++)
            {
                sliders[i] = new SliderClass
                {
                    Value = double.Parse(sliderValue[i]),
                    ApplicationPath = $"ApplicationPath_{i}",
                    ApplicationName = "discord"
                };
            }
        }
        else
        {
            sliders = new SliderClass[0];
        }
    }
    
    public bool CheckContainer(HorizontalStackLayout container)
    {
        if (Containers.Children.Count != sliders.Length)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            string data = serialPort.ReadLine().Trim();
            //resets sliders if they were improperly initialized
            if (!CheckSliders(data))
            {
                SetupSliderObjects();
            }

            if (!CheckContainer(Containers))
            {
            }
            double[] sliderValues = ConvertStringToDoubleArray(data);
            Debug.WriteLine($"Received data: {data}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                for (int i = 0; i < sliderValues.Length && i < sliders.Length; i++)
                {
                    sliders[i].SetValue(100*sliderValues[i]/1030);
                    
                   
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading data: {ex.Message}");
        }
    }

    private static double[] ConvertStringToDoubleArray(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new double[sliders.Length];

        return input
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.TryParse(s, out var n) ? n : 0)
            .ToArray();
    }
}