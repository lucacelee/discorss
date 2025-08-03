using System.Timers;
using System.IO;
using RSS;
using Formatting;
using DSharpPlus;
using Tommy;
using DSharpPlus.Entities;

class Program
{
    private static System.Timers.Timer? LinkTimer;
    private static Boolean Linking;
    public static Boolean RelayingRSS;
    public static ulong ChannelID;
    public static DiscordClient? Client;
    public required string MediaFolder;
    public required string Token;
    public required string RSS { get; set; }
    public required string Link { get; set; }
    public required int LinkingTime { get; set; }
    public required List<string>? Roles { get; set; }
    public required List<string>? RolesReplace { get; set; }
    public required List<bool>? TrimRoles { get; set; }
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        string ConfigPath;

        if (args.Length == 0) {
            Console.WriteLine("No arguments are passed! Looking for 'config.toml' in the default directory!");
            ConfigPath = "config.toml";
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

                        discorss 1.1
                                        
");     return;                         // This is the help message you get when using the '-h' or '--help' argument
        } else {
            ConfigPath = args[0];
        }

        TomlTable Table;
        try {
            Table = ReadConfig(ConfigPath);
        } catch {
            return;
        }
        List<string> TmpRoles = []; List<string> TmpRolesReplace = []; List<bool> TmpTrimRoles = [];
        try {
            foreach (var node in (TomlArray)Table["Discord"]["roles"])
                TmpRoles.Add(node.ToString()!);
            foreach (var node in (TomlArray)Table["Discord"]["inline_roles"]) 
                TmpRolesReplace.Add(node.ToString()!);
            foreach (var node in (TomlArray)Table["Discord"]["trim_roles"]) 
                TmpTrimRoles.Add(Boolean.Parse(node.ToString()!));
        } catch {                   // There is no use in even ⅔ lists being there, if we need all 3 to properly remove roles
            Console.WriteLine("     Failed to extract role information, is your config set up correctly?");
        } 
        var Instance = new Program {
            Token = Table["Discord"]["token"],
            MediaFolder = Table["Local"]["media_folder"],
            RSS = Table["Local"]["rss_feed_file"],
            Link = Table["RSS"]["link"],
            LinkingTime = (int)Decimal.Parse(Table["Discord"]["linking_time"]),
            Roles = TmpRoles,
            RolesReplace = TmpRoles,
            TrimRoles = TmpTrimRoles,
        };

        var XMLFile = new XML {
            FilePath = Instance.RSS,
            PreferConfig = Table["RSS"]["prefer_config"],
            Version = Table["RSS"]["rss_version"],
            Title = Table["RSS"]["title"],
            Link = Instance.Link,
            Description = Table["RSS"]["description"]
        };
        var Feed = XMLFile.GiveBirth();
        Console.WriteLine($"RSS Version: {Feed.Version}, title: {Feed.Channel!.Title}, Link: {Feed.Channel.Link},\ndescription: '{Feed.Channel.Description}'.");
                                                    // Feed.Channel has been asigned while birth was being given
        string WatchPath = Path.GetFullPath(Instance.RSS).Replace(Instance.RSS, "");
        Console.WriteLine("Checking directory '{0}' for updates.", WatchPath);
        using var FeedWatcher = new FileSystemWatcher(WatchPath);
        FeedWatcher.NotifyFilter = NotifyFilters.LastWrite;
        FeedWatcher.Changed += XMLFile.UpdateFile;
        FeedWatcher.Filter = "*.xml";
        FeedWatcher.EnableRaisingEvents = true;

        ChannelID = (ulong)Decimal.Parse(Table["Discord"]["channel"]);  

