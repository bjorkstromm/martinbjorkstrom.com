using Statiq.Common;
using Statiq.Core;
using Statiq.Yaml;

namespace site.Pipelines
{
    public class LayoutPipeline : Pipeline
    {
        public LayoutPipeline()
        {
            InputModules = new ModuleList
            {
                new ReadFiles("{!_,}*.hbs")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml())
            };
        }
    }
}