using Microsoft.Phone.Controls;

namespace WindowsPhone8App
{
    public partial class MainPage : PhoneApplicationPage
    {
        public MainPage()
        {
            InitializeComponent();
            PortableLibrary.populateDataAsync(item => this.listBox.Items.Add(item));
        }
    }
}