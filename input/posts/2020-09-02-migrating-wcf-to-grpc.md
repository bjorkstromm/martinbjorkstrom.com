Title: Migrating WCF to gRPC - The protobuf-net way
Published: 2020-09-02
Tags: 
- .NET
- gRPC
- Protocol Buffers
- WCF
---

In my [previous blog post](2020-07-08-grpc-reflection-in-net), I mentioned that it's straightforward to migrate your WCF services to gRPC using [protobuf-net.Grpc](https://github.com/protobuf-net/protobuf-net.Grpc/). In this blog post, we are going to look at how easy it actually is. Microsoft's [official guide for migrating WCF services to gRPC](https://docs.microsoft.com/en-us/dotnet/architecture/grpc-for-wcf-developers/migrate-wcf-to-grpc) only mentions the Google.Protobuf approach, which can be time consuming if you have a lot of data contracts that need to be migrated to `.proto` format. However, by using [protobuf-net.Grpc](https://github.com/protobuf-net/protobuf-net.Grpc/) we are be able to reuse the old WCF data contracts and service contracts with minimal code changes.

# Migrate a WCF request-reply service to a gRPC unary RPC

_This chapter is based on the ["Migrate a WCF request-reply service to a gRPC unary RPC"](https://docs.microsoft.com/en-us/dotnet/architecture/grpc-for-wcf-developers/migrate-request-reply) section in Microsoft's documentation. Please read that guide for a better understanding of the original WCF service._

## Migrating the Data Contracts and Service Contracts

The WCF service is a simple request-reply Portfolio service that let's you download either a single portfolio or all portfolios for a given trader. The service and data contracts are defined as following:

```csharp
[ServiceContract]
public interface IPortfolioService
{
    [OperationContract]
    Task<Portfolio> Get(Guid traderId, int portfolioId);

    [OperationContract]
    Task<List<Portfolio>> GetAll(Guid traderId);
}
```
```csharp
[DataContract]
public class Portfolio
{
    [DataMember]
    public int Id { get; set; }

    [DataMember]
    public Guid TraderId { get; set; }

    [DataMember]
    public List<PortfolioItem> Items { get; set; }
}

[DataContract]
public class PortfolioItem
{
    [DataMember]
    public int Id { get; set; }

    [DataMember]
    public int ShareId { get; set; }

    [DataMember]
    public int Holding { get; set; }

    [DataMember]
    public decimal Cost { get; set; }
}
```

Before migrating the Data Contracts and Service Contract to gRPC, I'd recommend creating a new class library for the shared contracts. These contracts can then be easily shared between the Server and the Client either via a project reference or package reference, depending on the structure of you WCF solution. Once we have created the class library we'll just copy over the source files and start migrating to gRPC.

The Data Contracts, unlike when migrating to gRPC using Google.Protobuf, will require minimal changes. The only thing we need to do is to define the `Order` property in the `DataMember` attribute. This is equivalent of defining field numbers when creating messages in `.proto` format. These field numbers are used to identify your fields in the message binary format, and should not be changed once your message type is in use. For more information about field numbers, see the [Protocol Buffers language guide](https://developers.google.com/protocol-buffers/docs/proto3#assigning_field_numbers).

<pre><code class="language-csharp">
[DataContract]
public class Portfolio
{
    [DataMember<span style="background-color: #FFFF00"><b>(Order = 1)</b></span>]
    public int Id { get; set; }

    [DataMember<span style="background-color: #FFFF00"><b>(Order = 2)</b></span>]
    public Guid TraderId { get; set; }

    [DataMember<span style="background-color: #FFFF00"><b>(Order = 3)</b></span>]
    public List&lt;PortfolioItem&gt; Items { get; set; }
}

[DataContract]
public class PortfolioItem
{
    [DataMember<span style="background-color: #FFFF00"><b>(Order = 1)</b></span>]
    public int Id { get; set; }

    [DataMember<span style="background-color: #FFFF00"><b>(Order = 2)</b></span>]
    public int ShareId { get; set; }

    [DataMember<span style="background-color: #FFFF00"><b>(Order = 3)</b></span>]
    public int Holding { get; set; }

    [DataMember<span style="background-color: #FFFF00"><b>(Order = 4)</b></span>]
    public decimal Cost { get; set; }
}
</code></pre>

The Service Contract, due to differences between gRPC and WCF, will however require slightly more modification. A RPC method in a gRPC service must define only one message type as request parameter and return only a single message. We can't accept a [scalar types](https://developers.google.com/protocol-buffers/docs/proto3#scalar) (i.e. primitive types) as request parameter and we can't return a scalar type. Until [Issue 70](https://github.com/protobuf-net/protobuf-net.Grpc/issues/70) is resolved, we need to merge all primitive parameters into a single message (i.e. `DataContract`). This also accounts for `Guid` parameter type, since it might be serialized as `string` depending on how you configure protobuf-net. We also can't accept a list of messages (or scalars) or return a list of messages (or scalars). With these rules in mind we need to modify our Service Contract to look something like the following:

<pre><code class="language-csharp">
[ServiceContract]
public interface IPortfolioService
{
    [OperationContract]
    Task&lt;Portfolio&gt; Get(<span style="background-color: #FFFF00"><b>GetPortfolioRequest request</b></span>);

    [OperationContract]
    Task&lt;<span style="background-color: #FFFF00"><b>PortfolioCollection</b></span>&gt; GetAll(<span style="background-color: #FFFF00"><b>GetAllPortfoliosRequest request</b></span>);
}
</code></pre>

The above changes in the service contract forces us to create some additional data contracts. So we create the following:
```csharp
[DataContract]
public class GetPortfolioRequest
{
    [DataMember(Order = 1)]
    public Guid TraderId { get; set; }

    [DataMember(Order = 2)]
    public int PortfolioId { get; set; }
}

[DataContract]
public class GetAllPortfoliosRequest
{
    [DataMember(Order = 1)]
    public Guid TraderId { get; set; }
}

[DataContract]
public class PortfolioCollection
{
    [DataMember(Order = 1)]
    public List<Portfolio> Items { get; set; }
}
```

That's basically it. Now we have migrated our WCF service contract and data contracts to gRPC. Next step is to migrate the data layer to .NET Core.

## Migrate the PortfolioData library to .NET Core 

Next up, we'll migrate the PortfolioData library to .NET Core, as described in Microsoft's guide [here](https://docs.microsoft.com/en-us/dotnet/architecture/grpc-for-wcf-developers/migrate-request-reply#migrate-the-portfoliodata-library-to-net-core). We will however not copy over the models (`Portfolio.cs` and `PortfolioItem.cs`) because these are already defined in the class library we created in the previous chapter. Instead we'll add a project reference to that shared library. Next step is to migrate the WCF service to an ASP.NET Core Application.

## Migrate the WCF Service to an ASP.NET Core application

The first thing we need to do is to create an ASP.NET Core application. So either start your favorite IDE and create a basic ASP.NET Core application or just simply run `dotnet new web` from the command-line. Next, we need to add a reference to the [protobuf-net.Grpc.AspNetCore](https://www.nuget.org/packages/protobuf-net.Grpc.AspNetCore/) NuGet package. Install it using you favorite package manager, or simply run `dotnet add package protobuf-net.Grpc.AspNetCore`. We'll also need to add a project reference to the PortfolioData library we created in the previous chapter.

Now that we have the project in place, and all dependencies added, we can go ahead and create the portfolio service. Create a new class with the following content.

```csharp
public class PortfolioService : IPortfolioService
{
    private readonly IPortfolioRepository _repository;

    public PortfolioService(IPortfolioRepository repository)
    {
        _repository = repository;
    }

    public async Task<Portfolio> Get(GetPortfolioRequest request)
    {
        var portfolio = await _repository.GetAsync(request.TraderId, request.PortfolioId);

        return portfolio;
    }

    public async Task<PortfolioCollection> GetAll(GetAllPortfoliosRequest request)
    {
        var portfolios = await _repository.GetAllAsync(request.TraderId);

        var response = new PortfolioCollection
        {
            Items = portfolios
        };

        return response;
    }
}
```

The above service looks very similar to the WCF service implementation, with the exception of the input parameter types and the return parameter types.

Last but not least, we need to wire up protobuf-net.Grpc in the ASP.NET Core pipeline and register the portfolio repository in the DI container. In `Startup.cs`, we'll do the following additions:

<pre><code class="language-csharp">
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        <span style="background-color: #FFFF00"><b>services.AddScoped&lt;IPortfolioRepository, PortfolioRepository&gt;();</b></span>
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
            <span style="background-color: #FFFF00"><b>endpoints.MapGrpcService&lt;PortfolioService&gt;();</b></span>
        });
    }
}
</code></pre>

Now that we have the gRPC service in place. The next thing on our list is to create a client application.

## Create a gRPC client application

For our client application, we'll go ahead and create a console application. Either create one using your favorite IDE or simply run `dotnet new console` from the command-line. Next, we need to add a reference to the [protobuf-net.Grpc](https://www.nuget.org/packages/protobuf-net.Grpc/) and [Grpc.Net.Client](https://www.nuget.org/packages/Grpc.Net.Client/) NuGet packages. Install them using your favorite package manager, or simply run `dotnet add package protobuf-net.Grpc` and `dotnet add package Grpc.Net.Client` from the command-line. We'll also need to add a project reference to the shared library we created in the [first chapter](#migrating-the-data-contracts-and-service-contracts).

In our `Program.cs` we'll add the following code in order to create a gRPC client and communicate with our gRPC service.
```csharp
class Program
{
    private const string ServerAddress = "https://localhost:5001";

    static async Task Main()
    {
        var channel = GrpcChannel.ForAddress(ServerAddress);
        var portfolios = channel.CreateGrpcService<IPortfolioService>();

        try
        {
            var request = new GetPortfolioRequest
            {
                TraderId = Guid.Parse("68CB16F7-42BD-4330-A191-FA5904D2E5A0"),
                PortfolioId = 42
            };
            var response = await portfolios.Get(request);

            Console.WriteLine($"Portfolio contains {response.Items.Count} items.");
        }
        catch (RpcException e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}
```

Now we can test our implementation by first starting up the ASP.NET Core application and then start our console application.

# Migrate WCF duplex services to gRPC

_This chapter is based on the ["Migrate WCF duplex services to gRPC"](https://docs.microsoft.com/en-us/dotnet/architecture/grpc-for-wcf-developers/migrate-duplex-services) section in Microsoft's documentation. Please read that guide for a better understanding of the original WCF service._

Now that we have covered the basics with migrating WCF services to gRPC using protobuf-net.Grpc, we can look at some more complicated samples using gRPC streaming.

## Migrating the Data Contracts and Service Contracts

In this chapter we are going to look at the SimpleStockPriceTicker, which is a duplex service for which the client starts the connection with a list of stock symbols, and the server uses the callback interface to send updates as they become available. The WCF service has a single method with no return type because it uses the callback interface `ISimpleStockTickerCallback` to send data to the client in real time.

```csharp
[ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ISimpleStockTickerCallback))]
public interface ISimpleStockTickerService
{
    [OperationContract(IsOneWay = true)]
    void Subscribe(string[] symbols);
}

[ServiceContract]
public interface ISimpleStockTickerCallback
{
    [OperationContract(IsOneWay = true)]
    void Update(string symbol, decimal price);
}
```



## Summary
As always, the complete sample code is available on GitHub, [here](https://github.com/mholo65/grpc-for-wcf-developers/).