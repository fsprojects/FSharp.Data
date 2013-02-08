using System;
using System.Net;
using System.Net.Browser;
using System.Windows.Controls;

namespace Silverlight5App
{
    public partial class MainPage : UserControl
    {
        public class Handler : IWebRequestCreate
        {
            public WebRequest Create(Uri uri)
            {
                return WebRequestCreator.ClientHttp.Create(new Uri("http://localhost:3234/Proxy.ashx?" + Uri.EscapeUriString(uri.OriginalString)));
            }
        }

        public MainPage()
        {
            InitializeComponent();

            HttpWebRequest.RegisterPrefix("http://", new Handler());

            PortableLibrary.populateDataAsync(true, item => this.tree.Items.Add(item));
        }
    }
}
