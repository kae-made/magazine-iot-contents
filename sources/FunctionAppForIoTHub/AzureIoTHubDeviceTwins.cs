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

namespace FunctionAppForIoTHub
{
    public static class AzureIoTHubDeviceTwins
    {
        [FunctionName("DeviceTwins")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string deviceId = req.Query["deviceid"];
            string opType = req.Query["type"];

            var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
            string connectionString = configuration.GetConnectionString("IOTHUB_CONNECTION_STRING_FOR_REGISTRY");
            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            await registryManager.OpenAsync();

            string responseMessage = "";
            string requestBody = "";

            var twinOfTargetDevice = await registryManager.GetTwinAsync(deviceId);
            if (twinOfTargetDevice != null)
            {
                switch (opType)
                {
                    case "tag":
                        if (req.Method.ToLower() == "get")
                        {
                            responseMessage = twinOfTargetDevice.Tags.ToJson();
                        }
                        else if (req.Method.ToLower() == "post")
                        {
                            requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                            string patch = "{ tags: { " + $"{requestBody}" + " } }";
                            await registryManager.UpdateTwinAsync(deviceId, patch, twinOfTargetDevice.ETag);
                            responseMessage = requestBody;
                        }
                        break;
                    case "desired":
                        if (req.Method.ToLower() == "post")
                        {
                            requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                            string patch = "{ properties { desired { " + $"{requestBody}" + " } } }";
                            await registryManager.UpdateTwinAsync(deviceId, patch, twinOfTargetDevice?.ETag);
                        }
                        else if (req.Method.ToLower() == "get")
                        {
                            responseMessage = twinOfTargetDevice.Properties.Desired.ToJson();
                        }
                        break;
                    case "reported":
                        if (req.Method.ToLower() == "get")
                        {
                            responseMessage = twinOfTargetDevice.Properties.Reported.ToJson();
                        }
                        else
                        {
                            responseMessage = "method for reported should be 'get'";
                        }
                        break;
                    default:
                        responseMessage = "type should be tag|desired|reportedD";
                        break;
                }
            }
            else
            {
                responseMessage = $"Device:{deviceId} has not benn registered!";
            }

            await registryManager.CloseAsync();

            return new OkObjectResult(responseMessage);
        }
    }
}
