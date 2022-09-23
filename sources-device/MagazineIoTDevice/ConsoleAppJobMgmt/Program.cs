// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

string cs = "HostName=youriothub.azure-devices.net;SharedAccessKeyName=registryReadWrite;SharedAccessKey=...";

var jobClinet = Microsoft.Azure.Devices.JobClient.CreateFromConnectionString(cs, new Microsoft.Azure.Devices.HttpTransportSettings() {});

await jobClinet.OpenAsync();
string deviceId = "TestDevice";

string twinJobId = "";
string methodJobId = "";
if (!string.IsNullOrEmpty(twinJobId))
{
    var twinJobStatus = await jobClinet.GetJobAsync(twinJobId);
}
if (!string.IsNullOrEmpty(methodJobId))
{
    var methodJobStatus = await jobClinet.GetJobAsync(methodJobId);
}

string jobId = Guid.NewGuid().ToString();
string targetQuery = $"DeviceId IN ['{deviceId}']";

var twin = new Microsoft.Azure.Devices.Shared.Twin(deviceId);
twin.Tags = new Microsoft.Azure.Devices.Shared.TwinCollection();
twin.Tags["Customer"] = "kae";
twin.Properties.Reported["RequestInterval"] = 20000;
twin.ETag = "*";
twin.Properties.Desired["CustomerUpdate"] = DateTime.UtcNow;

//var jobTwinResponse = await jobClinet.ScheduleTwinUpdateAsync(jobId, targetQuery, twin, DateTime.UtcNow, (long)TimeSpan.FromMinutes(2).TotalSeconds);
//Console.WriteLine($"TwinJob:{jobId} - Status={jobTwinResponse.Status.ToString()}");

jobId = Guid.NewGuid().ToString();
//var cloudMethod = new Microsoft.Azure.Devices.CloudToDeviceMethod("Start", TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(600));
var cloudMethod = new Microsoft.Azure.Devices.CloudToDeviceMethod("Stop", TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300));
var jobResponse = await jobClinet.ScheduleDeviceMethodAsync(jobId, targetQuery, cloudMethod, DateTime.UtcNow,(long)TimeSpan.FromMinutes(2).TotalSeconds);
Console.WriteLine($"MethodJob:{jobId} - {jobResponse.Status}");