        Client = Instance.BuildBuilder(Feed, XMLFile, Table["RSS"]["default"]).Build();
        await Client.ConnectAsync();    // This one connects you to Discord
        await Task.Delay(-1);           // You make it -1 so that the program doesn't stop
    }

    static TomlTable ReadConfig (string ConfigDir) {
        var Table = new TomlTable();
        try {                               // Trying to read the file and catching the exception if it isn't there
            using StreamReader reader = File.OpenText(ConfigDir);
            try {
                Table = TOML.Parse(reader); // Here, attempting to parse it and catching the exception if there are errors in the file
            }                               // the way that the example did it: https://github.com/dezhidki/Tommy?tab=readme-ov-file#how-to-use
            catch (TomlParseException ex) {
                Table = ex.ParsedTable;
                Console.WriteLine($"Unable to read the config file properly. Attempting to parse anyway.\nThe following exception occurred: {ex}");

                foreach(TomlSyntaxException syntaxEx in ex.SyntaxErrors)
                    Console.WriteLine($"Error on {syntaxEx.Column}:{syntaxEx.Line}: {syntaxEx.Message}");
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error: unable to find the config file. {ex.Message}");
            throw;     // I said 'THOU SHALT NOT PASS' and not pass hast thou indeed
        }

        if (Path.GetExtension(Table["Local"]["rss_feed_file"]) != ".xml"){
            Console.WriteLine("You must specify the path to the RSS feed."); throw new IOException("Invalid feed file type.");
        }
        return Table;
    }

    DiscordClientBuilder BuildBuilder (RSS.RSS Feed, XML XMLFile, string TitleDefault) {
        HttpClient http = new ();
        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.SetLogLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        builder.ConfigureEventHandlers(                     // A bunch of generic D#+ stuff to initialise the bot and handle new messages, all
            b => b.HandleMessageCreated(async (s, e) => {   // from the official guide btw: https://dsharpplus.github.io/DSharpPlus/index.html
                if (!RelayingRSS) {
                    if (e.Channel.Id == ChannelID) {
                        Console.WriteLine($"Message received: «{e.Message.Content}»");
                        var MD = new Markdown (e.Message) {
                            Roles = Roles!,
                            RolesReplace = RolesReplace!,
                            TrimRoles = TrimRoles!,
                            DefaultTitle = TitleDefault
                        };
                        if (!Linking) {
                            SetTimer(LinkingTime); Linking = true;
                            Item Message = await MD.ParseMessage();
                            var attachements = new List<Enclosure>();
                            await DownloadAttachements(http, e, attachements, MediaFolder, Link);
                            Message.Media = attachements;
                            Feed.Channel!.Items.Insert(0, Message);
                        } else {
                             Console.WriteLine("Timer closed!");
                            LinkTimer!.Enabled = false; SetTimer(LinkingTime);
                            Console.WriteLine("Adding this message to the previous post.");
                            await DownloadAttachements(http, e, Feed.Channel!.Items[0].Media, MediaFolder, Link);
                            Feed.Channel.Items[0].Description = String.Concat(Feed.Channel.Items[0].Description, "<br>\n<br>\n", await Task.Run(() => MD.AddMessage()));
                        }
                        await XMLFile.PutDown(Feed);
                    }
                }
            }
        ));
        return builder;
    }

    static async Task DownloadAttachements (HttpClient http, DSharpPlus.EventArgs.MessageCreatedEventArgs e, List<Enclosure> Attachements, string MediaFolder, string Link) {
        Console.WriteLine("Downloading attachements: ");
        foreach (var Attachement in e.Message.Attachments) {            // We cycle through every attachement and download it
            Console.Write($"{Attachement.Id}, ");
            byte[] data;
            try {
                data = await http.GetByteArrayAsync(Attachement.Url);
            } catch (Exception ex) {
                Console.WriteLine("\nFAILED TO DOWNLOAD ATTACHEMENT! DROPPING.");
                Console.WriteLine("Attachement URL: {0}", Attachement.Url);
                Console.WriteLine("The following exception occurred: {0}\n", ex.Message);
                return;
            }
            Directory.CreateDirectory(MediaFolder);
            string FileName;
            if (Attachement.FileName == null)
                FileName = "";
            else FileName = Attachement.FileName;
            string URL = Path.Combine(MediaFolder, Attachement.Id.ToString() + "_" + FileName);
            try {
                await File.WriteAllBytesAsync(URL, data);   // I doubt that there will be nameless files sent
            } catch (Exception ex) {
                Console.WriteLine("\nFAILED TO WRITE ATTACHEMENT TO FILE! DROPPING.");
                Console.WriteLine("Attachement URL: {0}", Attachement.Url);
                Console.WriteLine("The following exception occurred: {0}\n", ex.Message);
                return;
            }
            var A = new Enclosure
            {
                LocalUrl = URL,
                MediaUrl = Path.Combine(Link, URL),
                MediaType = Attachement.MediaType!,     // I don't know how can an attachement have no media type
                Length = (Attachement.MediaType!.Split('/')[0] == "audio" || Attachement.MediaType!.Split('/')[0] == "video") ? Attachement.MediaType.Length : 0
            };
            Attachements.Add(A);
            if (Attachements.Capacity == 0)
                Console.WriteLine("no attachements were present.");
        }
    }

    static void SetTimer(int Time) {
        Console.WriteLine("Starting a timer for {0} seconds!", Time);
        LinkTimer = new System.Timers.Timer(Time*1000);
        LinkTimer.Elapsed += NotLinking!;
        LinkTimer.AutoReset = false;
        LinkTimer.Enabled = true;
    }

    private static void NotLinking(Object source, ElapsedEventArgs e) {
        Linking = false;
        Console.WriteLine("Linking is {0}", Linking);
        Console.WriteLine("Timer elapsed at: {0}", e.SignalTime);
        Console.WriteLine("Timer closed!");
        LinkTimer!.Enabled = false;
    }
}