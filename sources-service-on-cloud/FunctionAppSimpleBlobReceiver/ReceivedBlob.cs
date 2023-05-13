using System;
using System.IO;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;


namespace FunctionAppSimpleBlobReceiver
{
    public class ReceivedBlob
    {
        [FunctionName("ReceivedBlob")]
        public void Run([BlobTrigger("edge-raspi3-001-files/{name}", Connection = "blob_connection_string")]BlockBlobClient myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n");
        }
    }
}
