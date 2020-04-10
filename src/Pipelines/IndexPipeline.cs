using System.Linq;
using Statiq.Common;
using Statiq.Core;
using Statiq.Handlebars;
using Statiq.Yaml;

namespace site.Pipelines
{
    public class IndexPipeline : AppliedLayoutPipeline
    {
        public IndexPipeline()
        {
            Dependencies.AddRange(nameof(BlogPostPipeline), nameof(TagsPipeline));

            InputModules = new ModuleList
            {
                new ReadFiles("_index.hbs")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml()),
                new SetDestination(Config.FromValue(new NormalizedPath("./index.html"))),
                new RenderHandlebars()
                    .WithModel(Config.FromDocument((doc, context) => new
                    {
                        description = doc.GetString("description"),
                        intro = doc.GetString("intro"),
                        posts = context.Outputs.FromPipeline(nameof(BlogPostPipeline))
                            .OrderByDescending(x => IMetadataConversionExtensions.GetDateTime(x, "Published"))
                            .Take(3)
                            .Select(post => new
                            {
                                link = context.GetLink(post),
                                title = post.GetString(Keys.Title),
                                excerpt = post.GetString("Excerpt"),
                                date = post.GetDateTime("Published").ToLongDateString()
                            }),
                        olderPosts = context.Outputs.FromPipeline(nameof(BlogPostPipeline))
                            .OrderByDescending(x => x.GetDateTime("Published"))
                            .Skip(3)
                            .Select(post => new
                            {
                                link = context.GetLink(post),
                                title = post.GetString(Keys.Title),
                            }), 
                        tags = context.Outputs.FromPipeline(nameof(TagsPipeline))
                            .OrderByDescending(x => x.GetChildren().Count)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Take(10)
                            .Select(tag => new
                            {
                                link = context.GetLink(tag),
                                title = tag.GetString(Keys.GroupKey),
                                count = tag.GetChildren().Count
                            }),
                        socialMediaLinks = doc.GetChildren("socialMediaLinks")
                            .Select(socialMediaLink => new
                            {
                                link = socialMediaLink.GetString("link"),
                                @class = socialMediaLink.GetString("class"),
                                title = socialMediaLink.GetString("title")
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