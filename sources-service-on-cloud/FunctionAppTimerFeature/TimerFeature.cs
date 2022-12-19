using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FunctionAppTimerFeature
{
    public static class TimerFeature
    {
        [FunctionName("TimerFeature")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger logger)
        {
            var dataForFiring = context.GetInput<DataForFiring>();
            dataForFiring.InstanceId = context.InstanceId;

            var timerEntityId = new EntityId(nameof(TimerEntry), dataForFiring.InstanceId);
            await context.CallEntityAsync(timerEntityId, "SetState", (dataForFiring.InstanceId, dataForFiring.FireTime, dataForFiring.Message));

            var cancelEvent = context.WaitForExternalEvent("Cancel");
            using (var cts = new CancellationTokenSource())
            {
                bool finished = false;
                var timerTask = context.CreateTimer(dataForFiring.FireTime, cts.Token);
                var winner = await Task.WhenAny(timerTask, cancelEvent);
                if (winner == timerTask)
                {
                    await context.CallActivityAsync("TimerFired", dataForFiring);
                    finished = true;
                }
                else if (winner == cancelEvent)
                {
                    logger.LogInformation($"Canceled - {dataForFiring.InstanceId}");
                    finished = true;
                }
                else
                {
                    logger.LogError($"Unknown task - {winner.ToString()}");
                }
                if (finished)
                {
                    await context.CallEntityAsync(timerEntityId, "Delete");
                }
            }
        }

        [FunctionName(nameof(TimerFired))]
        public static void TimerFired([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            var data = context.GetInput<DataForFiring>();
            log.LogInformation($"Fired {data.InstanceId}:{data.Message}");
        }

        [FunctionName("CancelTrigger")]
        public static async Task<HttpResponseMessage> CancelTrigger(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient client)
        {
            var instanceId = req.RequestUri.ParseQueryString()["instanceid"];
            await client.RaiseEventAsync(instanceId, "Cacnel");
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }

        [FunctionName("TimeRemain")]
        public static async Task<HttpResponseMessage> TimeRemain(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestMessage req,
            [DurableClient] IDurableEntityClient client)
        {
            var instanceId = req.RequestUri.ParseQueryString()["instanceid"];
            var timerEntityId = new EntityId(nameof(TimerEntry), instanceId);
            // EntityStateResponse<JObject> stateResponse = await client.ReadEntityStateAsync<JObject>(timerEntityId);
            var timerEntity = await client.ReadEntityStateAsync<TimerEntry>(timerEntityId);
            //var timeRemain = new { FireTime = timerEntity.EntityState.FireTime };
            return req.CreateResponse(System.Net.HttpStatusCode.OK, timerEntity, "text/json");
        }


        [FunctionName("TimerFeature_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string fireTime = req.RequestUri.ParseQueryString().Get("firetime");
            string timeMsg = req.RequestUri.ParseQueryString().Get("message");
            var dataForFiring = new DataForFiring() { FireTime = DateTime.Parse(fireTime), Message = timeMsg };

            string instanceId = await starter.StartNewAsync<DataForFiring>("TimerFeature", dataForFiring);


            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }

    public class DataForFiring
    {
        public string InstanceId { get; set; }
        public DateTime FireTime { get; set; }
        public string Message { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TimerEntry
    {
        [JsonProperty("InstanceId")]
        public string InstanceId { get; set; }
        [JsonProperty("FireTime")]
        public DateTime FireTime { get; set; }
        [JsonProperty("Message")]
        public string Message { get; set; }

        public DateTime GetFireTime() => this.FireTime;

        public void SetState((string instanceId, DateTime fireTime, string message) timerParams)
        {
            InstanceId = timerParams.instanceId;
            FireTime = timerParams.fireTime;
            Message = timerParams.message;
        }

        public void Delete()
        {
            Entity.Current.DeleteState();
        }

        [FunctionName(nameof(TimerEntry))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx) => ctx.DispatchAsync<TimerEntry>();        
    }
}