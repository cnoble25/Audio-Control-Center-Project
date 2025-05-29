using System.Diagnostics;
using Audio_Control_Center_Application;
using System.Management;
using System.IO.Ports;
// using Microsoft.UI.Xaml.Media.Animation;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Audio_Control_Center_Application
{
    public partial class MainPage : ContentPage
    {

        public MainPage()
        {
            
            HorizontalStackLayout slidersContainer = Container;
            InitializeComponent();
            SliderBuilder builder = new SliderBuilder(Container);
            sampleText.Text = JsonSerializer.Serialize(AudioController.GetAvailableProcesses());
            
            
        }


        public void On(object sender, EventArgs e)
        {
           
            
            
            // Handle button click event
            DisplayAlert("Button Clicked", "You clicked the button!", "OK");
        }
        
        

    }
}
