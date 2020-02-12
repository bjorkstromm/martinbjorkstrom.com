Title: Testing protected Azure Functions running in a container on your local machine
Published: 2020-02-12
Tags: 
- Azure
- Azure Functions
---

Recently I had to create an Azure Function using a custom container. The reason was a client of mine did some really cool things using Puppeteer, and now they wanted to run this in an Azure Function. So, I went to [docs.microsoft.com](https://docs.microsoft.com) and found a great [tutorial](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-function-linux-custom-image?tabs=portal%2Cbash&pivots=programming-language-csharp) on how to create a function on Linux using a custom container. However, a little bit into the tutorial, in the [build the container image and test locally](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-function-linux-custom-image?tabs=portal%2Cbash&pivots=programming-language-csharp) section, I noticed something very annoying, namely this:

> Once the image is running in a local container, open a browser to http://localhost:8080, which should display the placeholder image shown below. The image appears at this point because your function is running in the local container, as it would in Azure, which means that it's protected by an access key as defined in function.json with the "authLevel": "function" property. The container hasn't yet been published to a function app in Azure, however, so the key isn't yet available. **If you want to test locally, stop docker, change the authorization property to "authLevel": "anonymous", rebuild the image, and restart docker. Then reset "authLevel": "function" in function.json.** For more information, see [authorization keys](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook#authorization-keys).

Wait, what? Modify code, and rebuild the image? That sounds like a terribly slow and error-prone solution for testing your custom image locally. We will use the same image in Azure, with authorization working, so there must be a better way than modifying code and rebuilding the image. Digging deeper I found out that someone else was also asking about the same thing [here](https://github.com/Azure/azure-functions-host/issues/4147). The issue also contained a solution to my problem, see [this](https://github.com/Azure/azure-functions-host/issues/4147#issuecomment-477431016). The solution is to provide a custom `host.json` to the container running locally. I found the easiest way to do this was to mount a volume from the host machine containing the custom `host.json`. So, here's how I did this:

## 1. Enabling Shared Drives with Docker for Windows
I use Windows as my primary OS, thus using Docker for Windows in order to use Docker. There are lots of guides on how to enable Shared Drives. There's even one over at [docs.microsoft.com](https://docs.microsoft.com/en-us/archive/blogs/wael-kdouh/enabling-drive-sharing-with-docker-for-windows). I however **strongly** disagree with adding the local account into the `Administrators` group. Instead I created a local account, removed it from the `Users` group, and only gave the account access to the folder I wanted to share with Docker. In my case the folder I wanted to share with Docker was `C:\temp\docker`.

## 2. Create custom host.json
Next thing to do is to create a file called `host.json` with the following content:
```json
{
  "masterKey": {
    "name": "master",
    "value": "test",
    "encrypted": false
  },
  "functionKeys": [ ]
}
```
The value of the `masterKey` will be used as function key when testing the function later. I stored this file in folder `C:\temp\docker\keys`.

## 3. Testing the container image locally
In order to test this out, we need to create an Azure Function using a custom container. So, we'll follow the tutorial I linked to in the beginning of this post.

First, we'll create a new Functions project.
```powershell
func init LocalFunctionsProject --worker-runtime dotnet --docker
```

Then we'll add a function with a HTTP trigger
```powershell
func new --name HttpExample --template "HTTP trigger"
```

Then we'll build the image
```powershell
docker build -t localfunctions:dev .
```

And last, we'll run a container
```powershell
docker run -v C:\temp\docker\keys:/azure-functions-host/Secrets `
 -e AzureWebJobsSecretStorageType=files `
 -e AzureFunctionsJobHost__Logging__Console__IsEnabled=true `
 -p 8080:80 -it localfunctions:dev
```

The `-v` option will instruct Docker to mount a volume. In the example above it means that the folder `C:\temp\docker\keys` on the host will be mounted at `/azure-functions-host/Secrets` inside the container. This is a special folder that the Azure Functions runtime will use when we set the environment variable `AzureWebJobsSecretStorageType` to `files`. Remember that our custom `host.json` we created earlier sits in the `C:\temp\docker\keys` folder on the host machine.

As a bonus, we'll also enable console logging by setting the environment variable `AzureFunctionsJobHost__Logging__Console__IsEnabled` to `true`. This will help us a lot if/when we need to troubleshoot any issues in our container.

When the container is running, we can then test the function using e.g. cURL. First, we'll omit the function key (just to prove a point).
```bash
$ curl -s -i http://localhost:8080/api/HttpExample?name=foo
HTTP/1.1 401 Unauthorized
Date: Wed, 12 Feb 2020 21:13:23 GMT
Server: Kestrel
Content-Length: 0
```
And we'll get a `401 Unauthorized`, just like we should. Now, we'll add the function key as request-header (remember the value of the function key was `test`)
```bash
$ curl -s -i -H "x-functions-key:test" http://localhost:8080/api/HttpExample?name=foo
HTTP/1.1 200 OK
Date: Wed, 12 Feb 2020 21:17:05 GMT
Content-Type: text/plain; charset=utf-8
Server: Kestrel
Content-Length: 10

Hello, foo
```

And now we'll get a `200 OK` and a nice greeting from the server. Thanks for reading and hope you found this post useful.

