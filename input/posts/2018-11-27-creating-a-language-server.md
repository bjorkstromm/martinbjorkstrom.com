Title: Creating a language server using .NET
Published: 2018-11-26
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
In this demo, we are going to create a Language Server for `*.csproj` which enables autocomplete for `<PackageReference>` elements. We are going to focus on integrating it with Visual Studio Code, but since LSP is supported by a wide range of IDE´s and editors, the effort for integrating it with any other editor should be minimal. To create a Language Server using .NET, we are going to use [OmniSharp.Extensions.LanguageServer](https://www.nuget.org/packages/OmniSharp.Extensions.LanguageServer/), which is a C# implementation of the LSP protocol, maintained by the [OmniSharp](http://www.omnisharp.net/) team.

For parsing XML, we are going to use [Kirill Osenkov's XmlParser](https://github.com/KirillOsenkov/XmlParser). You may think that using `XmlReader` or `LINQ to XML` would be sufficient, this is however not true. The first and most important rule of implementing a Language Server, is that you'll need an error tolerant parser as most of the time the code in the editor is incomplete and syntactically incorrect. Microsoft left some valuable notes [here](https://github.com/Microsoft/tolerant-php-parser/blob/master/docs/HowItWorks.md) when they created the tolerant PHP parser, which currently backs PHP support in Visual Studio Code. Again, don't parse the files yourself (unless you know what you are doing), use a proper parser to get an Abstract Syntax Tree (AST).





## Credits

## Example code
