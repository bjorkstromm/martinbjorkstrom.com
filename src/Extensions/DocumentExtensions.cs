using System;
using System.Dynamic;
using Statiq.Common;
using Statiq.Feeds;

namespace site.Extensions
{
    public static class DocumentExtensions
    {
        private class DynamicDocumentWrapper : DynamicObject
        {
            private readonly IDocument _document;

            public DynamicDocumentWrapper(IDocument document)
            {
                _document = document;
            }

            public override bool TryGetMember(GetMemberBinder binder, out object result) =>
                _document.TryGetValue(binder.Name, out result);
        }

        public static dynamic AsDynamic(this IDocument document) => new DynamicDocumentWrapper(document ?? throw new ArgumentNullException(nameof(document)));

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