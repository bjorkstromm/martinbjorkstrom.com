using System.Linq;
using site.Extensions;
using Statiq.Common;
using Statiq.Core;
using Statiq.Handlebars;

namespace site.Pipelines
{
    public class TagIndexPipeline : ApplyLayoutPipeline
    {
        public TagIndexPipeline()
        {
            Dependencies.Add(nameof(TagsPipeline));
            
            InputModules = new ModuleList
            {
                new ReadFiles("_tagIndex.hbs")
            };

            ProcessModules = new ModuleList
            {
                new SetDestination(Config.FromValue(new NormalizedPath("./tags/index.html"))),
                new RenderHandlebars()
                    .WithModel(Config.FromContext(context => new
                    {
                        tags = context.Outputs.FromPipeline(nameof(TagsPipeline))
                            .OrderByDescending(x => x.GetChildren().Count)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Select(x => x.AsTag(context)),
                    }))
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}