using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WebAppSignalR.Models;

namespace WpfAppReceiveSignalR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string url = configuration.GetConnectionString("SIGNALR");
            if (!string.IsNullOrEmpty(url))
            {
                tbSignalRHub.Text = url;
                signalRServiceUrl = url;
            }
            url = configuration.GetConnectionString("GETTOKEN");
            if (!string.IsNullOrEmpty(url))
            {
                tbSignalRHub.Text = url;
            }


        }

        string signalRServiceUrl = "";
        string accessToken = "";

        HubConnection hubConnection;
        private async void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (buttonConnect.Content.ToString() == "Connect")
                {
                    if (string.IsNullOrEmpty(signalRServiceUrl))
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var accessTokenResult = await httpClient.GetAsync(tbSignalRHub.Text);
                            if (accessTokenResult.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                using (var reader = new StreamReader(accessTokenResult.Content.ReadAsStream()))
                                {
                                    string contentJson = reader.ReadToEnd();
                                    dynamic content = Newtonsoft.Json.JsonConvert.DeserializeObject(contentJson);
                                    signalRServiceUrl = content.url;
                                    accessToken = content.accessToken;
                                }
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        hubConnection = new HubConnectionBuilder().WithUrl(signalRServiceUrl).WithAutomaticReconnect().Build();
                    }
                    else
                    {
                        hubConnection = new HubConnectionBuilder().WithUrl(signalRServiceUrl,
                            option => { option.AccessTokenProvider = async () => { return accessToken; }; }
                            ).WithAutomaticReconnect().Build();
                    }
                    
                    hubConnection.On<ADTUpdateData>("TwinGraphUpdated", ReceivedUpdatedData);
                    await hubConnection.StartAsync();
                    buttonConnect.Content = "Disconnect";
                }
                else
                {
                    await hubConnection.StopAsync();
                    buttonConnect.Content = "Connect";
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

 
        private void ReceivedUpdatedData(ADTUpdateData msg)
        {
            AddMessage(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
        }

        private void AddMessage(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                var sb = new StringBuilder(tbMessage.Text);
                using (var writer = new StringWriter(sb))
                {
                    writer.WriteLine($"[{DateTime.Now.ToString("yyyyMMddHHmmss")}] {msg}");
                }
                tbMessage.Text = sb.ToString();
            });
        }
    }

}