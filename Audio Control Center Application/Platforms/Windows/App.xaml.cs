using Microsoft.UI.Xaml;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Audio_Control_Center_Application.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            try
            {
                this.InitializeComponent();
                
                // Handle Windows-specific unhandled exceptions
                Microsoft.UI.Xaml.Application.Current.UnhandledException += (sender, args) =>
                {
                    Debug.WriteLine($"Windows Unhandled Exception: {args.Exception.GetType().Name} - {args.Exception.Message}");
                    Debug.WriteLine($"Stack trace: {args.Exception.StackTrace}");
                    args.Handled = true; // Mark as handled to prevent crash
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in Windows App constructor: {ex.GetType().Name} - {ex.Message}");
            }
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
