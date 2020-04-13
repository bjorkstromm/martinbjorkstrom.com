using System;
using System.Threading.Tasks;
using Statiq.App;
using Statiq.Common;
using Statiq.Feeds;

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
                .AddSetting(Keys.Title, "Martin Björkström")
                .AddSetting(FeedKeys.Author, "Martin Björkström")
                .AddSetting(FeedKeys.Description, "Driving Digital Transformation on Serverless Containers...")
                .AddSetting(FeedKeys.Copyright, DateTime.UtcNow.Year.ToString())
                .RunAsync();
        }
    }
}
