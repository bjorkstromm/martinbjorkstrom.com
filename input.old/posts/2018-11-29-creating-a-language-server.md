Title: Creating a language server using .NET
Published: 2018-11-29
Tags: 
- Open Source
- .NET
- LSP
---
## Background
> A Language Server is meant to provide the language-specific smarts and communicate with development tools over a protocol that enables inter-process communication.
> 
> The idea behind the Language Server Protocol (LSP) is to standardize the protocol for how such servers and development tools communicate. This way, a single Language Server can be re-used in multiple development tools, which in turn can support multiple languages with minimal effort.
> 
> -- <cite>[Language Server Protocol](https://microsoft.github.io/language-server-protocol/)</cite>

LSP is a protocol originally developed by Microsoft for Visual Studio Code, which has evolved into an open standard that is supported by a wide range of editors and IDE's, including Visual Studio, Visual Studio Code, Eclipse, Atom, vim and emacs. The specification can be found on [GitHub](https://github.com/Microsoft/language-server-protocol) and through the [official LSP website](https://microsoft.github.io/language-server-protocol/specification). Visual Studio Code docs include a sample on [how to create a language server](https://code.visualstudio.com/docs/extensions/example-language-server) using Node.js®. But if you, like me, shouldn't be trusted with JavaScript, I have good news for you. In the rest of this blog post I'll walk you through the process of creating a Language Server supporting LSP using C# and .NET Core.

## Language Server Implementation
In this sample, we are going to create a Language Server for `*.csproj` which enables autocomplete for `<PackageReference>` elements. We are going to focus on integrating it with Visual Studio Code, but since LSP is supported by a wide range of IDE´s and editors, the effort for integrating it with any other editor should be minimal. To create a Language Server using .NET, we are going to use [OmniSharp.Extensions.LanguageServer](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer/), which is a C# implementation of the LSP, authored by [David Driscoll](https://github.com/david-driscoll) member of the [OmniSharp](http://www.omnisharp.net/) team.

For parsing XML, we are going to use [Kirill Osenkov's XmlParser](https://github.com/KirillOsenkov/XmlParser). You may think that using `XmlReader` or `LINQ to XML` would be sufficient, this is however not true. The first and most important rule of implementing a Language Server, is that you'll need an error tolerant parser as most of the time the code in the editor is incomplete and syntactically incorrect. Microsoft left some valuable notes [here](https://github.com/Microsoft/tolerant-php-parser/blob/master/docs/HowItWorks.md) when they created the tolerant PHP parser, which currently backs PHP support in Visual Studio Code. Again, don't parse the files yourself (unless you know what you are doing), use a proper parser to get an Abstract Syntax Tree (AST).

The full sample which we'll create in the rest of this blog post is available on GitHub at [https://github.com/mholo65/lsp-example/tree/blog-post](https://github.com/mholo65/lsp-example/tree/blog-post).

### Creating the server
First, well start of by creating a new .NET Core console application.
```cmd
dotnet new console -n Server
```
Then we'll add the dependencies (the latter is just for the XmlParser)
```cmd
dotnet add .\Server\Server.csproj package OmniSharp.Extensions.LanguageServer --version 0.10.0
dotnet add .\Server\Server.csproj package GuiLabs.Language.Xml --version 1.2.27
```
First thing we'll need to do is to implement an `ITextDocumentSyncHandler`. This is a handler which will handle the LSP Text Synchronization notifications [`didOpen`](https://microsoft.github.io/language-server-protocol/specification#textDocument_didOpen), [`textDocument/didChange`](https://microsoft.github.io/language-server-protocol/specification#textDocument_didChange), [`textDocument/didSave`](https://microsoft.github.io/language-server-protocol/specification#textDocument_didSave) and [`textDocument/didClose`](https://microsoft.github.io/language-server-protocol/specification#textDocument_didClose). `textDocument/didChange` is fundamental for a Language Server as this is where all document changes will end up as the end user is writing code. When registering the `textDocument/didChange` notification handler, we'll have the possibility to select either `Full` or `Incremental` as `syncKind`. For simplicity, in this sample, we'll register to receive the full document text on every text change. In real world scenarios, for performance reasons, I'd strongly suggest registering for receiving incremental updates.

To have the buffer available for other handlers, we'll create a `BufferManager` whose main task is to always contain the latest version of a document. For simplicity, in this sample, we'll just use a `ConcurrentDictionary` as the backing store which will just contain the full text for each document (with the document path as key). For real world scenarios, you'd most probably want to also parse the document upon each change and publish diagnostics as they occur (see [`textDocument/publishDiagnostics`](https://microsoft.github.io/language-server-protocol/specification#textDocument_publishDiagnostics) and [`PublishDiagnosticsExtensions.cs`](https://github.com/OmniSharp/csharp-language-server-protocol/blob/v0.10.0/src/Protocol/Document/Server/PublishDiagnosticsExtensions.cs))

_BufferManager.cs_
```csharp
class BufferManager
{
    private ConcurrentDictionary<string, Buffer> _buffers = new ConcurrentDictionary<string, Buffer>();

    public void UpdateBuffer(string documentPath, Buffer buffer)
    {
        _buffers.AddOrUpdate(documentPath, buffer, (k, v) => buffer);
    }

    public Buffer GetBuffer(string documentPath)
    {
        return _buffers.TryGetValue(documentPath, out var buffer) ? buffer : null;
    }
}
```

_TextDocumentSyncHandler.cs_
```csharp
class TextDocumentSyncHandler : ITextDocumentSyncHandler
{
    private readonly ILanguageServer _router;
    private readonly BufferManager _bufferManager;

    private readonly DocumentSelector _documentSelector = new DocumentSelector(
        new DocumentFilter()
        {
            Pattern = "**/*.csproj"
        }
    );

    private SynchronizationCapability _capability;

    public TextDocumentSyncHandler(ILanguageServer router, BufferManager bufferManager)
    {
        _router = router;
        _bufferManager = bufferManager;
    }

    public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

    public TextDocumentChangeRegistrationOptions GetRegistrationOptions()
    {
        return new TextDocumentChangeRegistrationOptions()
        {
            DocumentSelector = _documentSelector,
            SyncKind = Change
        };
    }

    public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
    {
        return new TextDocumentAttributes(uri, "xml");
    }

    public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var documentPath = request.TextDocument.Uri.ToString();
        var text = request.ContentChanges.FirstOrDefault()?.Text;

        _bufferManager.UpdateBuffer(documentPath, new StringBuffer(text));

        _router.Window.LogInfo($"Updated buffer for document: {documentPath}\n{text}");

        return Unit.Task;
    }

    public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        _bufferManager.UpdateBuffer(request.TextDocument.Uri.ToString(), new StringBuffer(request.TextDocument.Text));
        return Unit.Task;
    }
    ...
}
```

Last thing we'll need to do is to configure the Language Server and start it up in `Program.cs`.

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithLoggerFactory(new LoggerFactory())
                .AddDefaultLoggingProvider()
                .WithMinimumLogLevel(LogLevel.Trace)
                .WithServices(ConfigureServices)
                .WithHandler<TextDocumentSyncHandler>()
             );

        await server.WaitForExit;
    }

    static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<BufferManager>();
     }
}
```

### Creating the Completion Handler
Now, when we are able to react on document changes, we are able to implement handlers for any Language Feature of the LSP. In this sample we'll implement a handler for the [`textDocument/completion`](https://microsoft.github.io/language-server-protocol/specification#textDocument_completion) request. This is easily done in .NET by implementing the `ICompletionHandler` interface. When registering the completion handler, we'll have the possibility to also register a [`completionItem/resolve`](https://microsoft.github.io/language-server-protocol/specification#completionItem_resolve) handler (`ICompletionResolveHandler`). For simplicity, in this sample, we'll not use a completion resolver handler. This might be useful in real world scenarios when you'll want to return a list of completion items as quickly as possible, and later return additional information about the completion items upon request. E.g. with `<PackageReference>` completions, we could return the matching package ID's directly, and resolve package description upon request in the resolve handler.

To provide autocomplete for NuGet packages, we'll use the [Autcomplete API](https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource) which is part of the [NuGet V3 API](https://docs.microsoft.com/en-us/nuget/api/overview). This exposes a simple service to search for package ID's and enumerating package versions.

_CompletionHandler.cs_
```csharp
class CompletionHandler : ICompletionHandler
{
    private const string PackageReferenceElement = "PackageReference";
    private const string IncludeAttribute = "Include";
    private const string VersionAttribute = "Version";

