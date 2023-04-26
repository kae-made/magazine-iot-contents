using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

namespace WpfAppRTSP2Images
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        RTSP2Images rtsp2images = null;
        IoTDeviceFileToBlob iotDeviceFileToBlob = null;

        private async void buttonControl_Click(object sender, RoutedEventArgs e)
        {
            if (buttonControl.Content.ToString() == "Start")
            {
                if (rtsp2images != null)
                {
                    await rtsp2images.StopAsync(ShowLog);
                }
                try
                {
                    rtsp2images = new RTSP2Images(tbURL.Text, ((ComboBoxItem)cbFormat.SelectedItem).Content.ToString(), int.Parse(tbFPS.Text),
                       async (w) => { tbWidth.Text = $"{w}"; }, async (h) => { tbHeight.Text = $"{h}"; }, tbFolder.Text,
                       FileUpload);
                    if (cbUpload.IsChecked == true)
                    {
                        iotDeviceFileToBlob = new IoTDeviceFileToBlob(tbCS.Text, rtsp2images.UpdateProperties);
                        await iotDeviceFileToBlob.StartAsync();
                    }
                    await rtsp2images.StartAsync(ShowLog);
                    buttonControl.Content = "Stop";
                }
                catch (Exception ex) { ShowLog(ex.Message); }
            }
            else
            {
                if (rtsp2images != null)
                {
                    await rtsp2images.StopAsync(ShowLog);
                    rtsp2images = null;
                }
            }
        }

        private async Task FileUpload(string filePath)
        {
            if (cbUpload.IsChecked == true && iotDeviceFileToBlob != null)
            {
                await iotDeviceFileToBlob.UploadFile(filePath);
                await ShowLog($"Updated {filePath}");
                if (cbDelete.IsChecked == true)
                {
                    File.Delete(filePath);
                    await ShowLog($"Deleted {filePath}");
                }
            }
        }

        private async Task ShowLog(string message)
        {
            var sb = new StringBuilder(tbLog.Text);
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine(message);
            }
            tbLog.Text = sb.ToString();
        }

        private void buttonUpload_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
