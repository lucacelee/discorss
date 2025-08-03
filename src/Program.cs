using System.Timers;
using System.IO;
using RSS;
using Formatting;
using DSharpPlus;
using Tommy;
using DSharpPlus.Entities;

class Program {
    private static System.Timers.Timer? LinkTimer;
    private static Boolean Linking;
    public static Boolean RelayingRSS;
    public static ulong ChannelID;
    public static DiscordClient? Client;
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        string MediaFolder, Token, ConfigDir, rss, Link;
        int LinkingTime;
        List<string> Roles = [];
        List<string> RolesReplace = [];
        List<bool> TrimRoles = [];             // These variables are all used later for the config

        if (args.Length == 0) {
            Console.WriteLine("No arguments are passed! Looking for 'config.toml' in the default directory!");
            ConfigDir = "config.toml";
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

                        discorss 1.0
                                        
");
            return;                         // This is the help message you get when using the '-h' or '--help' argument
        } else {
            ConfigDir = args[0];
        }

        TomlTable table;
        try {                               // Trying to read the file and catching the exception if it isn't there
            using StreamReader reader = File.OpenText(ConfigDir);
            try {
                table = TOML.Parse(reader); // Here, attempting to parse it and catching the exception if there are errors in the file
            }                               // the way that the example did it: https://github.com/dezhidki/Tommy?tab=readme-ov-file#how-to-use
            catch (TomlParseException ex) {
                table = ex.ParsedTable;
                Console.WriteLine($"Unable to read the config file properly. Attempting to parse anyway.\nThe following exception occurred: {ex}");

                foreach(TomlSyntaxException syntaxEx in ex.SyntaxErrors)
                    Console.WriteLine($"Error on {syntaxEx.Column}:{syntaxEx.Line}: {syntaxEx.Message}");
            }
            Token = table["Discord"]["token"];          // Linking the predefined variables with the information from config.toml

            MediaFolder = table["Local"]["media_folder"];
            rss = table["Local"]["rss_feed_file"];
            Link = table["RSS"]["link"];

            LinkingTime = (int)Decimal.Parse(table["Discord"]["linking_time"]);

            (Roles, RolesReplace, TrimRoles) = GetRoles(Roles, RolesReplace, TrimRoles, (TomlArray)table["Discord"]["roles"], (TomlArray)table["Discord"]["inline_roles"], (TomlArray)table["Discord"]["trim_roles"]);
        } catch (Exception ex) {
            Console.WriteLine($"Error: unable to find the config file. {ex.Message}");
            return;     // I said 'THOU SHALT NOT PASS' and not pass hast thou indeed
        }

        if (Path.GetExtension(rss) != ".xml"){
            Console.WriteLine("You must specify the path to the RSS feed."); return;
        }

        var XMLFile = new XML{
            FilePath = rss,
            PreferConfig = table["RSS"]["prefer_config"],
            Version = table["RSS"]["rss_version"],
            Title = table["RSS"]["title"],
            Link = Link,
            Description = table["RSS"]["description"]
        };
        var Feed = XMLFile.GiveBirth();
        Console.WriteLine($"RSS Version: {Feed.Version}, title: {Feed.Channel!.Title}, Link: {Feed.Channel.Link},\ndescription: '{Feed.Channel.Description}'.");
                                                    // Feed.Channel has been asigned while birth was being given
        string WatchPath = Path.GetFullPath(rss).Replace(rss, "");
        Console.WriteLine("Checking directory '{0}' for updates.", WatchPath);
        using var FeedWatcher = new FileSystemWatcher(WatchPath);
        FeedWatcher.NotifyFilter = NotifyFilters.LastWrite;
        FeedWatcher.Changed += XMLFile.UpdateFile;
        FeedWatcher.Filter = "*.xml";
        FeedWatcher.EnableRaisingEvents = true;

