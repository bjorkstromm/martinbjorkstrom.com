using Statiq.Common;
using Statiq.Core;
using Statiq.Feeds;

namespace site.Pipelines
{
    public class FeedsPipeline : Pipeline
    {
        public FeedsPipeline()
        {
            Dependencies.Add(nameof(BlogPostPipeline));

            ProcessModules = new ModuleList
            {
                new ConcatDocuments(nameof(BlogPostPipeline)),
                new OrderDocuments(Config.FromDocument((x => x.GetDateTime(FeedKeys.Published))))
                    .Descending(),
                new GenerateFeeds()
                    .WithFeedTitle("Martin Björkström")
                    .WithFeedDescription("Driving Digital Transformation on Serverless Containers...")
                    .WithFeedAuthor("Martin Björkström")
            };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}