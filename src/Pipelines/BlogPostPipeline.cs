using System.Linq;
using site.Extensions;
using Statiq.Common;
using Statiq.Core;
using Statiq.Feeds;
using Statiq.Handlebars;
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

            PostProcessModules = PostProcessModules.Prepend(
                new SetMetadata("template",
                    Config.FromContext(async ctx =>
                        await ctx.FileSystem.GetInputFile("_post.hbs").ReadAllTextAsync())),
                new RenderHandlebars("template")
                    .WithModel(Config.FromDocument(async (doc, context) => new
                    {
                        title = doc.GetString(Keys.Title),
                        date = doc.GetDateTime(FeedKeys.Published).ToLongDateString(),
                        body = await doc.GetContentStringAsync(),
                        tags = doc.GetList<string>("tags")
                            .OrderBy(x => x)
                            .Select(x => context.Outputs
                                .FromPipeline(nameof(TagsPipeline))
                                .First(tag => tag.GetString(Keys.GroupKey) == x))
                            .Select(x => x.AsTag(context))
                    })),
                new SetContent(Config.FromDocument(x => x.GetString("template"))));

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}