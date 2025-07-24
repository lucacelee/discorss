using System.Timers;
using System.IO;
using RSS;
using Formatting;
using DSharpPlus;
using Tommy;
using DSharpPlus.Entities;

class Program {
    private static System.Timers.Timer LinkTimer;
    private static Boolean Linking;
    public static Boolean RelayingRSS;
    public static ulong ChannelID;
    public static DiscordClient Client;
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        string media_folder, token, configdir, rss, Link;
        int linking_time;
        List<string> roles = [];
        List<string> roles_replace = [];
        List<bool> trim_roles = [];
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

            media_folder = table["Local"]["media_folder"];
            rss = table["Local"]["rss_feed_file"];
            Link = table["RSS"]["link"];

            linking_time = (int)Decimal.Parse(table["Discord"]["linking_time"]);

            (roles, roles_replace, trim_roles) = await GetRoles(roles, roles_replace, trim_roles, (TomlArray)table["Discord"]["roles"], (TomlArray)table["Discord"]["inline_roles"], (TomlArray)table["Discord"]["trim_roles"]);
        } catch (Exception ex) {
            Console.WriteLine($"Error: unable to find the config file. {ex}");
            return;     // I said 'THOU SHALT NOT PASS' and not pass hast thou indeed
        }

        if (Path.GetExtension(rss) != ".xml"){
            Console.WriteLine("You must specify the path to the RSS feed."); return;
        }

        XML.FilePath = rss;
        var Feed = XML.GiveBirth(table["RSS"]["prefer_config"], table["RSS"]["rss_version"], table["RSS"]["title"], Link, table["RSS"]["description"]);
        Console.WriteLine($"RSS Version: {Feed.Version}, title: {Feed.Channel.Title}, Link: {Feed.Channel.Link},\ndescription: '{Feed.Channel.Description}'.");

        string WatchPath = Path.GetFullPath(rss).Replace(rss, "");
        Console.WriteLine("Checking directory '{0}' for updates.", WatchPath);
        using var FeedWatcher = new FileSystemWatcher(WatchPath);
        FeedWatcher.NotifyFilter = NotifyFilters.LastWrite;
        FeedWatcher.Changed += XML.UpdateFile;
        FeedWatcher.Filter = "*.xml";
        FeedWatcher.EnableRaisingEvents = true;

        ChannelID = (ulong)Decimal.Parse(table["Discord"]["channel"]);  

        // DiscordConfiguration DiscordLogConfig = new () {         I would love to have more logs and a different date format
        //     MinimumLogLevel = LogLevel.Debug,                    but this looks a bit outdated and I don't know how to do it
        //     LogTimestampFormat = "dd MMM yyyy - hh:mm:ss tt"     the 'modern' way, at least just yet
        // };
        // DiscordClient client = new(DiscordLogConfig);
        HttpClient http = new ();
        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.SetLogLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        builder.ConfigureEventHandlers(                     // A bunch of generic D#+ stuff to initialise the bot and handle new messages, all
            b => b.HandleMessageCreated(async (s, e) => {   // from the official guide btw: https://dsharpplus.github.io/DSharpPlus/index.html
                if (!RelayingRSS) {
                    if (e.Channel.Id == ChannelID) {
                        Console.WriteLine($"Message received: «{e.Message.Content}»");
                        if (!Linking) {
                            SetTimer(linking_time); Linking = true;
                            Item Message = await Discord.ParseMessage(e.Message, roles, roles_replace, trim_roles, table["RSS"]["default"]);
                            var attachements = new List<Enclosure>();
                            await DownloadAttachements(http, e, attachements, media_folder, Link);
                            Message.Media = attachements;
                            Feed.Channel.Items!.Insert(0, Message);
                        } else {
                            StopTimer(); SetTimer(linking_time);
                            Console.WriteLine("Adding this message to the previous post.");
                            Feed.Channel.Items![0].Description = String.Concat(Feed.Channel.Items[0].Description, "<br>\n<br>\n", await Discord.AddMessage(e.Message, roles, roles_replace, trim_roles));
                            await DownloadAttachements(http, e, Feed.Channel.Items[0].Media!, media_folder, Link);
                        }
                        await XML.PutDown(Feed);
                    }
                }
            }
        ));

        Client = builder.Build();
        await Client.ConnectAsync();   // This one connects you to Discord
        await Task.Delay(-1);           // You make it -1 so that the program doesn't stop
    }
    static (string, string) ArrangeArguments(string[] args){
        string configdir = (args[0].EndsWith(".toml")) ? args[0] : "config.toml";
        string rss = (args[1].EndsWith(".xml")) ? args[1] : "feed.xml";
        Console.WriteLine($"Argument №0 is '{args[0]}', and №1 is '{args[1]}'.");
        return (configdir, rss);
    }

    static async Task<(List<string>, List<string>, List<bool>)> GetRoles (List<string> roles, List<string> roles_replace, List<bool> trim_roles, TomlArray toml_roles, TomlArray toml_roles_replace, TomlArray toml_trim_roles) {
        foreach (var node in toml_roles)
            roles.Add(node.ToString()!);
        foreach (var node in toml_roles_replace) 
            roles_replace.Add(node.ToString()!);
        foreach (var node in toml_trim_roles) 
            trim_roles.Add(Boolean.Parse(node.ToString()!));
        return (roles, roles_replace, trim_roles);
    }

    static async Task DownloadAttachements (HttpClient http, DSharpPlus.EventArgs.MessageCreatedEventArgs e, List<Enclosure> attachements, string MediaFolder, string Link) {
        Console.WriteLine("Downloading attachements: ");
        foreach (var attachement in e.Message.Attachments)
        {            // We cycle through every attachement and download it
            Console.Write($"{attachement.Id}, ");
            var data = await http.GetByteArrayAsync(attachement.Url);
            Directory.CreateDirectory(MediaFolder);
            string URL = Path.Combine(MediaFolder, attachement.Id.ToString() + "_" + attachement.FileName!);
            await File.WriteAllBytesAsync(URL, data);
            var A = new Enclosure
            {
                LocalUrl = URL,
                MediaUrl = Path.Combine(Link, URL),
                MediaType = attachement.MediaType!,
                Length = (attachement.MediaType!.Split('/')[0] == "audio" || attachement.MediaType!.Split('/')[0] == "video") ? attachement.MediaType.Length : 0
            };
            attachements.Add(A);
        }
    }

    static void SetTimer(int Time) {
        Console.WriteLine("Starting a timer for {0} seconds!", Time);
        LinkTimer = new System.Timers.Timer(Time*1000);
        LinkTimer.Elapsed += NotLinking!;
        LinkTimer.AutoReset = false;
        LinkTimer.Enabled = true;
    }

    static void StopTimer() {
        Console.WriteLine("Timer closed!");
        LinkTimer.Enabled = false;
    }

    private static void NotLinking(Object source, ElapsedEventArgs e) {
        Linking = false;
        Console.WriteLine("Linking is {0}", Linking);
        Console.WriteLine("Timer elapsed at: {0}", e.SignalTime);
        StopTimer();
    }

    public static void Print (string s) {
        Console.Write(s + " ");
    }

    public static void PrintLn (string s) {
        Console.WriteLine(s);
    }
}