using Statiq.Common;
using Statiq.Core;

namespace site.Pipelines
{
    public class AssetsPipeline : Pipeline
    {
        public AssetsPipeline()
        {
            Isolated = true;
            ProcessModules = new ModuleList
            {
                new CopyFiles("./assets/{css,fonts,js,images}/**/*", "*.{png,ico,webmanifest}")
            };
        }
    }
}