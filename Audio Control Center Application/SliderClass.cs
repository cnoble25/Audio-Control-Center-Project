using Slider = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Slider;
using Syncfusion.Maui.Sliders;
namespace Audio_Control_Center_Application;

public class SliderClass
{
    public double Value { get; set; }
    public string ApplicationPath { get; set; }
    public string ApplicationName { get; set; }
    public int index { get; set; }
    
    
    public Label ApplicationNameLabel { get; set; }
    
    public SfSlider ControlledSlider { get; set; }
    
    public ImageButton ApplicationIconButton { get; set; }
    
    
    public SliderClass(double value, string applicationPath, string applicationName)
    {
        Value = value;
        ApplicationPath = applicationPath;
        ApplicationName = applicationName;
        for (int i = 0; i < SliderBuilder.sliders.Length; i++)
        {
            if (SliderBuilder.sliders[i] == this)
            {
                index = i;
                break;
            }
        }
    }
    
    public SliderClass()
    {
        // Default constructor
    }
    
    public void SetControlledSlider(SfSlider slider)
    {
        ControlledSlider = slider;
    }
    
    public void SetApplicationNameLabel(Label label)
    {
        ApplicationNameLabel = label;
    }
    public void SetApplicationIconButton(ImageButton button)
    {
        ApplicationIconButton = button;
    }
    
    public void SetValue(double value)
    {
        Value = value;
        if (ControlledSlider != null)
        {
            ControlledSlider.Value = Value;
            AudioController.SetApplicationVolume("", Value/100);
        } 
        // Update the slider's value in the UI
        // Here you can add code to update the slider value in the UI or send it to the serial port
        // For example, you might want to call a method in Slider_Builder to handle this
        // Slider_Builder.UpdateSliderValue(this);
    }
}