        ChannelID = (ulong)Decimal.Parse(table["Discord"]["channel"]);  
        HttpClient http = new ();
        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.SetLogLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        builder.ConfigureEventHandlers(                     // A bunch of generic D#+ stuff to initialise the bot and handle new messages, all
            b => b.HandleMessageCreated(async (s, e) => {   // from the official guide btw: https://dsharpplus.github.io/DSharpPlus/index.html
                if (!RelayingRSS) {
                    if (e.Channel.Id == ChannelID) {
                        Console.WriteLine($"Message received: «{e.Message.Content}»");
                        if (!Linking) {
                            SetTimer(LinkingTime); Linking = true;
                            Item Message = await Markdown.ParseMessage(e.Message, Roles, RolesReplace, TrimRoles, table["RSS"]["default"]);
                            var attachements = new List<Enclosure>();
                            await DownloadAttachements(http, e, attachements, MediaFolder, Link);
                            Message.Media = attachements;
                            Feed.Channel.Items.Insert(0, Message);
                        } else {
                            StopTimer(); SetTimer(LinkingTime);
                            Console.WriteLine("Adding this message to the previous post.");
                            await DownloadAttachements(http, e, Feed.Channel.Items[0].Media, MediaFolder, Link);
                            Feed.Channel.Items[0].Description = String.Concat(Feed.Channel.Items[0].Description, "<br>\n<br>\n", await Task.Run(() => Markdown.AddMessage(e.Message, Roles, RolesReplace, TrimRoles)));
                        }
                        await XMLFile.PutDown(Feed);
                    }
                }
            }
        ));

        Client = builder.Build();
        await Client.ConnectAsync();   // This one connects you to Discord
        await Task.Delay(-1);           // You make it -1 so that the program doesn't stop
    }

    static(List<string>, List<string>, List<bool>) GetRoles (List<string> Roles, List<string> RolesReplace, List<bool> TrimRoles, TomlArray TomlRoles, TomlArray TomlRolesReplace, TomlArray TomlTrimRoles) {
        try {
            foreach (var node in TomlRoles)
                Roles.Add(node.ToString()!);
            foreach (var node in TomlRolesReplace) 
                RolesReplace.Add(node.ToString()!);
            foreach (var node in TomlTrimRoles) 
                TrimRoles.Add(Boolean.Parse(node.ToString()!));
        } catch {                   // There is no use in even ⅔ lists being there, if we need all 3 to properly remove roles
            Console.WriteLine("     Failed to extract role information, is your config set up correctly?");
        } return (Roles, RolesReplace, TrimRoles);
    }

    static async Task DownloadAttachements (HttpClient http, DSharpPlus.EventArgs.MessageCreatedEventArgs e, List<Enclosure> attachements, string MediaFolder, string Link) {
        Console.WriteLine("Downloading attachements: ");
        foreach (var attachement in e.Message.Attachments)
        {            // We cycle through every attachement and download it
            Console.Write($"{attachement.Id}, ");
            var data = await http.GetByteArrayAsync(attachement.Url);
            Directory.CreateDirectory(MediaFolder);
            string FileName;
            if (attachement.FileName == null)
                FileName = "";
            else FileName = attachement.FileName;
            string URL = Path.Combine(MediaFolder, attachement.Id.ToString() + "_" + FileName);
            await File.WriteAllBytesAsync(URL, data);   // I doubt that there will be nameless files sent
            var A = new Enclosure
            {
                LocalUrl = URL,
                MediaUrl = Path.Combine(Link, URL),
                MediaType = attachement.MediaType!,     // I don't know how can an attachement have no media type
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
        LinkTimer!.Enabled = false;             // This is called only after the timer has been set 🡴
    }

    private static void NotLinking(Object source, ElapsedEventArgs e) {
        Linking = false;
        Console.WriteLine("Linking is {0}", Linking);
        Console.WriteLine("Timer elapsed at: {0}", e.SignalTime);
        StopTimer();
    }
}