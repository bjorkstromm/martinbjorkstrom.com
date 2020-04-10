using System;
using System.Linq;
using Statiq.Common;
using Statiq.Core;
using Statiq.Feeds;
using Statiq.Handlebars;

namespace site.Pipelines
{
    public abstract class ApplyLayoutPipeline : Pipeline
    {
        protected ApplyLayoutPipeline()
        {
            PostProcessModules = new ModuleList
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
                        year = ctx.Settings.GetString(FeedKeys.Copyright)
                    })),
                new SetContent(Config.FromDocument(x => x.GetString("template")))
            };
        }
    }
}