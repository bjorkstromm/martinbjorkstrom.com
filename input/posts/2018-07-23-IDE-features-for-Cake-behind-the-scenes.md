Title: IDE features for Cake, behind the scenes
Published: 2018-07-23
Tags:
- Cake
- OmniSharp
- Roslyn
---
## Introduction
In this post I'm going to walk you through the building blocks providing IDE features, such as autocompletion, code navigation, quick fixes, etc, when doing [Cake](https://cakebuild.net/) development in [Visual Studio Code](https://code.visualstudio.com/). Before we begin, if you haven't yet read [Mattias Karlsson's](https://www.devlead.se/) great post on the [layers and pieces of Cake](https://hackernoon.com/dispelling-the-magic-6dc0fdfe476c), do it now.

The building blocks providing the IDE features in Cake consists of the following pieces:
* [Bakery](https://github.com/cake-build/bakery), a tool for Cake script analysis and code generation. This tool basically takes a `.cake` file as input, and returns a Roslyn compatible C# script as output.
* [OmniSharp](https://github.com/OmniSharp/omnisharp-roslyn), a .NET development platform based on Roslyn workspaces. It provides project dependencies and language syntax to various IDE and plugins.
* [C# Extension for Visual Studio Code](https://github.com/OmniSharp/omnisharp-vscode), a visual studio extension that makes use of OmniSharp in order to provide IDE features for C#.

## Bakery
The Cake runner itself is a great tool for executing Cake scripts, but not a good tool for continuously reacting on text changes and producing Roslyn compatible C# scripts on every change. Since it's relying heavily on reflection, stuff like loading addins would easily turn into dependency hell the moment a user would like to update an addin, causing multiple versions of the same assembly trying to be loaded into to app domain. The best solution would be to use reflection only load and/or use separate app domains when inspecting addins. Caching is also something the Cake runner is lacking, since it simply doesn't need such functionality.

Due to the problems above, we, the Cake team, came to the conclusion that creating a simple tool for Cake script analysis and code generation would be the best option in short-term, and then slowly combining efforts of the tool back to the Cake runner. [Patrik Svensson](https://www.patriksvensson.se/) also came up with the idea of using [Mono.Cecil](https://github.com/jbevain/cecil/) instead of reflection for inspecting addins. This way we didn't need to worry about app domains and/or use reflection only load, which by the time didn't work on .NET Core (don't know if it still does), a target we wanted to support at that time. However, Cake Bakery was born (pun intended).

Bakery depends on `Cake.Core` and `Cake.NuGet`, thus script preprocessing, analysis, and addin installation works same way as in the Cake runner. The main thing that differs from the standard Cake runner is that Bakery uses Mono.Cecil for addin inspection and also provides caching, which is a good thing to have when the same script is analyzed over and over again. At the time of writing, Cake Bakery exposes a binary interface over a TCP socket. This is currently the only way of communicating with Bakery. Other interfaces and/or transport mechanisms, such as named pipes, or JSON over STDIO might be implemented in the future.