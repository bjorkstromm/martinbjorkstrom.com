using Statiq.Common;
using Statiq.Core;
using Statiq.Html;
using Statiq.Markdown;
using Statiq.Razor;
using Statiq.Yaml;

namespace Generator.Pipelines
{
    public sealed class BlogArchive : Pipeline
    {
        public BlogArchive()
        {
            Dependencies.Add(nameof(BlogPosts));

            InputModules = new ModuleList
            {
                new ReadFiles("_Archive.cshtml")
            };

            ProcessModules = new ModuleList
            {
                new ExtractFrontMatter(new ParseYaml()),
                new MergeDocuments
                {
                    new ReplaceDocuments(nameof(BlogPosts)),
                    new GroupDocuments("Blogs"),
                    new PaginateDocuments(25)
                }.Reverse(),
                new SetDestination(new FilePath("posts/index.cshtml")),
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