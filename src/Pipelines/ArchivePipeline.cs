using System.Linq;
using Statiq.Common;
using Statiq.Core;
using Statiq.Handlebars;

namespace site.Pipelines
{
    public class ArchivePipeline : AppliedLayoutPipeline
    {
        public ArchivePipeline()
        {
            Dependencies.Add(nameof(BlogPostPipeline));

            InputModules = new ModuleList
            {
                new ReadFiles("_archive.hbs")
            };

            ProcessModules = new ModuleList
            {
                new SetDestination(Config.FromValue(new NormalizedPath("./posts/index.html"))),
                new RenderHandlebars()
                    .WithModel(Config.FromContext(context => new
                    {
                        groups = context.Outputs.FromPipeline(nameof(BlogPostPipeline))
                            .GroupBy(x => IMetadataConversionExtensions.GetDateTime(x, "Published").Year)
                            .OrderByDescending(x => x.Key)
                            .Select(group => new
                            {
                                key = group.Key,
                                posts = group
                                    .OrderByDescending(x => x.GetDateTime("Published"))
                                    .Select(doc => new
                                    {
                                        link = context.GetLink(doc),
                                        title = doc.GetString(Keys.Title),
                                        date = doc.GetDateTime("Published").ToLongDateString()
                                    }),
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