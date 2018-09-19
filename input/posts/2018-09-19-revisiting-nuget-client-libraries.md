Title: Revisiting the NuGet v3 Libraries 
Published: 2018-09-19
Tags: 
- Open Source
- .NET
- NuGet
---

# Background
This is a follow-up post to Dave Glick's three part blog post ["Exploring the NuGet v3 Libraries"](https://daveaglick.com/posts/exploring-the-nuget-v3-libraries-part-1). To this day, this is the only documentation provided for the NuGet Client SDK and is even being referenced from [Microsoft's official documentation](https://docs.microsoft.com/en-us/nuget/reference/nuget-client-sdk). I found Dave's blog posts very valuable when implementing the [in-process NuGet Client for Cake](https://github.com/cake-build/cake/pull/1768/files), and I have used Dave as a sounding board for everything NuGet related ever since. Thank you Dave, for being such a great guy!

Anyhow, Dave's posts focuses on using the [NuGet.PackageManagement](https://www.nuget.org/packages/NuGet.PackageManagement/) bits for installing packages. This has it's caveats, for example:
* [NuGet.PackageManagement](https://www.nuget.org/packages/NuGet.PackageManagement/) is one of the few NuGet client libraries that doesn't target `netstandard`. This is a no-go if you'd like to install NuGet packages from a .NET Core application. Fortunately, [Eli Arbel](https://twitter.com/aelij) has created an [unofficial netstandard port](https://www.nuget.org/packages/NuGet.PackageManagement.NetStandard/), which I nowadays also help maintain. Maintaining a fork is painful however, and requires some extra work to keep up with the official NuGet releases.
* It's tightly coupled with the .NET project system, which I really don't care about in my use case (more on my use case below).
* It makes you implement [ugly workarounds](https://github.com/cake-build/cake/tree/17488d0c5bd2ca8a4a6815cd2f0bb307ee17e9ac/src/Cake.NuGet/Install) in order to tweak the libraries to work as you want ?

# Alternative approaches
> There's more than one way to skin a cat. But even more ways to install NuGet packages.
>
> -- <cite>Anonymous</cite>

One alternative approach I've looked at is using the [NuGet.Commands](https://www.nuget.org/packages/NuGet.Commands), which contains _"Complete commands common to command-line and GUI NuGet clients"_ but doesn't depend on NuGet.PackageManagement, (weird, huh?). The reason why I didn't investigate this further is that it also seems to be tightly coupled with the .NET project system. Another alternative would simply be to use the service-based [NuGet API](https://docs.microsoft.com/en-us/nuget/api/overview) directly. I've explored this alternative, but it would require quite a lot of effort to get something like package installation working.

# Going deeper
After reading through the `NuGet.Client` source on [Github](https://github.com/NuGet/NuGet.Client) and the NuGet API specification I've learned a simpler, more straightforward way to install NuGet packages. This approach doesn't rely on `NuGet.PackageManagement`, `NuGet.Commands`, or `NuGet.ProjectModel` and is therefore completely decoupled from the .NET project system. I'm currently investigating this approach in [Depends](https://github.com/mholo65/depends) while working on a new feature, and will most probably also implement it in Cake. This new approach "only" depends on the following NuGet libraries:
* [`NuGet.Common`](https://www.nuget.org/packages/NuGet.Common/) - Includes some required common types such as NuGet's own logger abstraction.
* [`NuGet.Configuration`](https://www.nuget.org/packages/NuGet.Configuration/) - NuGet's client configuration settings implementation.
* [`NuGet.Frameworks`](https://www.nuget.org/packages/NuGet.Frameworks/) - The understanding of different target framework monikers.
* [`NuGet.Packaging`](https://www.nuget.org/packages/NuGet.Packaging/) - NuGet's implementation for reading nupkg package and nuspec package specification files.
* [`NuGet.Packaging.Core`](https://www.nuget.org/packages/NuGet.Packaging/) - The core data structures for `NuGet.Packaging`.
* [`NuGet.Protocol`](https://www.nuget.org/packages/NuGet.Protocol) - The NuGet protocol implementation. Supports both V2 and V3 feeds.
* [`NuGet.Resolver`](https://www.nuget.org/packages/NuGet.Resolver) - NuGet's dependency resolver.
* [`NuGet.Versioning`](https://www.nuget.org/packages/NuGet.Versioning) - NuGet's implementation of Semantic Versioning.

What we are going to do next is:
1. Resolve all package dependency information recursively using `NuGet.Protocol`
2. Resolve the dependency graph from the list of available packages using `NuGet.Resolver`
3. Download all the packages in the dependency graph using `NuGet.Protocol`
4. Extract the content from the NuGet package using `NuGet.Packaging`
5. Locate the best matching assemblies with regards to the current target framework in the extracted package using `NuGet.Frameworks` and `NuGet.Packaging`

## Resolving package and its dependencies
We must learn to walk before we can run. Therefore, we will first look at the basics on how to retrieve dependency information for a single package (see code below).

We begin by creating a `ISettings` and loading the default settings (i.e. `nuget.config`). This will use the default conventions as described [here](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#config-file-locations-and-uses) for locating NuGet configuration. We'll need this to get the available NuGet sources for retrieving package dependency information. Next we'll create a `SourceRepositoryProvider` and pass the `ISettings` and the default NuGet V3 `INuGetResourceProvider`'s. We can think of this as a plugin system for NuGet repositories (we will look more into what they do soon). The documentation states the following:
> INuGetResourceProviders are imported by SourceRepository. They exist as singletons which span all sources, and are responsible for determining if they should be used for the given source when TryCreate is called. The provider determines the caching. Resources may be cached per source, but they are normally created new each time to allow for caching within the context they were created in. Providers may retrieve other resources from the source repository and pass them to the resources they create in order to build on them.

Next, we'll create the default null logger, but for any real usage I strongly suggest implementing your own `ILogger`. Then, we'll iterate through all repositories provided by the `SourceRepositoryProvider`. For each `SourceRepository` we'll resolve a `DependencyInfoResource`. This is where the NuGet Resource Providers comes to play. The default V3 provider registered previously will resolve this for us and make sure other resource's needed also are created. This specific resource, as the name indicates, is used for retrieving dependency information for packages. Lastly we call the `ResolvePackage` method on the `DependencyInfoResource` to get the dependency information. We pass in a `NuGetFramework` which represents our target framework. This will automatically filter out any dependencies that are not applicable for our target framework and only focus on the best matching dependencies.
```csharp
var packageId = "cake.nuget";
var version = "0.30.0";
var framework = "net46";

var package = new PackageIdentity(packageId, NuGetVersion.Parse(version));
var settings = Settings.LoadDefaultSettings(root: null);
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
            return;
        }
    }
}
```
Running the above code should output the following.
```
Cake.NuGet.0.30.0 : Cake.Core [0.30.0, ), Newtonsoft.Json [11.0.2, ), NuGet.Frameworks [4.7.0, ), NuGet.PackageManagement [4.7.0, ), NuGet.ProjectModel [4.7.0, ), NuGet.Versioning [4.7.0, )
```
Then what? In order to get dependency information for all packages in the dependency graph, we need to recursively call `ResolvePackage` for every dependency. One such implementation could look something like the code below. We use a `HashSet<SourcePackageDependencyInfo>` for storing all the packages. Note that we use the `PackageIdentityComparer` as comparer. This will only compare the `PackageIdentity` (id + version), and not compare any dependencies. We will assume that if the identity matches, the dependencies *should* also match.
```csharp
var settings = Settings.LoadDefaultSettings(root: null);
var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());

using (var cacheContext = new SourceCacheContext())
{
    var repositories = sourceRepositoryProvider.GetRepositories();
    var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
    await GetPackageDependencies(
        new PackageIdentity("cake.nuget", NuGetVersion.Parse("0.30.0")),
        NuGetFramework.ParseFolder("net46"), cacheContext, NullLogger.Instance, repositories, availablePackages);

    foreach (var availablePackage in availablePackages)
    {
        Console.WriteLine(availablePackage);
    }
}

async Task GetPackageDependencies(PackageIdentity package,
    NuGetFramework framework,
    SourceCacheContext cacheContext,
    ILogger logger,
    IEnumerable<SourceRepository> repositories,
    ISet<SourcePackageDependencyInfo> availablePackages)
{
    if (availablePackages.Contains(package)) return;

    foreach (var sourceRepository in repositories)
    {
        var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
        var dependencyInfo = await dependencyInfoResource.ResolvePackage(
            package, framework, cacheContext, logger, CancellationToken.None);

        if (dependencyInfo == null) continue;

        availablePackages.Add(dependencyInfo);
        foreach (var dependency in dependencyInfo.Dependencies)
        {
            await GetPackageDependencies(
                new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                framework, cacheContext, logger, repositories, availablePackages);
        }
    }
}
```
The above code should then output the following:
```
Cake.NuGet.0.30.0 : Cake.Core [0.30.0, ), Newtonsoft.Json [11.0.2, ), NuGet.Frameworks [4.7.0, ), NuGet.PackageManagement [4.7.0, ), NuGet.ProjectModel [4.7.0, ), NuGet.Versioning [4.7.0, )
Cake.Core.0.30.0
Newtonsoft.Json.11.0.2
NuGet.Frameworks.4.7.0
NuGet.PackageManagement.4.7.0 : Microsoft.Web.Xdt [2.1.2, ), NuGet.Commands [4.7.0, ), NuGet.Resolver [4.7.0, )
Microsoft.Web.Xdt.2.1.2
NuGet.Resolver.4.7.0 : NuGet.Protocol [4.7.0, )
NuGet.Protocol.4.7.0 : NuGet.Configuration [4.7.0, ), NuGet.Packaging [4.7.0, )
NuGet.Packaging.4.7.0 : Newtonsoft.Json [9.0.1, ), NuGet.Packaging.Core [4.7.0, )
NuGet.Packaging.Core.4.7.0 : NuGet.Common [4.7.0, ), NuGet.Versioning [4.7.0, )
NuGet.Versioning.4.7.0
NuGet.Common.4.7.0 : NuGet.Frameworks [4.7.0, )
Newtonsoft.Json.9.0.1
NuGet.Configuration.4.7.0 : NuGet.Common [4.7.0, )
NuGet.Commands.4.7.0 : NuGet.Credentials [4.7.0, ), NuGet.ProjectModel [4.7.0, )
NuGet.ProjectModel.4.7.0 : NuGet.DependencyResolver.Core [4.7.0, )
NuGet.DependencyResolver.Core.4.7.0 : NuGet.LibraryModel [4.7.0, ), NuGet.Protocol [4.7.0, )
NuGet.LibraryModel.4.7.0 : NuGet.Common [4.7.0, ), NuGet.Versioning [4.7.0, )
NuGet.Credentials.4.7.0 : NuGet.Protocol [4.7.0, )
```
## Resolve the dependency graph from the list of available packages
As you can see in the output from the last code snippet in the previous chapter, we are depending on two different versions of `Newtonsoft.Json`. `Cake.NuGet` depends on 11.0.2 or greater while `NuGet.Packaging` depends on 9.0.1 or greater. Other packages might have even more scenarios like this. At first it would be tempting to just "pick" the highest version, but what if `Newtonsoft.Json.9.0.1` depended on package `FooBar.0.1.0` and `Newtonsoft.Json.11.0.2` didn't? If we just remove duplicate packages by keeping the highest version, we'd end up with an unnecessary package (`FooBar.0.1.0`). Or what if `NuGet.Packaging` also depended on `FooBar.0.1.0` and we just removed duplicate packages by removing the lower version and it's dependencies? Then we'd end up with a missing dependency. This is where `NuGet.Resolver` comes in handy. By passing the list of available package through the `NuGetResolver` we can easily filter out any duplicate dependencies. 
```csharp
var packageId = "cake.nuget";
var settings = Settings.LoadDefaultSettings(root: null);
var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());

using (var cacheContext = new SourceCacheContext())
{
    var repositories = sourceRepositoryProvider.GetRepositories();
    var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
    await GetPackageDependencies(
        new PackageIdentity("cake.nuget", NuGetVersion.Parse("0.30.0")),
        NuGetFramework.ParseFolder("net46"), cacheContext, NullLogger.Instance, repositories, availablePackages);

    var resolverContext = new PackageResolverContext(
        DependencyBehavior.Lowest,
        new[] { packageId },
        Enumerable.Empty<string>(),
        Enumerable.Empty<PackageReference>(),
        Enumerable.Empty<PackageIdentity>(),
        availablePackages,
        sourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource),
        NullLogger.Instance);

    var resolver = new PackageResolver();
    var packagesToInstall = resolver.Resolve(resolverContext, CancellationToken.None)
        .Select(p => availablePackages.Single(x => PackageIdentityComparer.Default.Equals(x, p)));

    foreach (var packageToInstall in packagesToInstall)
    {
        Console.WriteLine(packageToInstall);
    }
}
...
```
## Download all the packages in the dependency graph
To download the NuGet package from the repository, we'll resolve a `DownloadResource` from the `SourceRepository` associated with the package and call `GetDownloadResourceResultAsync`. This will return a `DownloadResourceResult` which contains everything needed for obtaining information about the NuGet package and also extracting the package contents to disk.
```csharp
...
foreach (var packageToInstall in packagesToInstall)
{
    var downloadResource = await packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);
    var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
        packageToInstall,
        new PackageDownloadContext(cacheContext),
        SettingsUtility.GetGlobalPackagesFolder(settings),
        NullLogger.Instance, CancellationToken.None);
}
...
```
## Extract the content from the NuGet package
In order to extract the downloaded package, we'll create a `PackageExtractionContext` and a `PackagePathResolver`. Be careful with the `PackagePathResolver` and **ALWAYS** give an absolute path as root path. If you don't, it will not work correctly. Under the hood, it relies on a class called `PackagePathHelper`, which according to code comments is [a hack](https://github.com/NuGet/NuGet.Client/blob/3803820961f4d61c06d07b179dab1d0439ec0d91/src/NuGet.Core/NuGet.Packaging/PackageExtraction/PackagePathHelper.cs#L15). _No wonder I spent some time figuring out how it works...._
```csharp
...
var packagePathResolver = new PackagePathResolver(Path.GetFullPath("packages"));
var packageExtractionContext = new PackageExtractionContext(
    PackageSaveMode.Defaultv3,
    XmlDocFileSaveMode.None,
    NullLogger.Instance,
    new PackageSignatureVerifier(
        SignatureVerificationProviderFactory.GetSignatureVerificationProviders()),
    SignedPackageVerifierSettings.GetDefault());

foreach (var packageToInstall in packagesToInstall)
{
    ...
    await PackageExtractor.ExtractPackageAsync(
            downloadResult.PackageSource,
            downloadResult.PackageStream,
            packagePathResolver,
            packageExtractionContext,
            CancellationToken.None);
}
...
```
## Locate best matching assemblies with regards to the current target framework in the package
To read information from a NuGet package, we'll use a `PackageReaderBase`. We use this to find e.g. lib items and framework items. In order to find the best matching items based on our target framework, we'll use a `FrameworkReducer` to reduce the list of available target frameworks in the package down to the nearest match.
```csharp
var nuGetFramework = NuGetFramework.ParseFolder("net46");
...
var frameworkReducer = new FrameworkReducer();

foreach (var packageToInstall in packagesToInstall)
{
    ...
    var libItems = downloadResult.PackageReader.GetLibItems();
    var nearest = frameworkReducer.GetNearest(nuGetFramework, libItems.Select(x => x.TargetFramework));
    Console.WriteLine(string.Join("\n", libItems
        .Where(x => x.TargetFramework.Equals(nearest))
        .SelectMany(x => x.Items)));

    var frameworkItems = downloadResult.PackageReader.GetFrameworkItems();
    nearest = frameworkReducer.GetNearest(nuGetFramework, frameworkItems.Select(x => x.TargetFramework));
    Console.WriteLine(string.Join("\n", frameworkItems
        .Where(x => x.TargetFramework.Equals(nearest))
        .SelectMany(x => x.Items)));
}
```

## If package is already downloaded, just use a PackageFolderReader
If the NuGet package is already installed, there's no need to download the NuGet package in order to get the `DownloadResourceResult` and from there obtain the `PackageReader`. We can simply check if the package is already installed, using the `PackagePathResolver` and then create a `PackageFolderReader` from the installed path. This is where the `PackagePathResolver` failed me many times, before I realized that absolute paths was a must.
```csharp
foreach (var packageToInstall in packagesToInstall)
{
    PackageReaderBase packageReader;
    var installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
    if (installedPath == null)
    {
        // Install packages
        ...
        packageReader = downloadResult.PackageReader;
    }
    else
    {
        packageReader = new PackageFolderReader(installedPath);
    }

    var libItems = packageReader.GetLibItems();
    var nearest = frameworkReducer.GetNearest(nuGetFramework, libItems.Select(x => x.TargetFramework));
    Console.WriteLine(string.Join("\n", libItems
        .Where(x => x.TargetFramework.Equals(nearest))
        .SelectMany(x => x.Items)));

    var frameworkItems = packageReader.GetFrameworkItems();
    nearest = frameworkReducer.GetNearest(nuGetFramework, frameworkItems.Select(x => x.TargetFramework));
    Console.WriteLine(string.Join("\n", frameworkItems
        .Where(x => x.TargetFramework.Equals(nearest))
        .SelectMany(x => x.Items)));
}
```

# Sample
A complete working example can be found [here](https://gist.github.com/mholo65/ad5776c36559410f45d5dcd0181a5c64). Thank you for reading this relatively long blog post and hopefully you learned a little bit more about the NuGet Client libraries (at least I did). Next thing to do is to refine this approach and eventually integrate it into Cake. Keep an eye out for the PR.