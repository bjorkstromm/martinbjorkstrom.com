Title: I ported my blog to Statiq
Published: 2020-04-17
Tags: 
- .NET
- Statiq
- Static site generator
---
### Background
About six months ago, I got assigned a task to write a convention-driven documentation generator at work. Our first thought was to use [Wyam](https://wyam.io/) for this, but after discussions with [Dave Glick](https://github.com/daveaglick/), he lured us into trying out this new shiny stuff he was working on, called [Statiq](https://statiq.dev/). Creating new stuff with Wyam at that point would have resulted in a rewrite once Statiq would be released, making Wyam obsolete.

We started out with Statiq, and for someone with almost zero-experience with Wyam, the learning-curve was steep. The lack of documentation for Statiq at that time resulted in lots of chatting with Dave and lots of source code reading. The end result, however, turned out great. We did lots of cool stuff like generating tables and pages from SQL queries, downloading artifacts from Azure DevOps and generating documentation from the assemblies, integrating Swagger UI for API documentation, etc, etc. For me, Wyam has always been magic that I never really grasped, but Statiq on the other hand was really easy to work with once you got the hang of it.

So, what is Statiq?
> Statiq is the world's most powerful static generation platform, allowing you to use or create a static generator that's exactly what you need.
>
> -- <cite>[Statiq/Dave Glick](https://statiq.dev/)</cite>

Seems compelling, right? Additionally, since Statiq runs on .NET, you'll have all the power of .NET in your static generator pipelines. This is what really makes it shine!

#### How Statiq works
_(The following section is copied from the official Statiq documentation and reflects the state of Statiq by the time this blog post was written)_
![Statiq Flow](/assets/images/statiq-flow.png){width=100%}

Statiq is powerful because it combines a few simple building blocks that can be rearranged and used in limitless combinations. Think of it like LEGO® for static generation.
* Content and data can come from a variety of sources including input files, databases, and services.
* Documents are created which each contain content and metadata.
* The documents are processed by pipelines.
* Each pipeline consists of one or more modules that manipulate the documents given to it by transforming, aggregating, filtering, or producing entirely new documents.
* The final output of each pipeline is made available to other pipeline and may be written to output files or deployed to hosting services.

### Motivation
Back in December, when the documentation generator I was working on was feature complete, I wanted to continue using Statiq as it was still evolving. This was when I decided that my next project will be porting this blog to use Statiq. The old Wyam template that I was using worked, but it was something I had copy-pasted from [Gary Park's blog](https://github.com/gep13/gep13) and I had almost zero knowledge on what happens behind the scene. As most software developers, I don't trust magic, I need to know how stuff works. Therefore my next project would be porting this blog to Statiq.

<blockquote class="twitter-tweet"><p lang="en" dir="ltr">TODO: Rewrite my blog with <a href="https://twitter.com/statiqdev?ref_src=twsrc%5Etfw">@statiqdev</a> and then blog about it. I&#39;ve had the privilege to work with the framework for a work thingy and I see great potential in it. Wyam was pure magic to me, but Statiq I can understand 😀</p>&mdash; Martin Björkström (@mholo65) <a href="https://twitter.com/mholo65/status/1202304328465301512?ref_src=twsrc%5Etfw">December 4, 2019</a></blockquote> <script async src="https://platform.twitter.com/widgets.js" charset="utf-8"></script> 

<br/>

### Attempt 1 - Porting Wyam Blog recipe to Statiq
My first though was to port the old Wyam blog recipe and theme to Statiq. So I cloned Wyam sources and started porting these to Statiq.
* [Wyam Blog Recipe](https://github.com/Wyamio/Wyam/tree/94a3f1ba258b7d1aaf4f9e55b222697698346396/src/recipes/Wyam.Blog)
* [Phantom Blog Theme](https://github.com/Wyamio/Wyam/tree/94a3f1ba258b7d1aaf4f9e55b222697698346396/themes/Blog/Phantom)

After a couple of hours working on this, which can be found [here](https://github.com/mholo65/mholo65/commits/feature/statiq), I quickly realized that this is very difficult and time consuming. And worst of all, I was porting code which I had no idea of what it was doing. The Wyam blog recipe was very extensible and I didn't need many of the extension points it was offering. Some of the code would also have been better off just re-written in Statiq. Once again, I consulted Dave...

### Attempt 2 - Using Statiq.Web
Once Dave was happy with [Statiq Framework](https://github.com/statiqdev/Statiq.Framework), which is the core of Statiq, he started working on the replacements for Wyam recipes, namely [Statiq.Web](https://github.com/statiqdev/Statiq.Web) and [Statiq.Docs](https://github.com/statiqdev/Statiq.Docs). So in beginning of March I decided to give Statiq.Web a go (source code [here](https://github.com/mholo65/mholo65/commits/statiq.web)). I quickly faced same problem here as with [Attempt 1](#attempt-1-porting-wyam-blog-recipe-to-statiq). While the recipe was mostly ported, it still lacked themes, which led me into porting the old Wyam themes to Statiq. The end result would have been much like using Wyam, i.e. magic which I didn't understand :) So I wanted full control...

### Attempt 3 - Using Statiq.Framework
As I had used [Statiq Framework](https://github.com/statiqdev/Statiq.Framework) previously for work related stuff, I was quite comfortable with this approach. I started by [contributing a Handlebars module to Statiq](https://github.com/statiqdev/Statiq.Framework/pull/90), because I prefer using Handlebars over Razor for simple templates. Once the Handlebars module was pulled in, I started porting my blog. The first thing I did was to take the HTML output from the old Wyam-powered blog and create handlebars templates from it. next step was to create some pipelines. The basics of the pipelines are described in the following sections. To better understand the concept of Statiq pipelines and phases, please read [the documentation here](https://statiq.dev/framework/concepts/pipelines) before continuing reading this.

#### Blog post pipeline
The main pipeline in my blog engine is the pipeline that handles the blog posts. It reads all blog posts, using the `./posts/*.md` glob pattern in the `input` phase.

```csharp
public BlogPostPipeline()
{
    InputModules = new ModuleList
    {
        new ReadFiles("./posts/*.md")
    };

    ProcessModules = new ModuleList
    {
            new ExtractFrontMatter(new ParseYaml()),
            new RenderMarkdown()
                .UseExtensions(),
            new GenerateExcerpt(),
            new SetDestination(".html")
    };

    ...

    OutputModules = new ModuleList
    {
        new WriteFiles()
    };
}
```