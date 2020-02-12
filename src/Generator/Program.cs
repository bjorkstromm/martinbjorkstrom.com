using System.Threading.Tasks;
using Generator.Pipelines;
using Statiq.App;

namespace Generator
{
    internal static class Program
    {
        private static Task Main(string[] args) =>
            Bootstrapper
                .CreateDefault(args)
                .RunAsync();
    }
}