    private readonly ILanguageServer _router;
    private readonly BufferManager _bufferManager;
    private readonly NuGetAutoCompleteService _nuGetService;

    private readonly DocumentSelector _documentSelector = new DocumentSelector(
        new DocumentFilter()
        {
            Pattern = "**/*.csproj"
        }
    );

    private CompletionCapability _capability;

    public CompletionHandler(ILanguageServer router, BufferManager bufferManager, NuGetAutoCompleteService nuGetService)
    {
        _router = router;
        _bufferManager = bufferManager;
        _nuGetService = nuGetService;
    }

    public CompletionRegistrationOptions GetRegistrationOptions()
    {
        return new CompletionRegistrationOptions
        {
            DocumentSelector = _documentSelector,
            ResolveProvider = false
        };
    }

    public async Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        var documentPath = request.TextDocument.Uri.ToString();
        var buffer = _bufferManager.GetBuffer(documentPath);

        if (buffer == null)
        {
            return new CompletionList();
        }

        var syntaxTree = Parser.Parse(buffer);

        var position = GetPosition(buffer.GetText(0, buffer.Length),
            (int)request.Position.Line,
            (int)request.Position.Character);

        var node = syntaxTree.FindNode(position);

        var attribute = node.AncestorNodes().OfType<XmlAttributeSyntax>().FirstOrDefault();
        if (attribute != null && node.ParentElement.Name.Equals(PackageReferenceElement))
        {
            if (attribute.Name.Equals(IncludeAttribute))
            {
                var completions = await _nuGetService.GetPackages(attribute.Value);

                var diff = position - attribute.ValueNode.Start;

                return new CompletionList(completions.Select(x => new CompletionItem
                {
                    Label = x,
                    Kind = CompletionItemKind.Reference,
                    TextEdit = new TextEdit
                    {
                        NewText = x,
                        Range = new Range(
                            new Position
                            {
                                Line = request.Position.Line,
                                Character = request.Position.Character - diff + 1
                            }, new Position
                            {
                                Line = request.Position.Line,
                                Character = request.Position.Character - diff + attribute.ValueNode.Width - 1
                            })
                    }
                }), isIncomplete: completions.Count > 1);
            }
            else if (attribute.Name.Equals(VersionAttribute))
            {
                var includeNode = node.ParentElement.Attributes.FirstOrDefault(x => x.Name.Equals(IncludeAttribute));

                if (includeNode != null && !string.IsNullOrEmpty(includeNode.Value))
                {
                    var versions = await _nuGetService.GetPackageVersions(includeNode.Value, attribute.Value);

                    var diff = position - attribute.ValueNode.Start;

                    return new CompletionList(versions.Select(x => new CompletionItem
                    {
                        Label = x,
                        Kind = CompletionItemKind.Reference,
                        TextEdit = new TextEdit
                        {
                            NewText = x,
                            Range = new Range(
                                new Position
                                {
                                    Line = request.Position.Line,
                                    Character = request.Position.Character - diff + 1
                                }, new Position
                                {
                                    Line = request.Position.Line,
                                    Character = request.Position.Character - diff + attribute.ValueNode.Width - 1
                                })
                        }
                    }));
                }
            }
        }

