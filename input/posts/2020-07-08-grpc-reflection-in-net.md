Title: gRPC Server Reflection in the .NET world
Published: 2020-07-08
Tags: 
- .NET
- gRPC
- Protocol Buffers
---
> gRPC Server Reflection is an optional extension for servers to assist clients in runtime construction of requests without having stub information precompiled into the client.
> 
> -- <cite>[gRPC Server Reflection Protocol](https://github.com/grpc/grpc/blob/master/doc/server-reflection.md)</cite>

The primary use case for gRPC Server Reflection is debugging and testing tools. Think cURL or Postman, but for gRPC. However there's another interesting use case, especially in the .NET world where we have code-first support through [protobuf-net.Grpc](https://github.com/protobuf-net/protobuf-net.Grpc).

If you've been using WCF you're probably familiar with a code-first approach that exposes metadata about the service using MEX or WSDL endpoints. These metadata endpoints could then be used to generate client stubs. Additionally, if you created a standard SOAP service, to be consumed over HTTP, interoperability with other programming languages was also possible by consuming the WSDL document. This is similar to what many of us do today with ASP.NET Core and OpenAPI. We create the controllers and models in C#, generate OpenAPI definition using [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) and then generate clients in TypeScript using [Autorest](https://github.com/Azure/autorest).

While Google's way of working with protobuf and gRPC has always been contract-first, [Marc Gravell's](https://github.com/mgravell) [protobuf-net](https://github.com/protobuf-net/protobuf-net) has always had code-first development as first priority. With Google's protobuf, you define your services and messages in .proto format and then use the protobuf compiler (protoc) to generate messages, client, and server stubs in the language of your choice. However, with protobuf-net you can start out with C#, F# or VB.NET code. There's no need for a .proto file. Code-first with protobuf-net is great if you don't need interoperability with other languages outside the .NET ecosystem, and even if you did, there have been ways to to get the contract generated in .proto format for message types. However, up until recently, if you needed the proto contract for gRPC services you were on your own. This was however fixed last weekend.

<blockquote class="twitter-tweet"><p lang="en" dir="ltr">Had a crap night&#39;s sleep, so I got up instead and looked at gRPC &quot;service discovery&quot; APIs; merged a great PR from <a href="https://twitter.com/mholo65?ref_src=twsrc%5Etfw">@mholo65</a> that adds the server bits, added interface=&gt;.proto generation, and added code-gen from live service to protogen (think &quot;mex&quot; in WCF terms); pretty happy!</p>&mdash; Marc Gravell (@marcgravell) <a href="https://twitter.com/marcgravell/status/1279713764212948997?ref_src=twsrc%5Etfw">July 5, 2020</a></blockquote> <script async src="https://platform.twitter.com/widgets.js" charset="utf-8"></script> 

protobuf-net.Grpc now supports [proto contract generation for services types](https://github.com/protobuf-net/protobuf-net.Grpc/issues/5#issuecomment-653854482) and [gRPC Server reflection](https://github.com/protobuf-net/protobuf-net.Grpc/pull/63).

## Generating .proto files from code-first services using gRPC server reflection

Let's take a look at how to use gRPC server reflection with protobuf-net.gRPC and how to generate .proto files using a command-line client. We start by creating a new ASP.NET Core application and install the NuGet packages needed:

```cmd
dotnet new web -n Reflection.Sample

cd Reflection.Sample

dotnet add package protobuf-net.Grpc.AspNetCore
dotnet add package protobuf-net.Grpc.AspNetCore.Reflection
```

We create a simple service and some messages.

```csharp
using System.ServiceModel;
using System.Threading.Tasks;
using ProtoBuf;

namespace Reflection.Sample
{
    [ServiceContract]
    public interface IGreeter
    {
        ValueTask<HelloReply> SayHello(HelloRequest request);
    }

    [ProtoContract]
    public class HelloRequest
    {
        [ProtoMember(1)]
        public string Name { get; set; }
    }

    [ProtoContract]
    public class HelloReply
    {
        [ProtoMember(1)]
        public string Message { get; set; }
    }

    public class Greeter : IGreeter
    {
        public ValueTask<HelloReply> SayHello(HelloRequest request)
        {
            return new ValueTask<HelloReply>(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }
    }
}
```

And then register our service in `Startup.cs`
<pre><code class="language-csharp">
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.Server;

namespace Reflection.Sample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            <span style="background-color: #FFFF00"><b>services.AddCodeFirstGrpc();</b></span>
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                <span style="background-color: #FFFF00"><b>endpoints.MapGrpcService&lt;Greeter&gt;();</b></span>
            });
        }
    }
}
</code></pre>

