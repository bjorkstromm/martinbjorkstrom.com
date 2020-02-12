using Statiq.Common;
using Statiq.Core;
using Statiq.Html;
using Statiq.Markdown;
using Statiq.Yaml;

namespace Generator.Pipelines
{
    public sealed class BlogPosts : Pipeline
    {
        public BlogPosts()
        {
            InputModules = new ModuleList
            {
                new ReadFiles("posts/**/*.md")
            };

            ProcessModules = new ModuleList
            {
                new ProcessIncludes(),
                new ExtractFrontMatter(new ParseYaml()),
                new ProcessShortcodes(),
                new RenderMarkdown()
                    .UseExtensions(),
                new GenerateExcerpt(),
                new AddTitle(),
                new SetDestination(".html")
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