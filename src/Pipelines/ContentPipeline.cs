using Statiq.Common;
using Statiq.Core;
using Statiq.Markdown;
using Statiq.Yaml;

namespace site.Pipelines
{
    public class ContentPipeline : ApplyLayoutPipeline
    {
        public ContentPipeline()
        {
            InputModules = new ModuleList
            {
                new ReadFiles("*.md")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml()),
                new RenderMarkdown(),
                new SetDestination(".html")
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}