The last thing we need to do is to enable gRPC server reflection. This is also done in our `Startup.cs`
<pre><code class="language-csharp">
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProtoBuf.Grpc.Server;

namespace Reflection.Sample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCodeFirstGrpc();
            <span style="background-color: #FFFF00"><b>services.AddCodeFirstGrpcReflection();</b></span>
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService&lt;Greeter&gt;();
                <span style="background-color: #FFFF00"><b>endpoints.MapCodeFirstGrpcReflectionService();</b></span>
            });
        }
    }
}
</code></pre>

### Consuming the reflection endpoint

gRPC provides it's own CLI for consuming the server reflection API (https://github.com/grpc/grpc/blob/master/doc/command_line_tool.md) but it has some problems. You need to build it from source (at least I couldn't find a Windows binary) and it doesn't support generating .proto files. With this in mind, I decided to create [a .NET global tool](https://github.com/mholo65/dotnet-grpc-cli/) for this instead.

We start by installing the global tool from [NuGet](https://www.nuget.org/packages/dotnet-grpc-cli/).
```cmd
dotnet tool install -g dotnet-grpc-cli
```

And with our newly created gRPC service running, we'll run the tool to inspect which services are available:
```cmd
dotnet grpc-cli ls https://localhost:5001

Reflection.Sample.Greeter
grpc.reflection.v1alpha.ServerReflection
```

We can see the Greeter service we created, and also the server reflection service. As you can see, the reflection service is just another gRPC service. If you're curious, you can find the proto definition for the gRPC reflection service [here](https://github.com/grpc/grpc/blob/598e171746576c5398388a4c170ddf3c8d72b80a/src/proto/grpc/reflection/v1alpha/reflection.proto).

Now that we know the name of the services, we can list the methods.
```cmd
dotnet grpc-cli ls https://localhost:5001 Reflection.Sample.Greeter

filename: Reflection.Sample.Greeter.proto
package: Reflection.Sample
service Greeter {
  rpc SayHello(Reflection.Sample.HelloRequest) returns (Reflection.Sample.HelloReply) {}
}
```

And also dump the output in .proto format
```cmd
dotnet grpc-cli dump https://localhost:5001 Reflection.Sample.Greeter

---
File: Reflection.Sample.HelloRequest.proto
---
syntax = "proto3";
package Reflection.Sample;

message HelloRequest {
  string Name = 1;
}

---
File: Reflection.Sample.HelloReply.proto
---
syntax = "proto3";
package Reflection.Sample;

message HelloReply {
  string Message = 1;
}

---
File: Reflection.Sample.Greeter.proto
---
syntax = "proto3";
import "Reflection.Sample.HelloRequest.proto";
import "Reflection.Sample.HelloReply.proto";
package Reflection.Sample;

service Greeter {
   rpc SayHello(HelloRequest) returns (HelloReply);
}
```

We probably want this written to the filesystem, so we will then run the following.
```cmd
dotnet grpc-cli dump https://localhost:5001 Reflection.Sample.Greeter -o ./protos
```

That's it! We now have .proto files, generated from our server reflection endpoint. These .proto files can now be used to generate client stubs in the language of your choice.

### Summary
As you can see, gRPC server reflection can be very useful in code-first scenarios, but it's also useful for testing and debugging. gRPC CLI supports invoking methods, and this is something I'm also planning on supporting in the .NET global tool. It is also worth mentioning that [Protogen](https://www.nuget.org/packages/protobuf-net.Protogen/), which is a tool for generating protobuf-net source code from proto files, also recently [got support for generating source from gRPC server reflection endpoints](https://github.com/protobuf-net/protobuf-net/pull/676) with the new `--grpc` option.

Code-first using protobuf-net.Grpc is in my opinion the simplest, and most straightforward way to port over your old WCF services to .NET Core and gRPC. It's way more simpler than the official guide over at [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/architecture/grpc-for-wcf-developers/migrate-wcf-to-grpc) which uses Google's protobuf instead of protobuf-net. With protobuf-net.Grpc, you'll most likely get to keep all your data contracts intact, while only needing to add the `Order` property to your data member attributes. Your service contracts, however, will need some rework. But in the end, these are minimal changes compared to porting your data contracts and service contracts over to .proto format. If you also need interoperability from other programming languages, it's a peace of cake to generate .proto files from your gRPC service. So, what are you waiting for? Start porting over your WCF services to .NET Core now, and make sure to use protobuf-net.Grpc when you do it.