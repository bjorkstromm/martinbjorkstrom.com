using System.Threading.Tasks;
using Statiq.App;
using Statiq.Common;

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
                .AddSetting(Keys.LinksUseHttps, true)
                .AddSetting(Keys.Host, "martinbjorkstrom.com")
                .RunAsync();
        }
    }
}
