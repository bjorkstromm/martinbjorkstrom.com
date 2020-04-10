using System.Linq;
using Statiq.Common;
using Statiq.Core;
using Statiq.Handlebars;

namespace site.Pipelines
{
    public class TagIndexPipeline : AppliedLayoutPipeline
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
                            .OrderByDescending(x => IDocumentHierarchyExtensions.GetChildren(x).Count)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Select(tag => new
                            {
                                link = context.GetLink(tag),
                                title = tag.GetString(Keys.GroupKey),
                                count = tag.GetChildren().Count
                            })
                    }))
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}