namespace Audio_Control_Center_Application
{
    public partial class MainPage : ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
        }


        public void On(object sender, EventArgs e)
        {
            // Handle button click event
            DisplayAlert("Button Clicked", "You clicked the button!", "OK");
        }

    }
}
