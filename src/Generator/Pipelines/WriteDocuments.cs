using Statiq.Common;
using Statiq.Core;
using Statiq.Html;
using Statiq.Markdown;
using Statiq.Razor;
using Statiq.Yaml;

namespace Generator.Pipelines
{
    public sealed class WriteDocuments : Pipeline
    {
        public WriteDocuments()
        {
            Dependencies.AddRange(new[] {
                nameof(BlogPosts),
                nameof(Pages),
                nameof(Tags),
            });

            OutputModules = new ModuleList
            {
                new ReplaceDocuments(),
                new WriteFiles()
            };
        }
    }
}