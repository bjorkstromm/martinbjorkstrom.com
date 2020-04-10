using Statiq.Common;
using Statiq.Core;
using Statiq.Html;
using Statiq.Markdown;
using Statiq.Yaml;

namespace site.Pipelines
{
    public class BlogPostPipeline : ApplyLayoutPipeline
    {
        public BlogPostPipeline()
        {
            InputModules = new ModuleList
            {
                new ReadFiles("./posts/*.md")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml()),
                new RenderMarkdown(),
                new GenerateExcerpt(),
                new SetDestination(".html")
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}