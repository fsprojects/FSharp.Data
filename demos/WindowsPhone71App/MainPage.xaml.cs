using Microsoft.Phone.Controls;

namespace WindownPhone71App
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