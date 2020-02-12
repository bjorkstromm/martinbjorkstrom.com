Title: Open Sourcing Alpha-Beta
Published: 2018-10-09
Tags: 
- Open Source
- .NET
- Azure
- Cognitive Services
---

[Alpha-Beta](https://github.com/mholo65/alpha-beta) is a simple learning game I made for my children about a year ago that I'm now open sourcing. It's a fun way for kids to learn to write words, and locate keys on the keyboard. The game picks a random word from a list for the user to type. It uses Azure cognitive services like [Bing Image Search](https://docs.microsoft.com/en-us/azure/cognitive-services/bing-image-search/) for searching related images and [Bing Speech](https://docs.microsoft.com/en-us/azure/cognitive-services/speech/home) for reading the word out loud. See it in action below:

![alpha-beta](https://raw.githubusercontent.com/mholo65/alpha-beta/master/media/demo.gif)

The game is multilingual, just specify the locale in `app.config` and you're good to go. It _should_  support all the locales that are supported by Bing Speech (see list [here](https://docs.microsoft.com/en-us/azure/cognitive-services/Speech/api-reference-rest/bingvoiceoutput#SupLocales)), although  I've only tested `fi-FI`, `sv-SE`, `en-US` and `en-GB`. I also tried to make it super simple for my non-programming significant other to add and remove words used in the game. Currently it picks the words from a `txt`-file, where each words used are separated by a new line. I know that there are lots of similar games out there, but since my kids are swedish speaking, it was hard to find something good. Creating Alpha-Beta also got me to do some hands-on work with Azure cognitive services, which was a pleasant acquaintance.

Source can be found [here](https://github.com/mholo65/alpha-beta). It currently only runs on Windows, and you'll need an Azure subscription and have to set up `Bing Search` and `Bing Speech`. Guide for setting up Azure cognitive services can be found [here](https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-apis-create-account). Happy hacking! I hope your kids will have as much fun with it as mine have had.