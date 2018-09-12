Title: Announcing dotnet depends 
Published: 2018-08-06
Tags: 
- Open Source
- .NET
---

## TL;DR;

[dotnet depends v0.1.0](https://github.com/mholo65/depends/releases/tag/0.1.0) has now been released. This is a tool for exploring dependencies in .NET projects, with an GUI heavily inspired from [bitbake's depexp](https://wiki.yoctoproject.org/wiki/BitBake/GUI). Hopefully this tool will be a valuable addition to your toolbox when e.g. debugging transitive dependencies. Install it by running: `dotnet tool install --global dotnet-depends`, and report any issues and/or feature requests on [Github](https://github.com/mholo65/depends).

## Background

Ever since [Dave Glick announced Buildalyzer](https://daveaglick.com/posts/running-a-design-time-build-with-msbuild-apis), I knew I wanted to build something with it. I had been thinking about a dependency explorer, since you know, debugging transitive dependencies in .NET really sucks. It always takes a dozen of clicks on [nuget.org](https://www.nuget.org/) and a couple of minutes scrolling up-and-down in `project.assets.json` to figure out which dependency brought in package A, which in turn was incompatible with package B.

At first, when I started playing with [Buildalyzer](https://github.com/daveaglick/Buildalyzer), I was determined that the tool should output a dependency graph in the [DOT language](https://en.wikipedia.org/wiki/DOT_(graph_description_language)), which in turn could be converted to SVG or PNG using [graphviz](https://www.graphviz.org/). After a few attempts I came to the conclusion that the graphs easily became too messy and was hard to interpret. I was close to accept my defeat, when I remembered a great tool called `depexp` which I had used with [Yocto](https://www.yoctoproject.org/) back in the days, when I was creating/tweaking embedded Linux distros (narrator: he still does, occasionally...).

Next problem then... Since I wanted this to be a [.NET Core global tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), how do I introduce a GUI like `depexp` in a .NET Core console application? I then remembered the console UI toolkit for .NET [Miguel de Icaza announced](https://twitter.com/migueldeicaza/status/964352496243273728). So, I took a dependency on [Terminal.Gui / Gui.cs](https://github.com/migueldeicaza/gui.cs), and tweeted this:

<blockquote class="twitter-tweet" data-lang="en"><p lang="en" dir="ltr">Been playing with Buildalyzer and this is what I came up with. A simple dependency explorer for <a href="https://twitter.com/hashtag/dotnet?src=hash&amp;ref_src=twsrc%5Etfw">#dotnet</a>. GUI influenced by bitbake&#39;s depexp and powered by gui.cs.<br><br>Source can be found here: <a href="https://t.co/eJqSgjQ2ky">https://t.co/eJqSgjQ2ky</a> <a href="https://t.co/7DbESQUs75">pic.twitter.com/7DbESQUs75</a></p>&mdash; Martin Björkström (@mholo65) <a href="https://twitter.com/mholo65/status/1014128499987288069?ref_src=twsrc%5Etfw">July 3, 2018</a></blockquote>
<script async src="https://platform.twitter.com/widgets.js" charset="utf-8"></script>

Feedback was awesome, so I submitted a couple of [PR's to Gui.cs](https://github.com/migueldeicaza/gui.cs/pulls?q=is%3Apr+is%3Aclosed+author%3Amholo65) and awaited Buildalyzer v1.0.0 before releasing v0.1.0 of `dotnet depends`.

Curious to see how it works? Install it by running: `dotnet tool install --global dotnet-depends`, and then run `dotnet depends /path/to/myproject.csproj`. If your project targets multiple frameworks, you can specify the target by appending the `-f|--framework <FRAMEWORK>` option. Please remember to report any issues and/or feature requests on [Github](https://github.com/mholo65/depends). Happy hacking!