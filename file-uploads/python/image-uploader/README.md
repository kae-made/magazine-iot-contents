# Azure IoT Edge deployment

## Docker Image
reference url - [kaemade/image-uploader:0.0.1-arm32v7](https://hub.docker.com/layers/kaemade/image-uploader/0.0.1-arm32v7/images/sha256-10e0d84d3268943a24e05abe0a09d3912b1cc02d47a1b8255b2e4aec1b660dc4?context=repo)

## Module Twins settings
```json
{
    "rtsp_service_url": "http://<rtsp-image-server ip address>:<rtsp-image-server port/image",
    "duration_in_sec": 120,
    "blob_service_address": "AzureBlobStorageonIoTEdge",
    "blob_account_name": "<- blob account name for Blob on IoT Edge ->",
    "blob_account_key": "<- base64 key for blob account name for Blob on IoT Edge ->",
    "container_name": "<- container name of Blob on IoT Edge ->"
}
```
