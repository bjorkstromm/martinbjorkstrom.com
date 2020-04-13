using System;
using System.Dynamic;
using Statiq.Common;
using Statiq.Feeds;

namespace site.Extensions
{
    public static class DocumentExtensions
    {
        public static object AsTag(this IDocument document, IExecutionContext context) => new
        {
            link = context.GetLink(document),
            title = document.GetString(Keys.GroupKey),
            count = document.GetChildren().Count
        };

        public static object AsPost(this IDocument document, IExecutionContext context) => new
        {
            link = context.GetLink(document),
            title = document.GetString(Keys.Title),
            excerpt = document.GetString(FeedKeys.Excerpt),
            date = document.GetDateTime(FeedKeys.Published).ToLongDateString()
        };
    }
}