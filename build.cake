#addin "nuget:?package=Cake.Wyam&version=1.4.1"
#addin "nuget:?package=Cake.Npm&version=0.14.0"
#tool "nuget:?package=Wyam&version=1.4.1"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var preview = Argument("preview", false);
var watch = Argument("watch", false);
var theme = "Phantom";
var recipe = "Blog";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
   // Executed BEFORE the first task.
   Information("Running tasks...");
});

Teardown(ctx =>
{
   // Executed AFTER the last task.
   Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory("./output");
});

Task("Build")
    .IsDependentOn("Clean")
    .Does(() =>
{
    Wyam(new WyamSettings
    {
        Theme = theme,
        Recipe = recipe,
        Preview = preview,
        Watch = watch
    });
});

Task("Install-Netlify-Cli")
    .Does(()=>
{
    NpmInstall(new NpmInstallSettings
    {
        Global = false,
        LogLevel = NpmLogLevel.Warn,
    }.AddPackage("netlify-cli"));
});


// Task("Deploy")
//     .IsDependentOn("Build")
//     .IsDependentOn("Install-Netlify-Cli")
//     .Does(() => 
// {
//     var token = EnvironmentVariable("NETLIFY_TOKEN");
//     var siteId = EnvironmentVariable("NETLIFY_SITEID");
//     if(string.IsNullOrEmpty(token)) {
//         throw new Exception("Could not get NETLIFY_TOKEN environment variable");
//     }
//     if(string.IsNullOrEmpty(siteId)) {
//         throw new Exception("Could not get NETLIFY_SITEID environment variable");
//     }

//     NetlifyDeploy("./output", new NetlfiyDeploySettings {
//         SiteId = siteId,
//         Token = token
//     });
// });

Task("Default")
    .IsDependentOn("Build");

RunTarget(target);