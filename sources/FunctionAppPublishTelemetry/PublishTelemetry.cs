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
using Azure.DigitalTwins.Core;
using Azure.Identity;

namespace FunctionAppPublishTelemetry
{
    public static class PublishTelemetry
    {
        static DigitalTwinsClient adtClient = null;

        [FunctionName("PublishTelemetry")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            if (adtClient == null)
            {
                var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
                string adtUrl = configuration.GetConnectionString("ADT");
                var credential = new DefaultAzureCredential();
                adtClient = new DigitalTwinsClient(new Uri(adtUrl), credential);
            }

            string twinId = req.Query["twinId"];
            var telemetryContent = new
            {
                Environment = new
                {
                    Temperature = 27.1,
                    Humidity = 63.7,
                    AtmosphericPressure = 1001.5,
                    CO2Concentration = 204.8,
                    Brightness = 10004.2
                }
            };
            string payload = Newtonsoft.Json.JsonConvert.SerializeObject(telemetryContent);
            var publishResponse = await adtClient.PublishTelemetryAsync(twinId, Guid.NewGuid().ToString(), payload);

            var response = new
            {
                status = publishResponse.Status,
                content = System.Text.Encoding.UTF8.GetString(publishResponse.Content)
            };
            string responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(response);

            return new OkObjectResult(responseMessage);
        }
    }
}
