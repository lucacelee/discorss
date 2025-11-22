using System.Timers;
using System.Text.RegularExpressions;
using RSS;
using Formatting;
using DSharpPlus;
using Tommy;
using DSharpPlus.Entities;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using System.Linq.Expressions;

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
    public string? IdLink { get; set; }
    public string? XmlIdElement { get; set; }
    private RSS.RSS? Feed { get; set; }
    private FileSystemWatcher? FeedWatcher;
    private string? TenorAPI;
    public const string Version = "1.1.6";
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        const string Version = "1.1.6";
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

                        discorss " + Version + "\n");
                        return;                         // This is the help message you get when using the '-h' or '--help' argument
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
            RolesReplace = TmpRolesReplace,
            TrimRoles = TmpTrimRoles,
            IdLink = Table["RSS"]["message_link_format"],
            XmlIdElement = Table["RSS"]["id_xml_element"],
            TenorAPI = Table["Discord"]["tenor_api_key"]
        };

        var XMLFile = new XML {
            FilePath = Instance.RSS,
            PreferConfig = Table["RSS"]["prefer_config"],
            Version = Table["RSS"]["rss_version"],
            Title = Table["RSS"]["title"],
            Link = Instance.Link,
            Description = Table["RSS"]["description"]
        };
        try {
            Instance.Feed = XMLFile.GiveBirth();
            Console.WriteLine($"RSS Version: {Instance.Feed.Version}, title: {Instance.Feed.Channel!.Title}, Link: {Instance.Feed.Channel.Link},\ndescription: '{Instance.Feed.Channel.Description}'.");
        } catch {
            return;
        }
        
        Client = Instance.BuildBuilder(XMLFile, Table["RSS"]["default"]).Build();
                                                    // Feed.Channel has been asigned while birth was being given
        string WatchPath = Path.GetFullPath(Instance.RSS).Replace(Path.GetFileName(Instance.RSS), "");
        Console.WriteLine("Checking directory '{0}' for updates.\n", WatchPath);
        try {
            Instance.FeedWatcher = new FileSystemWatcher(WatchPath) {
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "*.xml",
                EnableRaisingEvents = true
            };
            Instance.FeedWatcher.Changed += async (sender, e) => {
                Instance.Feed = await XMLFile.UpdateFile(sender, e, Instance.Feed, [Instance.IdLink, Instance.XmlIdElement]) ?? Instance.Feed;
            };
        } catch (Exception ex){
            Console.WriteLine("Error surveying the directory, the following exception occurred:\n{0}", ex.Message);
            return;
        }

        ChannelID = (ulong)Decimal.Parse(Table["Discord"]["channel"]);
        var DiscordChannel = await Client.GetChannelAsync(ChannelID);
        string GuildName;
        try { GuildName = DiscordChannel.Guild.Name; }
        catch { GuildName = "[unavailable]"; }
        Console.WriteLine("Connecting to #{0} in '{1}'", DiscordChannel.Name, GuildName);

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

    DiscordClientBuilder BuildBuilder (XML XMLFile, string TitleDefault) {
        HttpClient http = new ();
        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.SetLogLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
        builder.ConfigureEventHandlers(                     // A bunch of generic D#+ stuff to initialise the bot and handle new messages, all
            b => b.HandleMessageCreated(async (s, e) => {   // from the official guide btw: https://dsharpplus.github.io/DSharpPlus/index.html
                if (!RelayingRSS) {
                    if (e.Channel.Id == ChannelID) {
                        Console.WriteLine($"\nMessage received: «{e.Message.Content}»");
                        (string? eMessage, string? TenorGifUrl, float? GifDuration) = await GetTenorURLs(http, e.Message);
                        var MD = new Markdown (e.Message) {
                            Roles = Roles!,
                            RolesReplace = RolesReplace!,
                            TrimRoles = TrimRoles!,
                            DefaultTitle = TitleDefault,
                            Message = (eMessage == null) ? "[GIF]" : ReplaceDiscordJumpLinks(eMessage)
                        };
                        if (!Linking) {                     // Linking — connecting multiple Discord messages into a single RSS entry
                            SetTimer(LinkingTime); Linking = true;
                            Item Message = await MD.ParseMessage();
                            Message.Link = XMLFile.Link + IdLink + Message.Timestamp;   // Later more than just timestamp will be supported hopefully :)
                            var attachements = new List<Enclosure>();
                            await DownloadAttachements(http, e, attachements);
                            if (TenorGifUrl != null) {
                                attachements.Insert(0, new Enclosure {
                                    LocalUrl = "",
                                    MediaUrl = TenorGifUrl,
                                    Length = (int?)GifDuration ?? 0,
                                    MediaType = "image/gif"
                                });
                            }
                            Message.Media = attachements;
                            Feed!.Channel!.Items.Insert(0, Message);
                        } else {
                            Console.WriteLine("Timer closed!");
                            LinkTimer!.Enabled = false; SetTimer(LinkingTime);
                            Console.WriteLine("Adding this message to the previous post.");
                            await DownloadAttachements(http, e, Feed!.Channel!.Items[0].Media);
                            if (TenorGifUrl != null) {
                                Feed!.Channel!.Items[0].Media.Insert(0, new Enclosure {
                                    LocalUrl = "",
                                    MediaUrl = TenorGifUrl,
                                    Length = (int?)GifDuration ?? 0,
                                    MediaType = "image/gif"
                                });
                            }
                            Feed!.Channel.Items[0].Description = String.Concat(Feed.Channel.Items[0].Description, "<br>\n<br>\n", await Task.Run(() => MD.AddMessage()));
                        }
                        await XMLFile.PutDown(Feed);
                    }
                }
            }
        ));
        return builder;
    }

    async Task DownloadAttachements (HttpClient http, DSharpPlus.EventArgs.MessageCreatedEventArgs e, List<Enclosure> Attachements) {
        Console.WriteLine("Downloading attachements: ");
        foreach (var Attachement in e.Message.Attachments) {            // We cycle through every attachement and download it
            Console.Write($"{Attachement.Id}, ");
            byte[] data;
            try {
                data = await http.GetByteArrayAsync(Attachement.Url);
            } catch (Exception ex) {
                Console.WriteLine("\nFAILED TO DOWNLOAD ATTACHEMENT! DROPPING.");
                Console.WriteLine("Attachement URL: {0}", Attachement.Url);
                Console.WriteLine("Error: {0}\n", ex.Message);
                return;
            }
            string FullFolder = Path.GetFullPath(RSS).Replace(Path.GetFileName(RSS), "") + MediaFolder;
            try {
                Directory.CreateDirectory(FullFolder);
            } catch (Exception ex) {
                Console.WriteLine("\nFAILED TO DEFINE THE MEDIA FOLDER!\nError: {0}\nUnable to download attachements, dropping.", ex.Message);
                return;
            }
            string URL;
            string FileName = Attachement.Id.ToString() + "_" + (Attachement.FileName ?? "");
            try {
                URL = Path.Combine(FullFolder, FileName);
                await File.WriteAllBytesAsync(URL, data);   // I doubt that there will be nameless files sent
            } catch (Exception ex) {
                Console.WriteLine("\nFAILED TO WRITE ATTACHEMENT TO FILE! DROPPING.");
                Console.WriteLine("Attachement URL: {0}", Attachement.Url);
                Console.WriteLine("Error: {0}\n", ex.Message);
                return;
            }
            var A = new Enclosure {
                LocalUrl = URL,
                MediaUrl = Path.Combine(Link, MediaFolder, FileName),
                MediaType = Attachement.MediaType!,     // I don't know how can an attachement have no media type
                Length = (Attachement.MediaType!.Split('/')[0] == "audio" || Attachement.MediaType!.Split('/')[0] == "video") ? Attachement.MediaType.Length : 0
            };
            Attachements.Add(A);
            if (Attachements.Capacity == 0)
                Console.WriteLine("no attachements were present.");
        }
    }

    private async Task<(string?, string?, float?)> GetTenorURLs (HttpClient http, DSharpPlus.Entities.DiscordMessage M) {
        string Message = M.Content;
        if (TenorAPI != null) {
            var TenorRegex = new Regex(@"^https://tenor.com/view/(?<GIF>.+)$");
            Console.WriteLine("Getting Tenor GIFs... Match? {0}", TenorRegex.IsMatch(Message));
            try {
                if (TenorRegex.IsMatch(Message)) {
                    Console.WriteLine("It was empty!");
                    var Fugi = TenorRegex.Match(Message);
                    string TenorApiUrl = "https://tenor.googleapis.com/v2/posts?" + "&key=" + TenorAPI + "&client_ley=discorss&ids=" + Fugi.Result(@"${GIF}").Split('-')[^1];
                    var Response = await http.GetFromJsonAsync<JSON.Response>(TenorApiUrl);
                    string? Url = Response!.Results![0].MediaFormats!.Gif!.Url!;
                    float? Duration = Response!.Results![0].MediaFormats!.Gif!.Duration!;
                    Console.WriteLine("Got Tenor URL, here is the result:\n{0}", Url);
                    return (null, Url, Duration);
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Oops, cannot get the GIF!\nError: {0}", ex);
            }
        }
        return (Message, null, null);
    }

    private string ReplaceDiscordJumpLinks (string Message) {
        string DiscordLink = @"(?<!.*\[.+\]\()https://discord\.com/channels/(?<Guild>\d+)/" + ChannelID + @"/(?<Message>\d+)(?:\b|$)";
        string EmbeddedDiscordLink = @"(\[)(?<Title>.*)(\])(\()https://discord\.com/channels/(?<Guild>\d+)/" + ChannelID + @"/(?<Message>\d+)(\))";
        bool FoundLinks = false;
        Console.Write("Checking for links to messages in this channel... ");

        foreach (Match T in Regex.Matches(Message, DiscordLink)) {
            Console.Write("Found one!");
            foreach (var Item in Feed!.Channel!.Items) {
                if (Item.DiscordLink == T.Result(@"https://discord.com/channels/${Guild}/" + ChannelID + @"/${Message}")) {
                    FoundLinks = true;
                    Console.Write(" It matches a message in the feed too!\nItem in question: {0}\n\n", Item.Link);
                    Regex Rgx = new(DiscordLink);
                    Message = Rgx.Replace(Message, "[[Link!]](" + Link + IdLink + Item.Timestamp + ")", 1);
                }
            }
        }

        foreach (Match T in Regex.Matches(Message, EmbeddedDiscordLink)) {
            Console.Write("Found one embedded!");
            foreach (var Item in Feed!.Channel!.Items) {
                if (Item.DiscordLink == T.Result(@"https://discord.com/channels/${Guild}/" + ChannelID + @"/${Message}")) {
                    FoundLinks = true;
                    Console.Write(" It matches a message in the feed too!\nItem in question: {0}\n\n", Item.Link);
                    Regex Rgx = new(EmbeddedDiscordLink);
                    Message = Rgx.Replace(Message, "[" + T.Result(@"${Title}") + "](" + Link + IdLink + Item.Timestamp + ")", 1);
                }
            }
    }
        if (FoundLinks)
            Console.WriteLine("\nMessage, with Discord jump links replaced:\n{0}\n", Message);
        return Message;
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

class JSON {
    public class Response {
        [JsonPropertyName("results")]
        public List<Result>? Results { get; set; }
    }
    public class Result {
        [JsonPropertyName("media_formats")]
        public MediaFormats? MediaFormats { get; set; }
    }

    public class MediaFormats {
        [JsonPropertyName("gif")]
        public Media? Gif { get; set; }
    }

    public class Media {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("duration")]
        public float? Duration { get; set; }
    }
}