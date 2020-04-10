using System.Linq;
using Statiq.Common;
using Statiq.Core;
using Statiq.Handlebars;

namespace site.Pipelines
{
    public class TagsPipeline : AppliedLayoutPipeline
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
                // Due to bug https://github.com/statiqdev/Statiq.Framework/issues/93, we must set directory again
                new SetDestination(Config.FromDocument(doc => new NormalizedPath("./tags/").Combine(doc.Destination.FileName))),
                new RenderHandlebars()
                    .WithModel(Config.FromDocument((doc, context) => new
                    {
                        title = doc.GetString(Keys.GroupKey),
                        posts = doc.GetChildren()
                            .OrderByDescending(x => x.GetDateTime("Published"))
                            .Select(child => new
                            {
                                link = context.GetLink(child),
                                title = child.GetString(Keys.Title),
                                date = child.GetDateTime("Published").ToLongDateString()
                            }),
                        tags = context.Inputs
                            .OrderByDescending(x => x.GetChildren().Count)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Take(10)
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