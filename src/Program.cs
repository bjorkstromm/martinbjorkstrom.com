using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Statiq.App;
using Statiq.Common;
using Statiq.Core;
using Statiq.Feeds;
using Statiq.Handlebars;
using Statiq.Html;
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
                .AddSetting(Keys.LinkLowercase, true) 
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
                new GenerateExcerpt(),
                new SetDestination(".html")
            };

            PostProcessModules = ApplyLayout.Modules;

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }

    public static class ApplyLayout
    {
        public static ModuleList Modules => new ModuleList
        {
            new SetMetadata("template", Config.FromContext(async ctx => await ctx.Outputs
                .FromPipeline(nameof(LayoutPipeline))
                .First(x => x.Source.FileName == "layout.hbs")
                .GetContentStringAsync())),
            new RenderHandlebars("template")
                .Configure(async (context, document, handlebars) =>
                {
                    foreach (var partial in context.Outputs
                        .FromPipeline(nameof(LayoutPipeline)).WhereContainsKey("partial"))
                    {
                        handlebars.RegisterTemplate(
                            partial.GetString("partial"),
                            await partial.GetContentStringAsync());
                    }
                }).WithModel(Config.FromDocument(async (doc, ctx) => new
                {
                    title = doc.GetString(Keys.Title),
                    body = await doc.GetContentStringAsync(),
                    link = ctx.GetLink(doc),
                    year = DateTime.UtcNow.Year
                })),
            new SetContent(Config.FromDocument(x => x.GetString("template")))
        };
    }

    public class TagIndexPipeline : Pipeline
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
                            .OrderByDescending(x => x.GetChildren().Length)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Select(tag => new
                            {
                                link = context.GetLink(tag),
                                title = tag.GetString(Keys.GroupKey),
                                count = tag.GetChildren().Length
                            })
                    }))
            };

            PostProcessModules = ApplyLayout.Modules;

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
    
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
                new SetDestination(Config.FromDocument(doc => new NormalizedPath($"./tags/{doc.GetString(Keys.GroupKey)}.html"))),
                new OptimizeFileName(),
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
                            .OrderByDescending(x => x.GetChildren().Length)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Take(10)
                            .Select(tag => new
                            {
                                link = context.GetLink(tag),
                                title = tag.GetString(Keys.GroupKey),
                                count = tag.GetChildren().Length
                            })
                    }))
            };

            PostProcessModules = ApplyLayout.Modules;

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }

    public class ArchivePipeline : Pipeline
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
                            .GroupBy(x => x.GetDateTime("Published").Year)
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

            PostProcessModules = ApplyLayout.Modules;

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
    
    public class IndexPipeline : Pipeline
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
                            .OrderByDescending(x => x.GetDateTime("Published"))
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
                            .OrderByDescending(x => x.GetChildren().Length)
                            .ThenBy(x => x.GetString(Keys.GroupKey))
                            .Take(10)
                            .Select(tag => new
                            {
                                link = context.GetLink(tag),
                                title = tag.GetString(Keys.GroupKey),
                                count = tag.GetChildren().Length
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

            PostProcessModules = ApplyLayout.Modules;

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
                new CopyFiles("./assets/{css,fonts,js}/**/*")
            };
        }
    }

    public class FeedsPipeline : Pipeline
    {
        public FeedsPipeline()
        {
            Dependencies.Add(nameof(BlogPostPipeline));

            ProcessModules = new ModuleList
            {
                new GenerateFeeds()
                    .WithAtomPath("atom.xml")
            };
        }
    }
}
