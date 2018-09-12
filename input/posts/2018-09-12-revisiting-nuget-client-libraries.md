Title: Revisiting the NuGet v3 Libraries 
Published: 2018-09-12
Tags: 
- Open Source
- .NET
- NuGet
---

# Background
This is a follow up post on Dave Glick's three part blog post ["Exploring the NuGet v3 Libraries"](https://daveaglick.com/posts/exploring-the-nuget-v3-libraries-part-1). To this day, these are the only documentation provided for the NuGet Client SDK, and are even being referenced from [Microsoft official documentation](https://docs.microsoft.com/en-us/nuget/reference/nuget-client-sdk). I found Dave's blog posts very valuable when implementing the [in-process NuGet Client for Cake](https://github.com/cake-build/cake/pull/1768/files), and I have used Dave as a sounding board for everything NuGet related ever since. Thank you Dave, for being such a great guy!

Anyhow, Dave's posts focuses on using the [NuGet.PackageManagement](https://www.nuget.org/packages/NuGet.PackageManagement/) bits for installing packages. This has it's caveats, for example:
* [NuGet.PackageManagement](https://www.nuget.org/packages/NuGet.PackageManagement/) is one of the few NuGet client library that doesn't target netstandard. This is a no-go if you'd like to install NuGet packages from an .NET Core application. Fortunately, [Eli Arbel](https://twitter.com/aelij) have created a [unofficial netstandard port](https://www.nuget.org/packages/NuGet.PackageManagement.NetStandard/), which I nowadays also help maintain. Maintaining a fork is however painful, and requires some extra work to keep up with the official NuGet releases.
* It's tightly coupled with the .NET project system, which I really don't care about.
* It makes you implement [ugly workarounds](https://github.com/cake-build/cake/tree/17488d0c5bd2ca8a4a6815cd2f0bb307ee17e9ac/src/Cake.NuGet/Install) in order to tweak the libraries to work as you want 😄

# Alternative approaches
> There's more than one way to skin a cat. But even more ways to install NuGet packages.
>
> -- <cite>Anonymous</cite>

One alternative approach I've looked at is using the [NuGet.Commands](https://www.nuget.org/packages/NuGet.Commands), which contains _"Complete commands common to command-line and GUI NuGet clients"_ but doesn't depend on NuGet.PackageManagement, (weird, huh?). The reason why I didn't investigate this further is that it also seems to be tightly coupled with the .NET project system. A third alternative would simply be to use the [NuGet API](https://docs.microsoft.com/en-us/nuget/api/overview) directly. I've explored the alternative, but it would require quite a lot of effort to get something like package installation working.

# Going deeper
After reading through the NuGet.Client source on [Github](https://github.com/NuGet/NuGet.Client) and the NuGet API specification I have learned an easier way to install NuGet packages. This approach doesn't rely on NuGet.PackageManagement, NuGet.Commands nor NuGet.ProjectModel, thus is completely decoupled from the .NET project system. I'm currently investigating this approach in [Depends](https://github.com/mholo65/depends) while working on a new feature, and will most probably also implement it in Cake. The approach we are going to look at is "only" depending in the following NuGet libraries:
* [`NuGet.Common`](https://www.nuget.org/packages/NuGet.Common/) - Includes some common types needed, such as NuGet's own logger implementation.
* [`NuGet.Configuration`](https://www.nuget.org/packages/NuGet.Configuration/) - NuGet's client configuration settings implementation.
* [`NuGet.Frameworks`](https://www.nuget.org/packages/NuGet.Frameworks/) - The understanding of different target framework monikers.
* [`NuGet.Packaging`](https://www.nuget.org/packages/NuGet.Packaging/) - NuGet's implementation for reading nupkg package and nuspec package specification files.
* [`NuGet.Packaging.Core`](https://www.nuget.org/packages/NuGet.Packaging/) - The core data structures for NuGet.Packaging.
* [`NuGet.Protocol`](https://www.nuget.org/packages/NuGet.Protocol) - The NuGet protocol implementation. Supports both V2 and V3 feeds.
* [`NuGet.Resolver`](https://www.nuget.org/packages/NuGet.Resolver) - NuGet's dependency resolver.
* [`NuGet.Versioning`](https://www.nuget.org/packages/NuGet.Versioning) - NuGet's implementation of Semantic Versioning.

## Resolving package and its dependencies

```csharp
var packageId = "cake.nuget";
var version = "0.30.0";
var framework = "net46";

var package = new PackageIdentity(packageId, NuGetVersion.Parse(version));
var settings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());
var nuGetFramework = NuGetFramework.ParseFolder(framework);
var logger = NullLogger.Instance;

using (var cacheContext = new SourceCacheContext())
{
    foreach (var sourceRepository in sourceRepositoryProvider.GetRepositories())
    {
        var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
        var dependencyInfo = await dependencyInfoResource.ResolvePackage(
            package, nuGetFramework, cacheContext, logger, CancellationToken.None);
        
        if (dependencyInfo != null)
        {
            Console.WriteLine(dependencyInfo);
        }
    }
}
```

```
Cake.NuGet.0.30.0 : Cake.Core [0.30.0, ), Newtonsoft.Json [11.0.2, ), NuGet.Frameworks [4.7.0, ), NuGet.PackageManagement [4.7.0, ), NuGet.ProjectModel [4.7.0, ), NuGet.Versioning [4.7.0, )
```