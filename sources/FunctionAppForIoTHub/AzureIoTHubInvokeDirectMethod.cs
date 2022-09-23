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
    public static class AzureIoTHubInvokeDirectMethod
    {
        [FunctionName("InvokeDirectMethod")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string deviceId = req.Query["deviceid"];
            string methodName = req.Query["method"];

            var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
            string connectionString = configuration.GetConnectionString("IOTHUB_CONNECTION_STRING_FOR_SERVICE");
            var serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            await serviceClient.OpenAsync();

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var cloudToDeviceMethod = new CloudToDeviceMethod(methodName);
            cloudToDeviceMethod.SetPayloadJson(requestBody);
            var methodResponse = await serviceClient.InvokeDeviceMethodAsync(deviceId, cloudToDeviceMethod);

            var response = new
            {
                Status = methodResponse.Status,
                Payload = methodResponse.GetPayloadAsJson()
            }; ;

            string responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(response);

            return new OkObjectResult(responseMessage);
        }
    }
}