using Statiq.Common;
using Statiq.Core;
using Statiq.Html;
using Statiq.Markdown;
using Statiq.Razor;
using Statiq.Yaml;

namespace Generator.Pipelines
{
    public sealed class TagIndex : Pipeline
    {
        public TagIndex()
        {
            Dependencies.Add(nameof(Tags));

            InputModules = new ModuleList
            {
                new ReadFiles("_Tags.cshtml")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml()),
                new SetDestination(new FilePath("tags/index.cshtml")),
            };

            // TransformModules = new ModuleList
            // {
            //     new RenderRazor()
            //         .WithLayout(new FilePath("_Layout.cshtml"))
            // };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}