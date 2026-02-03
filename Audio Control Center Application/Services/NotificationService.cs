namespace Audio_Control_Center_Application.Services
{
    public static class NotificationService
    {
        private static ContentPage? GetCurrentPage()
        {
            try
            {
                if (Application.Current?.MainPage is Shell shell)
                {
                    return shell.CurrentPage as ContentPage;
                }
                return Application.Current?.MainPage as ContentPage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting current page: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        public static async Task ShowErrorAsync(string message)
        {
            try
            {
                var page = GetCurrentPage();
                if (page != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await page.DisplayAlert("Error", $"❌ {message}", "OK");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error showing error alert: {ex.GetType().Name} - {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot show error alert - page is null: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowErrorAsync: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public static async Task ShowSuccessAsync(string message)
        {
            try
            {
                var page = GetCurrentPage();
                if (page != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await page.DisplayAlert("Success", $"✅ {message}", "OK");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error showing success alert: {ex.GetType().Name} - {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot show success alert - page is null: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowSuccessAsync: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public static async Task ShowInfoAsync(string message)
        {
            try
            {
                var page = GetCurrentPage();
                if (page != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await page.DisplayAlert("Info", $"ℹ️ {message}", "OK");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error showing info alert: {ex.GetType().Name} - {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot show info alert - page is null: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowInfoAsync: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
