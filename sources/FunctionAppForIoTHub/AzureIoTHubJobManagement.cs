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
    public static class AzureIoTHubJobManagement
    {
        [FunctionName("JobManagement")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var configuration = new ConfigurationBuilder().AddJsonFile("local.settings.json", true).AddEnvironmentVariables().Build();
            string connectionString = configuration.GetConnectionString("IOTHUB_CONNECTION_STRING_FOR_OWNER");
            var jobClient = JobClient.CreateFromConnectionString(connectionString);
            string responseMessage = "";

            try
            {
                await jobClient.OpenAsync();

                string jobId = req.Query["jobid"];
                if (req.Method.ToLower() == "get")
                {
                    var monitorJob = await jobClient.GetJobAsync(jobId);
                    string queryCondition = monitorJob.QueryCondition;
                    string jobStatusMessage = monitorJob.StatusMessage;
                    string cloudToDeviceMethod = monitorJob.CloudToDeviceMethod.MethodName;
                    string failerReason = null;
                    if (monitorJob.Status == JobStatus.Failed)
                    {
                        failerReason = monitorJob.FailureReason;
                    }
                    var deviceHubStatistics = monitorJob.DeviceJobStatistics;
                    var resultOfMonitor = new
                    {
                        QueryCondition = queryCondition,
                        StatusMessage = jobStatusMessage,
                        CloudToDeviceMethod = cloudToDeviceMethod,
                        Status = monitorJob.Status.ToString(),
                        FailerReason = failerReason,
                        DeviceCount = deviceHubStatistics.DeviceCount,
                        DeviceSucceededCount = deviceHubStatistics.SucceededCount,
                        DeviceRunningCount = deviceHubStatistics.RunningCount,
                        DevicePendingCount = deviceHubStatistics.PendingCount,
                        DeviceFailedCount = deviceHubStatistics.FailedCount
                    };
                    responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(resultOfMonitor);
                }
                else if (req.Method.ToLower() == "post")
                {
                    jobId = Guid.NewGuid().ToString();
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    dynamic data = JsonConvert.DeserializeObject(requestBody);
                    string targetDevicesQuery = data["target"];
                    string methodName = data["method"];
                    int timeoutInMinutes = data["timeout"];
                    dynamic payload = data["payload"];
                    string payloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var cloudToDeviceMethod = new CloudToDeviceMethod(methodName,TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
                    cloudToDeviceMethod.SetPayloadJson(payloadJson);
                    var jobResponse = await jobClient.ScheduleDeviceMethodAsync(jobId, targetDevicesQuery, cloudToDeviceMethod, DateTime.UtcNow, (long)TimeSpan.FromMinutes(timeoutInMinutes).TotalSeconds);
                    var responseOfJob = new
                    {
                        JobId = jobId,
                        Status = jobResponse.Status.ToString()
                    };
                    responseMessage = Newtonsoft.Json.JsonConvert.SerializeObject(responseOfJob);
                }
                else
                {
                    responseMessage = "bad method";
                }
                await jobClient.CloseAsync();
            }
            catch (Exception ex)
            {
                responseMessage = ex.Message;
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
