using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfAppRTSP2Images
{
    public delegate Task PropertiesUpdater(IDictionary<string, object> properties);
    public class IoTDeviceFileToBlob
    {
        DeviceClient deviceClient;
        string connectionString;
        PropertiesUpdater propertiesUpdater;

        public IoTDeviceFileToBlob(string connectionString, PropertiesUpdater propertiesUploader)
        {
            this.connectionString = connectionString;
            this.propertiesUpdater = propertiesUploader;
            deviceClient=DeviceClient.CreateFromConnectionString(connectionString);
        }

        public async Task StartAsync()
        {
            var twin = await deviceClient.GetTwinAsync();
            Dictionary<string, object> dp = ResolveDesiredProperties(twin.Properties.Desired);
            await propertiesUpdater(dp);

            deviceClient.SetDesiredPropertyUpdateCallback(desiredPropertiesResolver, this);
        }

        public async Task UploadFile(string fileName)
        {
            var fileInfo = new FileInfo(fileName);
            var fileUploadSasUriRequest = new FileUploadSasUriRequest()
            {
                BlobName = fileInfo.Name
            };
            var sasUri = await deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest);
            var uploadUri = sasUri.GetBlobUri();
            var blockBlobClient = new BlockBlobClient(uploadUri);
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read))
            {
                await blockBlobClient.UploadAsync(fs, new Azure.Storage.Blobs.Models.BlobUploadOptions());
            }
        }

        private async Task desiredPropertiesResolver(TwinCollection desiredProperties, object userContext)
        {
            var dp= ResolveDesiredProperties(desiredProperties);
            await propertiesUpdater(dp);
        }

        private static Dictionary<string, object> ResolveDesiredProperties(TwinCollection twinDP)
        {
            var dp = new Dictionary<string, object>();
            if (twinDP.Contains("duration"))
            {
                dp.Add("duration", (string)twinDP["duration"]);
            }
            if (twinDP.Contains("format"))
            {
                dp.Add("format", (string)twinDP["format"]);
            }

            return dp;
        }
    }
}
