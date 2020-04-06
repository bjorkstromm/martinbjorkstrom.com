using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Statiq.App;
using Statiq.Common;
using Statiq.Core;
using Statiq.Handlebars;
using Statiq.Markdown;
using Statiq.Yaml;

namespace site
{
    internal static class Program
    {
        private static Task<int> Main(string[] args)
        {
            return Bootstrapper
                .Factory
                .CreateDefault(args)
                .RunAsync();
        }
    }

    public class BlogPostPipeline : Pipeline
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
                new SetDestination(".html")
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }

    public class TagListPipeline : Pipeline
    {
        public TagListPipeline()
        {
            Dependencies.Add(nameof(BlogPostPipeline));

            ProcessModules = new ModuleList
            {
                new ExecuteConfig(Config.FromContext(context =>
                {
                    var groups = context.Outputs
                        .FromPipeline(nameof(BlogPostPipeline))
                        .Select(x => (Document: x, Keys: x.GetList<string>("Tags")))
                        .ToList();

                    return groups
                        .GroupByMany(x => x.Keys, x => x.Document)
                        .Select(x => context.CreateDocument(
                            new MetadataItems
                            {
                                { Keys.Children, x.ToImmutableArray() },
                                { Keys.GroupKey, x.Key }
                            }));
                }))
            };
        }
    }

    public class TagsPipeline : Pipeline
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
                new SetDestination(Config.FromDocument(doc => new NormalizedPath($"./tags/{doc[Keys.GroupKey]}.html"))),
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
                            .OrderBy(x => x.GetString(Keys.GroupKey))
                            .Select(tag => new
                            {
                                link = context.GetLink(tag),
                                title = doc.GetString(Keys.GroupKey),
                                count = doc.GetChildren().Length
                            })
                    }))
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }

    public class AssetsPipeline : Pipeline
    {
        public AssetsPipeline()
        {
            Isolated = true;
            ProcessModules = new ModuleList
            {
                new CopyFiles("./assets/{css,fonts,js}/**/*.*")
            };
        }
    }
}
