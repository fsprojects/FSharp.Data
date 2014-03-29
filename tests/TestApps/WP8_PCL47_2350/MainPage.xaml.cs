using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;

namespace WP8_PCL47_2350
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SystemTray.SetProgressIndicator(this, new ProgressIndicator { IsVisible = true, IsIndeterminate = true, Text = "Loading..." });
            content.Text = await Test.getTestDataAsTask();
            SystemTray.SetProgressIndicator(this, null);
        }
    }
}