        return new CompletionList();
    }

    private static int GetPosition(string buffer, int line, int col)
    {
        var position = 0;
        for (var i = 0; i < line; i++)
        {
            position = buffer.IndexOf('\n', position) + 1;
        }
        return position + col;
    }

    public void SetCapability(CompletionCapability capability)
    {
        _capability = capability;
    }
}
```

_NuGetAutoCompleteService.cs_
```csharp
class NuGetAutoCompleteService
{
    private HttpClient _client = new HttpClient();

    public async Task<IReadOnlyCollection<string>> GetPackages(string query)
    {
        var response = await _client.GetStringAsync($"https://api-v2v3search-0.nuget.org/autocomplete?q={query}");
        return JObject.Parse(response)["data"].ToObject<List<string>>();
    }

    public async Task<IReadOnlyCollection<string>> GetPackageVersions(string package, string version)
    {
        var response = await _client.GetStringAsync($"https://api-v2v3search-0.nuget.org/autocomplete?id={package}");
        return JObject.Parse(response)["data"].ToObject<List<string>>();
    }
}
```

And last we'll just hook up our completion handler and the NuGet completion service in `Program.cs`.

<pre><code class="language-csharp">
class Program
{
    static async Task Main(string[] args)
    {
        var server = await LanguageServer.From(options =>
            options
                .WithInput(Console.OpenStandardInput())
                .WithOutput(Console.OpenStandardOutput())
                .WithLoggerFactory(new LoggerFactory())
                .AddDefaultLoggingProvider()
                .WithMinimumLogLevel(LogLevel.Trace)
                .WithServices(ConfigureServices)
                .WithHandler&lt;TextDocumentSyncHandler&gt;()
                <span style="background-color: #FFFF00"><b>.WithHandler&lt;CompletionHandler&gt;()</b></span>
            );

        await server.WaitForExit;
    }

    static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton&lt;BufferManager&gt;();
        <span style="background-color: #FFFF00"><b>services.AddSingleton&lt;NuGetAutoCompleteService&gt;();</b></span>
    }
}
</code></pre>

### Creating the client
Creating a Visual Studio Code Extension, aka the LSP client which utilizes our Language Server, is quite straightforward. We need to specify an activation event in `package.json` and then create a LSP client which starts the language server in our `activate` function. In this sample, we'll activate the extension when a XML file is opened and then configure our LSP client to synchronize any changes made to `.csproj` files with the Language Server.

_package.json_
```json
{
    "name": "client",
    "displayName": "Client",
    "description": "Example LSP client",
    "publisher": "mholo65",
    "version": "0.0.1",
    "engines": {
        "vscode": "^1.29.0"
    },
    "categories": [
        "Other"
    ],
    "activationEvents": [
        "onLanguage:xml"
    ],
    "main": "./out/extension",
    "contributes": {},
    ...
}

