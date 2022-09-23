'use strict';

var uuid = require('uuid');
var JobClient = require('azure-iothub').JobClient;

var connectionString = 'HostName=youriothub.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=...';
var targetQuery = "deviceId IN ['TestDevice']";
var startTime = new Date();
var maxExecutionTimeInSeconds = 300;
var jobClient = JobClient.fromConnectionString(connectionString);

function monitorJob(jobId, callback) {
    var jobMonitorInterval = setInterval(function () {
        jobClient.getJob(jobId, function (err, result) {
            if (err) {
                console.error('Could not get job status: ' + err.message);
            } else {
                console.log('Job: ' + jobId + ' - status: ' + result.status);
                if (result.status === 'completed' || result.status === 'failed' || result.status === 'cancelled') {
                    clearInterval(jobMonitorInterval);
                    callback(null, result);
                }
            }
        });
    }, 5000);
}
var methodParams = {
    methodName: 'Start',
    payload: null,
    responseTimeoutInSeconds: 30
};
var methodJobId = uuid.v4();
console.log('scheduling device method job with id: ' + methodJobId);
jobClient.scheduleDeviceMethod(
    methodJobId,
    targetQuery,
    methodParams,
    startTime,
    maxExecutionTimeInSeconds,
    function (err) {
        if (err) {
            console.error('Could not schedule device method job: ' + err.message);
        }
        else {
            monitorJob(methodJobId, function (err, result) {
                if (err) {
                    console.error('Could not monitor device method job: ' + err.message);
                } else {
                    console.log(JSON.stringify(result, null, 2));
                }
            })
        }
    });