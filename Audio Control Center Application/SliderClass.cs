namespace Audio_Control_Center_Application;

public class SliderClass
{
    public double Value { get; set; }
    public string? ApplicationPath { get; set; }
    public string? ApplicationName { get; set; }
    public int index { get; set; }
    
    
    public Label? ApplicationNameLabel { get; set; }
    
    public Label? VolumeLabel { get; set; }
    
    public Slider? ControlledSlider { get; set; }
    
    public ImageButton? ApplicationIconButton { get; set; }
    
    
    public SliderClass(double value, string? applicationPath, string? applicationName)
    {
        Value = value;
        ApplicationPath = applicationPath;
        ApplicationName = applicationName;
        if (SliderBuilder.sliders != null)
        {
            for (int i = 0; i < SliderBuilder.sliders.Length; i++)
            {
                if (SliderBuilder.sliders[i] == this)
                {
                    index = i;
                    break;
                }
            }
        }
    }
    
    public SliderClass()
    {
        // Default constructor
    }
    
    public void SetControlledSlider(Slider slider)
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
    
    public void SetVolumeLabel(Label label)
    {
        VolumeLabel = label;
    }
    
    public void SetValue(double value, bool animate = true)
    {
        Value = value;
        
        try
        {
            if (ControlledSlider != null)
            {
                if (animate)
                {
                    // Smoothly animate slider value change
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            if (ControlledSlider == null) return;
                            
                            double currentValue = ControlledSlider.Value;
                            double targetValue = Value;
                            double steps = 10;
                            double increment = (targetValue - currentValue) / steps;
                            
                            for (int i = 0; i < steps; i++)
                            {
                                if (ControlledSlider == null) return;
                                ControlledSlider.Value = currentValue + (increment * (i + 1));
                                await Task.Delay(10);
                            }
                            if (ControlledSlider != null)
                            {
                                ControlledSlider.Value = targetValue;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error animating slider value: {ex.GetType().Name} - {ex.Message}");
                        }
                    });
                }
                else
                {
                    ControlledSlider.Value = Value;
                }
            }
            
            // Update the volume percentage label with animation
            if (VolumeLabel != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var label = VolumeLabel; // Capture reference
                        if (label == null) return;
                        
                        // Load settings to check if inversion is enabled
                        var settings = Audio_Control_Center_Application.Models.AppSettings.Load();
                        double displayValue = settings?.InvertSliders == true 
                            ? 100 - Value 
                            : Value;
                        
                        if (animate)
                        {
                            await label.ScaleTo(1.15, 80, Easing.SpringOut);
                            if (label != null) // Check again after await
                            {
                                label.Text = $"{displayValue:F0}%";
                            }
                            if (label != null)
                            {
                                await label.ScaleTo(1.0, 120, Easing.SpringIn);
                            }
                        }
                        else
                        {
                            if (label != null)
                            {
                                label.Text = $"{displayValue:F0}%";
                            }
                        }
                    }
                    catch (NullReferenceException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NullReferenceException updating volume label: {ex.Message}\nStack trace: {ex.StackTrace}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating volume label: {ex.GetType().Name} - {ex.Message}\nStack trace: {ex.StackTrace}");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SetValue: {ex.GetType().Name} - {ex.Message}");
        }
    }
}