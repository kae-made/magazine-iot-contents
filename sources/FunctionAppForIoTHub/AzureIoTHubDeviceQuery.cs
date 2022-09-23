using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Devices;
using System.Collections.Generic;

namespace FunctionAppForIoTHub
{
    public static class AzureIoTHubDeviceQuery
    {
        [FunctionName("DeviceQuery")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
            string connectionString = configuration.GetConnectionString("IOTHUB_CONNECTION_STRING_FOR_REGISTRY");
            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            await registryManager.OpenAsync();

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var query = registryManager.CreateQuery(requestBody);
            var deviceEntries = new List<DeviceEntry>();
            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsTwinAsync();
                foreach (var twin in page)
                {
                    deviceEntries.Add(new DeviceEntry() { DeviceId = twin.DeviceId });
                }
            }
            string responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(deviceEntries);

            await registryManager.CloseAsync();

            return new OkObjectResult(responseMessage);
        }
    }

    public class DeviceEntry
    {
        public string DeviceId { get; set; }
    }
}
