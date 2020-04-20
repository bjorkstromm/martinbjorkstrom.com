
var npmPath = (IsRunningOnWindows()
                ? Context.Tools.Resolve("npm.cmd")
                : Context.Tools.Resolve("npm"))
                ?? throw new Exception("Failed to resolve npm, make sure Node is installed.");

Action<FilePath, ProcessArgumentBuilder> Cmd => (path, args) => {
    var result = StartProcess(path, new ProcessSettings { Arguments = args });

    if(0 != result)
    {
        throw new Exception($"Failed to execute tool {path.GetFilename()} ({result})");
    }
};

Task("Build")
    .Does(() => DotNetCoreRun("./src/site.csproj"));

Task("Install-Netlify-Cli")
    .Does(() => Cmd(npmPath, new ProcessArgumentBuilder()
        .Append("install")
        .AppendSwitch("--prefix", " ", "tools")
        .Append("netlify-cli")));

Task("Deploy")
    .IsDependentOn("Build")
    .IsDependentOn("Install-Netlify-Cli")
    .Does(() => {
        var netlifyPath = (IsRunningOnWindows()
            ? Context.Tools.Resolve("netlify.cmd")
            : Context.Tools.Resolve("netlify"))
            ?? throw new Exception("Failed to resolve netlify-cli, make sure netlify-cli is installed.");

        Cmd(netlifyPath, new ProcessArgumentBuilder()
            .Append("deploy")
            .AppendSwitch("--dir", "=", "output"));
    });

Task("Preview")
    .Does(() => DotNetCoreRun("./src/site.csproj", new ProcessArgumentBuilder()
        .Append("preview")));

Task("Default")
    .IsDependentOn("Build");

RunTarget(Argument("Target", "Default"));