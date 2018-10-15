Title: Dissecting the Azure Service Fabric Mesh Right-Click Publish
Published: 2018-10-15
Tags: 
- .NET
- Azure
- Service Fabric
---
# Because friends don't let friends do right-click publish
> This is not a post on why you should not do right-click publish, but rather a post on how to avoid it when working with Azure Service Fabric Mesh. If you want an answer for the why part, please read [Damian Brady's blog post](https://damianbrady.com.au/2018/02/01/friends-dont-let-friends-right-click-publish/) on the subject.

If you've ever looked at the [Azure Service Fabric Mesh tutorials](https://docs.microsoft.com/en-us/azure/service-fabric-mesh/), you've probably noticed that they only show you how to do right-click publish (or deploy pre-made ARM-templates). In order to understand how to avoid right-click publish, we'll need to understand what right-click publish does. Therefore, in this blog post we're going to dissect the right-click publish feature of [Service Fabric Mesh Tools for Visual Studio](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.ServiceFabricMesh). Before I started any practical work, which eventually lead to this blog post, I had a rough idea on how to bypass right-click publish when working with Azure Service Fabric Mesh solutions and Visual Studio, namely:
* Create docker images for each service in the application
* Push the docker images to an Azure Container Registry
* Generate an ARM template
* Deploy the Azure Service Fabric Mesh application using the generated ARM template

I knew that the Service Fabric Mesh Tools was accountable for some of the "magic", while the [Service Fabric MSBuild targets](https://www.nuget.org/packages/Microsoft.VisualStudio.Azure.SFApp.Targets) was accountable the rest. Because I'm more comfortable with debugging MSBuild than reverse-engineering Visual Studio extensions, the natural starting point was investigating the MSBuild targets and see how far it would take me.

In the following sections I will use the `todolistapp` sample located [here](https://github.com/Azure-Samples/service-fabric-mesh/tree/c3da5474a67d635565a092ed9090442888142f9f/src/todolistapp). So if you'd like to follow along, make sure you clone the [Service Fabric Mesh Samples repository](https://github.com/Azure-Samples/service-fabric-mesh).

## Investigating MSBuild Targets
There are many ways to debug MSBuild, one  way to find all targets is to use the preprocess switch with MSBuild. MSBuild help says the following about the preprocess switch:
>  /preprocess[:file]
                     Creates a single, aggregated project file by
                     inlining all the files that would be imported during a
                     build, with their boundaries marked. This can be
                     useful for figuring out what files are being imported
                     and from where, and what they will contribute to
                     the build. By default the output is written to
                     the console window. If the path to an output file
                     is provided that will be used instead.
                     (Short form: /pp)
                     Example:
                       /pp:out.txt

We run the following command and then inspect the `out.xml` file.
```cmd
msbuild /r /pp:out.xml todolistapp\todolistapp.sfaproj
```
While inspecting the `out.xml` file, we'll find two targets which are of special interest; `SFAppBuildApplication` and `SFAppPackageApplication`. I also remember seeing these two targets when skimming through the Service Fabric Mesh Tools logs in Visual Studio.

## Building Service Fabric Mesh Application
Let's try out the first target then, run:
```cmd
msbuild /r /t:todolistapp:SFAppBuildApplication /p:Configuration=Release;Platform="Any CPU"
```
In the logs, we can see that the above target will find all services and build docker images, which is exactly what we want! We can see that it is naming and tagging our images like `webfrontend:dev` and `todoservice:dev`.
```cmd
  docker build -f "C:\Users\mb\src\gh\service-fabric-mesh\src\todolistapp\WebFrontEnd\Dockerfile" -t webfrontend:dev "C:\Users\mb\src\gh\service-fabric-mesh\src\todolistapp"
  Sending build context to Docker daemon   3.52MB

  Step 1/16 : FROM microsoft/dotnet:2.1-aspnetcore-runtime-nanoserver-sac2016 AS base
   ---> b1d6aab503b4
   ---> d35f8074bc6a

  ...

  Step 16/16 : ENTRYPOINT ["dotnet", "WebFrontEnd.dll"]
   ---> Running in 95c5976daa00
  Removing intermediate container 95c5976daa00
   ---> 915e93d027a6
  Successfully built 915e93d027a6
  Successfully tagged webfrontend:dev
```
The above step solved the first issue for us, it created docker images for each service in the application. While we could have searched for all dockerfile's ourselves and run `docker build`, I find the MSBuild target a little more helpful.

## Push the docker images to an Azure Container Registry
Next thing to do is to push our newly created docker images to a container registry. If you don't already have an Azure Container Registry, please follow the steps [here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-portal) to create one and obtain the access keys (login server, username and password). Now, to push our local docker images to the Azure Container Registry, we'll just follow some of the examples found [here](https://docs.microsoft.com/en-us/azure/container-registry/container-registry-get-started-docker-cli).

First we'll use [docker tag](https://docs.docker.com/engine/reference/commandline/tag/) to create aliases of our local images (replace `todolistappacr` with the name of your container registry):
```cmd
docker tag webfrontend:dev todolistappacr.azurecr.io/webfrontend:1.0
docker tag todoservice:dev todolistappacr.azurecr.io/todoservice:1.0
```

Next, we'll need to login to the Azure Container Registry and push our images (replace `todolistappacr`with the name of your container registry).
```cmd
docker login todolistappacr.azurecr.io -u username -p password
docker push todolistappacr.azurecr.io/webfrontend:1.0
docker push todolistappacr.azurecr.io/todoservice:1.0
```

## Packaging Service Fabric Mesh Application
Now that we have pushed the docker images to our container registry, we can start preparing the Azure Service Fabric Mesh application. For this, we'll call `msbuild` with the `SFAppPackageApplication` target.
```cmd
msbuild /r /t:todolistapp:SFAppPackageApplication /p:Configuration=Release;Platform="Any CPU"
```
The above target will find all `yaml`-files and pass them to a tool called `SfSbzYamlMerge.exe`. The tool will merge all `yaml`-files and output a `json`-file (`merged-arm_rp.json`). This `json`-file is the ARM (Azure Resource Manager) template we are going to use for publishing our Service Fabric Mesh application to Azure. If we try to diff the `merged-arm_rp.json` file to the one Visual Studio leaves behind after doing right-click publish, we'll notice some differences.

```diff
--- bin/Release/SBZPkg/merged-arm_rp.json       2018-10-11 21:46:46.967348000 +0300
+++ bin/Debug/SBZPkg/merged-arm_rp.json 2018-10-11 17:38:36.497734800 +0300
@@ -8,6 +8,10 @@
       "metadata": {
         "description": "Location of the resources."
       }
+    },
+    "registryPassword": {
+      "defaultValue": "",
+      "type": "SecureString"
     }
   },
   "resources": [
@@ -29,7 +33,7 @@
               "codePackages": [
                 {
                   "name": "WebFrontEnd",
-                  "image": "webfrontend:dev",
+                  "image": "todolistappacr.azurecr.io/webfrontend:20181011173823",
                   "endpoints": [
                     {
                       "name": "WebFrontEndListener",
@@ -55,6 +59,11 @@
                       "cpu": 0.5,
                       "memoryInGB": 1.0
                     }
+                  },
+                  "imageRegistryCredential": {
+                    "server": "todolistappacr.azurecr.io",
+                    "username": "todolistappacr",
+                    "password": "[parameters('registryPassword')]"
                   }
                 }
               ],
@@ -74,7 +83,7 @@
               "codePackages": [
                 {
                   "name": "ToDoService",
-                  "image": "todoservice:dev",
+                  "image": "todolistappacr.azurecr.io/todoservice:20181011173823",
                   "endpoints": [
                     {
                       "name": "ToDoServiceListener",
@@ -92,6 +101,11 @@
                       "cpu": 0.5,
                       "memoryInGB": 1.0
                     }
+                  },
+                  "imageRegistryCredential": {
+                    "server": "todolistappacr.azurecr.io",
+                    "username": "todolistappacr",
+                    "password": "[parameters('registryPassword')]"
                   }
                 }
               ],
```

The ARM template generated by Visual Studio takes a parameter called `registryPassword` and the images in the code packages for each service seems to point at our Azure Container Registry, while our ARM template just uses `todoservice:dev` (the default name and tag created by the MSBuild targets). The Visual Studio generated ARM template also has a `imageRegistryCredential` in each code package. We'll need to update our ARM template with the changes seen above. Make sure you use the correct registry server and username, also make sure to use the same tag of the docker image as we used when we pushed the images (we used `1.0` in the example previously. So change `20181011173823` to `1.0`).

## Deploy the Azure Service Fabric Mesh application using the generated ARM template
Now that we have created our ARM template, we are ready to publish our Service Fabric Mesh application to Azure. We'll use the same commands as can be found in the [Service Fabric Mesh Tutorial](https://docs.microsoft.com/en-us/azure/service-fabric-mesh/service-fabric-mesh-tutorial-template-deploy-app).

First, Login to Azure
```cmd
az login
```

Then, create a resource group
```cmd
az group create -l eastus -n todolistapp-rg
```

Last, create the deployment (replace `password` with the password to your Azure Container Registry).
```cmd
az mesh deployment create --resource-group todolistapp-rg --template-file todolistapp\bin\Release\SBZPkg\merged-arm_rp.json --name todolistapp --parameters location=eastus registryPassword=password
```

You may follow the progress of the deployment in Azure Portal, but after a couple of minutes you should see that the application have been successfully deployed.
```cmd
Deploying . . .
application todolistapp has been deployed successfully on network todolistappNetwork with public ip address 40.76.208.91
To recieve additional information run the following to get the status of the application deployment.
az mesh app show --resource-group todolistapp-rg --name todolistapp
```

Now, just open a browser and head over to the IP address and see your application in action. Make sure to also specify the correct port. The sample use `20006` as default, so in the above example I'd open my browser and go to http://40.76.208.91:20006/.

## Conclusion
While we got pretty far by just using the msbuild targets located in `Microsoft.VisualStudio.Azure.SFApp.Targets` we still need to do some manual work with pushing docker images and modifying the ARM-template. AFAIK, the msbuild targets don't understand publish profile `yaml`-files as these are solely intended to be used with the Visual Studio Tooling (ie. right-click deploy). We still have some work to do before we can deploy Service Fabric Mesh applications from our CI/CD pipeline. However, if it can be documented, it can be automated. Therefore, stay tuned for a follow up blog post on how to automate the above steps with [Cake](https://cakebuild.net/).
