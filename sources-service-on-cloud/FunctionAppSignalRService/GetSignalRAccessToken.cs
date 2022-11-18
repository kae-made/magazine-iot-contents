using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;

namespace FunctionAppSignalRService
{
    public static class GetSignalRAccessToken
    {
        [FunctionName("GetSignalRAccessToken")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [SignalRConnectionInfo (HubName = "ADTHub", ConnectionStringSetting ="AzureSignalRConnectionString")]
            SignalRConnectionInfo connectionInfo,
            ILogger log)
        {
            log.LogInformation("SignalR negotiation request.");

            return new OkObjectResult(connectionInfo);
        }
    }
}
