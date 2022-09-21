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
using System.Linq;

namespace FunctionAppForIoTHub
{
    public static class AccessToIoTHubSendC2DMessage
    {
        [FunctionName("C2DMessage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string deviceId = req.Query["deviceid"];
            string messageId = req.Query["messageid"];

            var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
            string connectionString = configuration.GetConnectionString("IOTHUB_CONNECTION_STRING_FOR_SERVICE");
            var serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
            await serviceClient.OpenAsync();

            string responseMessage = "";

            if (req.Method.ToLower() == "get")
            {
                var feedbackReceiver = serviceClient.GetFeedbackReceiver();
                var feedbackBatch = await feedbackReceiver.ReceiveAsync();
                if (feedbackBatch != null)
                {
                    var sentMsg = feedbackBatch.Records.Where(m => m.OriginalMessageId == messageId).FirstOrDefault();
                    if (sentMsg != null)
                    {
                        var response = new
                        {
                            SentTo = sentMsg.DeviceId,
                            MessageId = sentMsg.OriginalMessageId,
                            Status = sentMsg.StatusCode.ToString()
                        };
                        responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(response);
                    }
                }
                if (string.IsNullOrEmpty(responseMessage))
                {
                    responseMessage = $"C2D messgae({messageId} to {deviceId}) doesn't exist";
                }               
            }
            else if (req.Method.ToLower() == "post")
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var c2dMsg = new Message(System.Text.Encoding.UTF8.GetBytes(requestBody));
                c2dMsg.MessageId = Guid.NewGuid().ToString();
                c2dMsg.Ack = DeliveryAcknowledgement.Full;
                await serviceClient.SendAsync(deviceId, c2dMsg);

                var response = new
                {
                    SendTo = deviceId,
                    MessageId = messageId
                };
                responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(response);
            }

            await serviceClient.OpenAsync();

            return new OkObjectResult(responseMessage);
        }
    }
}
