using System.Linq;
using site.Extensions;
using Statiq.Common;
using Statiq.Core;
using Statiq.Feeds;
using Statiq.Handlebars;

namespace site.Pipelines
{
    public class TagsPipeline : ApplyLayoutPipeline
    {
        public TagsPipeline()
        {
            Dependencies.Add(nameof(BlogPostPipeline));

            InputModules = new ModuleList
            {
                new ReadFiles("_tag.hbs")
            };

            ProcessModules = new ModuleList
            {
                new MergeDocuments
                {
                    new ReplaceDocuments(nameof(BlogPostPipeline)),
                    new GroupDocuments("Tags")
                }.Reverse(),
                new SetDestination(Config.FromDocument(doc => new NormalizedPath($"./tags/{doc.GetString(Keys.GroupKey)}.html"))),
                new OptimizeFileName(),
                new RenderHandlebars()
                    .WithModel(Config.FromDocument((doc, context) => new
                    {
                        title = doc.GetString(Keys.GroupKey),
                        posts = doc.GetChildren()
                            .OrderByDescending(x => x.GetDateTime(FeedKeys.Published))
                            .Select(x => x.AsPost(context)),
                        tags = context.Inputs
                            .OrderByDescending(x => x.GetChildren().Count)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Take(10)
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