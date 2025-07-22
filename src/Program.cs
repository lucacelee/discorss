using System.Timers;
using RSS;
using Formatting;
using DSharpPlus;
using Tommy;

class Program {
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        string media_folder, stringID, token, configdir, rss, title, description, link, version, default_title;
        List<string> roles = [];
        List<string> roles_replace = [];
        bool prefer_config;                 // These variables are all used later for the config

        if (args.Length == 0) {
            Console.WriteLine("No arguments are passed! Looking for 'config.toml' in the default directory!");
            configdir = "config.toml";
        } else if (args[0] == "--help" || args[0] == "-h") {    // Thanks to http://patorjk.com/software/taag/ for the fun ASCII art!
            Console.Write(@"
       ░██ ░██                                                                
       ░██                                                                    
 ░████████ ░██ ░███████   ░███████   ░███████  ░██░████  ░███████   ░███████  
░██    ░██ ░██░██        ░██    ░██ ░██    ░██ ░███     ░██        ░██        
░██    ░██ ░██ ░███████  ░██        ░██    ░██ ░██       ░███████   ░███████  
░██   ░███ ░██       ░██ ░██    ░██ ░██    ░██ ░██             ░██        ░██ 
 ░█████░██ ░██ ░███████   ░███████   ░███████  ░██       ░███████   ░███████  
                                                                             
           A simple tool to link a Discord channel and an RSS feed.

           Use: $ discross (-h | --help) [config file path]
                     -h | --help  —  display this message and exit.
                     [config]     —  file path to your config file;
                                     it must be a .toml file.
            If not specified, the app looks in its directory for a
            'config.toml'. If that isn't present either, exit.

            The RSS feed file is specified in the config. If empty,
            the program exists with a message, asking to specify it
                                        
");
            return;                         // This is the help message you get when using the '-h' or '--help' argument
        } else {
            configdir = args[0];
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
            default_title = table["RSS"]["default"];
            prefer_config = table["RSS"]["prefer_config"];

            media_folder = table["Local"]["media_folder"];
            rss = table["Local"]["rss_feed_file"];

            (roles, roles_replace) = await GetRoles(roles, roles_replace, (TomlArray)table["Discord"]["roles"], (TomlArray)table["Discord"]["inline_roles"]);
        } catch (Exception ex) {
            Console.WriteLine($"Error: unable to find the config file. {ex}");
            return;     // I said 'THOU SHALT NOT PASS' and not pass hast thou indeed
        }

        if (Path.GetExtension(rss) != ".xml"){
            Console.WriteLine("You must specify the path to the RSS feed."); return;
        }

        var Feed = XML.GiveBirth(rss, prefer_config, version, title, link, description);
        Console.WriteLine($"RSS Version: {Feed.Version}, title: {Feed.Channel.Title}, Link: {Feed.Channel.Link},\ndescription: '{Feed.Channel.Description}'.");

        ulong ChannelID = (ulong)Decimal.Parse(stringID);  

        // DiscordConfiguration DiscordLogConfig = new () {         I would love to have more logs and a different date format
        //     MinimumLogLevel = LogLevel.Debug,                    but this looks a bit outdated and I don't know how to do it
        //     LogTimestampFormat = "dd MMM yyyy - hh:mm:ss tt"     the 'modern' way, at least just yet
        // };
        // DiscordClient client = new(DiscordLogConfig);
        HttpClient http = new ();
        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.ConfigureEventHandlers(                     // A bunch of generic D#+ stuff to initialise the bot and handle new messages, all
            b => b.HandleMessageCreated(async (s, e) => {   // from the official guide btw: https://dsharpplus.github.io/DSharpPlus/index.html
                if (e.Channel.Id == ChannelID) {
                    // Console.Write($"Attachement 0 url: {e.Message.Attachments[0].Url}. ");
                    Console.WriteLine($"Message received: «{e.Message.Content}»");
                    Item Message = await Discord.ParseMessage(e.Message, roles, roles_replace, default_title);
                    var attachements = new List<Enclosure>();
                    Console.WriteLine("Downloading attachements: ");
                    foreach (var attachement in e.Message.Attachments) {            // We cycle through every attachement and download it
                        Console.Write($"{attachement.Id}, ");
                        var data = await http.GetByteArrayAsync(attachement.Url);
                        Directory.CreateDirectory(media_folder);
                        string URL = Path.Combine(media_folder, attachement.FileName!);
                        await File.WriteAllBytesAsync(URL, data);
                        var A = new Enclosure {
                            LocalUrl = URL,
                            MediaUrl = Path.Combine("link", URL),
                            MediaType = attachement.MediaType!,
                            Length = (attachement.MediaType!.Split('/')[0] == "audio") ? attachement.MediaType.Length : 0
                        };
                        attachements.Add(A);
                    } Message.Media = attachements;
                    Feed.Channel.Items.Add(Message);
                }
            }
        ));

        await builder.ConnectAsync();   // This one connects you to Discord
        await Task.Delay(-1);           // You make it -1 so that the program doesn't stop
    }
    static (string, string) ArrangeArguments(string[] args){
        string configdir = (args[0].EndsWith(".toml")) ? args[0] : "config.toml";
        string rss = (args[1].EndsWith(".xml")) ? args[1] : "feed.xml";
        Console.WriteLine($"Argument №0 is '{args[0]}', and №1 is '{args[1]}'.");
        return (configdir, rss);
    }

    static async Task<(List<string>, List<string>)> GetRoles (List<string> roles, List<string> roles_replace, TomlArray toml_roles, TomlArray toml_roles_replace) {
        foreach (var node in toml_roles)
            roles.Add(node.ToString()!);
        foreach (var node in toml_roles_replace) 
            roles_replace.Add(node.ToString()!);
        return (roles, roles_replace);
    }

    public static void Print (string s) {
        Console.Write(s + " ");
    }

    public static void PrintLn (string s) {
        Console.WriteLine(s);
    }
}