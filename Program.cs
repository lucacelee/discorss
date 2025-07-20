using RSS;
using DSharpPlus;
using Tommy;
using Microsoft.VisualBasic;
using System.Reflection.Metadata.Ecma335;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
class Program {
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        string media_folder;

        string stringID;
        string token;
        string configdir;
        string message;

        string rss;
        string title;
        string description;
        string link;
        string version;
        bool prefer_config;     // These variables are all used later for the config

        switch (args.Length) {  // Argument №0 is config.toml and argument №1 is the RSS feed XML file (hopefully)
            case 0:
                Console.Write("No command line arguments passed, looking for config.toml in the default directory.");
                Console.WriteLine(" Looking for the RSS feed in the default directory.");
                configdir = "config.toml"; rss = "feed.xml";    // Gotta pray that these are there, otherwise THOU SHALT NOT PASS
                break;
            case 1:
                if (args[0] == "--help" || args[0] == "-h") {   
                    Console.WriteLine("discorss v0.1 - a programm to sync your Discord channel with an RSS feed\nCommand line arguments: [config.toml location] [RSS feed location]\nThe RSS feed file doesn't have to exist on first run, it will be created automatically.");
                    return;             // This is the help message you get when using the '-h' or '--help' argument
                } else {
                    Console.WriteLine("Not enough arguments! Only 1 of 2 is present!\nAttempting to use the available data: checking for config and/or feed and searching in the default directory.");
                    if (args[0].EndsWith(".xml")) {         // No idea how one ends up with one flag, but you gotta try anyway
                        rss = args[0]; configdir = "config.toml";
                    } else if (args[0].EndsWith(".toml")) {
                        configdir = args[0]; rss = "feed.xml";
                    } else {
                        rss = "feed.xml"; configdir = "config.toml";
                    }
                }
                break;
            case 2:
                (configdir, rss) = ArrangeArguments(args);
                break;  // I thought that just in case someone switches them places, I better check which file is which
            default:    // And yes, I am aware these are duplicates of one another. This happened only once, okay?!
                Console.WriteLine("Too many arguments! Using argument 0 for config, and argument 1 for feed.");
                (configdir, rss) = ArrangeArguments(args);
                break;
        }

        TomlTable table;
        try {                               // Trying to read the file and catching the exception if it isn't there
            using StreamReader reader = File.OpenText(configdir);
            try {
                table = TOML.Parse(reader); // Here, attempting to parse it and catching the exception if there are errors in the file
            }                               // the way that the example did it: https://github.com/dezhidki/Tommy?tab=readme-ov-file#how-to-use
            catch (TomlParseException ex) {
                table = ex.ParsedTable;
                Console.WriteLine($"Unable to read the config file properly. Attempting to parse anyway.\nThe following exception occurred: {ex}");

                foreach(TomlSyntaxException syntaxEx in ex.SyntaxErrors)
                    Console.WriteLine($"Error on {syntaxEx.Column}:{syntaxEx.Line}: {syntaxEx.Message}");
            }
            token = table["Discord"]["token"];          // Linking the predefined variables with the information from config.toml
            stringID = table["Discord"]["channel"];

            title = table["RSS"]["title"];
            description = table["RSS"]["description"];
            link = table["RSS"]["link"];
            version = table["RSS"]["rss_version"];
            prefer_config = table["RSS"]["prefer_config"];

            media_folder = table["Local"]["media_folder"];
        } catch {
            Console.WriteLine("Error: unable to find the config file.");
            return;     // I said 'THOU SHALT NOT PASS' and not pass hast thou indeed
        }
        // Console.WriteLine(table["RSS"], typeof(TomlTable));

        XML RSS = new();
        var feed = RSS.GiveBirth(rss, prefer_config, version, title, link, description);
        Console.WriteLine($"RSS Version: {feed.Version}, title: {feed.Channel.title}, Link: {feed.Channel.link},\ndescription: '{feed.Channel.description}'.");

        ulong ChannelID = (ulong)Decimal.Parse(stringID);  

        // DiscordConfiguration DiscordLogConfig = new () {
        //     MinimumLogLevel = LogLevel.Debug,
        //     LogTimestampFormat = "dd MMM yyyy - hh:mm:ss tt"
        // };
        // DiscordClient client = new(DiscordLogConfig);
        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.ConfigureEventHandlers(                     // A bunch of generic D#+ stuff to initialise the bot and handle new messages, all
            b => b.HandleMessageCreated(async (s, e) => {   // from the official guide btw: https://dsharpplus.github.io/DSharpPlus/index.html
                if (e.Channel.Id == ChannelID) {
                    // Console.Write($"Attachement 0 url: {e.Message.Attachments[0].Url}. ");
                    Console.WriteLine($"Message received: «{message = e.Message.Content}»");
                    HttpClient http = new ();
                    foreach (var attachement in e.Message.Attachments) {
                        Console.WriteLine(attachement.Url);
                        var data = await http.GetByteArrayAsync(attachement.Url);
                        Directory.CreateDirectory(media_folder);
                        await File.WriteAllBytesAsync(Path.Combine(media_folder, attachement.FileName), data);
                    }
                }
            }
        ));

        await builder.ConnectAsync();   // This one connects you to Discord
        await Task.Delay(-1);           // You make it -1 so that the program doesn't stop
    }
    static (string, string) ArrangeArguments(string[] args){
        string configdir = (args[0].EndsWith(".toml")) ? _ = args[0] : "config.toml";
        string rss = (args[1].EndsWith(".xml")) ? _ = args[1] : "feed.xml";
        Console.WriteLine($"Argument №0 is '{args[0]}', and №1 is '{args[1]}'.");
        return (configdir, rss);
    }
}