namespace Audio_Control_Center_Application;

public class Slider
{
    public double Value { get; set; }
    public string ApplicationPath { get; set; }
    public string ApplicationName { get; set; }
    public int index { get; set; }
    
    public Slider(double value, string applicationPath, string applicationName)
    {
        Value = value;
        ApplicationPath = applicationPath;
        ApplicationName = applicationName;
        for (int i = 0; i < Slider_Builder.sliders.Length; i++)
        {
            if (Slider_Builder.sliders[i] == this)
            {
                index = i;
                break;
            }
        }
    }
    
    public Slider()
    {
        // Default constructor
    }
    
    public void SetValue(double value)
    {
        Value = value;
        // Here you can add code to update the slider value in the UI or send it to the serial port
        // For example, you might want to call a method in Slider_Builder to handle this
        // Slider_Builder.UpdateSliderValue(this);
    }
}