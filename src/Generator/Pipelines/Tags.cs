using Statiq.Common;
using Statiq.Core;
using Statiq.Html;
using Statiq.Markdown;
using Statiq.Razor;
using Statiq.Yaml;

namespace Generator.Pipelines
{
    public sealed class Tags : Pipeline
    {
        public Tags()
        {
            Dependencies.Add(nameof(BlogPosts));

            InputModules = new ModuleList
            {
                new ReadFiles("_Tag.cshtml")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml()),
                new MergeDocuments
                {
                    new ReplaceDocuments(nameof(BlogPosts)),
                    new GroupDocuments("Tags")
                }.Reverse(),
                new SetDestination(Config.FromDocument(doc => new FilePath($"tags/{doc[Keys.GroupKey]}.cshtml"))),
            };

            // TransformModules = new ModuleList
            // {
            //     new RenderRazor()
            //         .WithLayout(new FilePath("_Layout.cshtml"))
            // };

            OutputModules = new ModuleList
            {
                new WriteFiles()
            };
        }
    }
}