```

_extension.ts_
```typescript
'use strict';

import { workspace, Disposable, ExtensionContext } from 'vscode';
import { LanguageClient, LanguageClientOptions, SettingMonitor, ServerOptions, TransportKind, InitializeParams } from 'vscode-languageclient';
import { Trace } from 'vscode-jsonrpc';

export function activate(context: ExtensionContext) {

    // The server is implemented in node
    let serverExe = 'dotnet';

    // If the extension is launched in debug mode then the debug server options are used
    // Otherwise the run options are used
    let serverOptions: ServerOptions = {
        run: { command: serverExe, args: ['/path/to/Server.dll'] },
        debug: { command: serverExe, args: ['/path/to/Server.dll'] }
    }

    // Options to control the language client
    let clientOptions: LanguageClientOptions = {
        // Register the server for plain text documents
        documentSelector: [
            {
                pattern: '**/*.csproj',
            }
        ],
        synchronize: {
            // Synchronize the setting section 'languageServerExample' to the server
            configurationSection: 'languageServerExample',
            fileEvents: workspace.createFileSystemWatcher('**/*.csproj')
        },
    }

    // Create the language client and start the client.
    const client = new LanguageClient('languageServerExample', 'Language Server Example', serverOptions, clientOptions);
    client.trace = Trace.Verbose;
    let disposable = client.start();

    // Push the disposable to the context's subscriptions so that the
    // client can be deactivated on extension deactivation
    context.subscriptions.push(disposable);
}
```

### Profit
If you've read this far and maybe checked out the code in the [sample repository](https://github.com/mholo65/lsp-example/tree/blog-post), you should have a Visual Studio Code extension which adds autocomplete functionality for package references in `.csproj` files, just like in the tweet below.
<blockquote class="twitter-tweet" data-conversation="none" data-lang="en"><p lang="en" dir="ltr">Now using XmlParser. Code&#39;s much cleaner, and it was a piece of cake to also get autocomplete for version. Btw, source is here <a href="https://t.co/cUnKGliC4T">https://t.co/cUnKGliC4T</a> <a href="https://t.co/ac3LizlY4S">pic.twitter.com/ac3LizlY4S</a></p>&mdash; Martin Björkström (@mholo65) <a href="https://twitter.com/mholo65/status/1066815193718669313?ref_src=twsrc%5Etfw">November 25, 2018</a></blockquote>
<script async src="https://platform.twitter.com/widgets.js" charset="utf-8"></script>

&nbsp;
## Credits and Resources
If you think `<PackageReference>` autocomplete is cool, then you should definitely check out [Adam Friedman's](https://github.com/tintoy) [MSBuild project tools](https://marketplace.visualstudio.com/items?itemName=tintoy.msbuild-project-tools) extension for Visual Studio Code. The extension includes `<PackageReference>` autocomplete and a bunch of other useful tools for MSBuild project files. The source for the Language Server (which uses the same LSP libraries as used in this sample) is available on [GitHub](https://github.com/tintoy/msbuild-project-tools-server). Other examples of language servers implemented in .NET, using the same awesome [OmniSharp.Extensions.LanguageServer](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer/) libraries are [Razor for VSCode](https://github.com/aspnet/Razor.VSCode/tree/master/src/Microsoft.AspNetCore.Razor.LanguageServer) and [LSP support for OmniSharp Roslyn](https://github.com/OmniSharp/omnisharp-roslyn/tree/master/src/OmniSharp.LanguageServerProtocol).

If you're curious about LSP support in .NET or have additional questions, please come hang out with Adam, David and me in the [OmniSharp Slack](https://omnisharp.herokuapp.com/) `#lsp` channel.