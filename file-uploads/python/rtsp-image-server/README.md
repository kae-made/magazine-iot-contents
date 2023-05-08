# Azure IoT Edge deployment

Other Docker Image can get image data from this IoT Edge Module.

```
http://<ip addres of the IoT Edge device>:<rest_server_port>/image
```

## Docker Image
reference url - [kaemade/rtsp-iamge-server:0.0.1-arm32v7](https://hub.docker.com/layers/kaemade/rtsp-image-server/0.0.1-arm32v7/images/sha256-05e5d453c7e43a1d2c3c46c15b375b33922b60b3bf66284b290aac984dcc1ac1?context=repo)

## Environment Variables
- rtsp_url - Url of RTSP Stream
- rest_server_port - Port number for image getter

## Container Create Options
```
{
    "NetworkingConfig": {
        "EndpointsConfig": {
            "host": {}
        }
    },
    "HostConfig": {
        "NetworkMode": "host",
        "PortBindings": {
            "3000/tcp": [
                {
                    "HostPort": "3000" ← shoud be same as rest_server_port
                }
            ]
        },
        "ExportPorts": {
            "3000/tcp": {} ← shoud be same as rest_server_port
        }
    }
}
```
## Module Twins settings
```json
{
}
```
