using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using DSharpPlus;
using DSharpPlus.Entities;
using Formatting;

namespace RSS{
    public class XML
    {
        private bool InWriting { get; set; }
        public string? FilePath { get; set; }
        public required bool PreferConfig { get; set; }
        public required string Version { get; set; }
        public required string Title { get; set; }
        public required string Link { get; set; }
        public required string Description { get; set; }
        public async void UpdateFile(object sender, FileSystemEventArgs e) {
            if (Program.RelayingRSS)
                return;
            Console.WriteLine("File update received!");
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;
            if (InWriting)
                return;

            Console.WriteLine("RSS feed updated! Reading changes.");
            XmlSerializer serialiser = new(typeof(RSS));
            using FileStream filestream = new(FilePath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (filestream.Position > 0)    // XML.FilePath is set on line 85 in Program.cs, e.g. before FileSystemWatcher is initialised
                    filestream.Position = 0;

            var rss = new RSS();
            try {
                rss = (RSS)serialiser.Deserialize(filestream)!; // There ought to be a file stream if the file was updated, right?
            } catch {
                Console.WriteLine("Failed to load the feed. Any update to the file will be skipped.");
                return;
            }

            if (rss.Channel == null || rss.Channel.Items == null)
                return;
            var Item = rss.Channel.Items[0];
            if (Item.Origin == "Discord")
                return;

            Program.RelayingRSS = true;
            List<DiscordEmbed> Embeds = [];
            Console.WriteLine(Item.Description);
            if (Item.Media != null) {
                foreach (var Medium in Item.Media) {
                    Embeds.Add(new DiscordEmbedBuilder {
                        ImageUrl = Medium.MediaUrl
                    });
                }
            }
            DiscordChannel Channel = await Program.Client!.GetChannelAsync(Program.ChannelID);  // Client is built before connecting to Discord :/
            var Message = new DiscordMessageBuilder()
                .WithContent(Markup.Format(Item.Description, Item.Title, Item.Author));
            foreach (var Embed in Embeds)
                Message.AddEmbed(Embed);
            await Message.SendAsync(Channel);
            await Task.Delay(500);
            Program.RelayingRSS = false;
        }
        public async Task PutDown(RSS rss) {
            InWriting = true;
            XmlSerializer serializer = new(typeof(RSS));
            using FileStream fileStream = new(FilePath!, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            serializer.Serialize(fileStream, rss);  // XML.FilePath is assigned before writing to the file
            await fileStream.FlushAsync();
            await Task.Delay(500);
            InWriting = false;
        }
        public RSS GiveBirth() {
            if (File.Exists(FilePath)) {                                                // If file exists — deserialise; otherwise create new
                Console.Write("Reading file...");
                XmlSerializer serialiser = new(typeof(RSS));
                using FileStream filestream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (filestream.Position > 0)
                    filestream.Position = 0;
                try {
                    Console.WriteLine(" Deserialising the XML!");
                    var rss = (RSS)serialiser.Deserialize(filestream)!;
                    if (rss.Version != Version || rss.Channel!.Title != Title || rss.Channel.Link != Link || rss.Channel.Description != Description)
                        return (PreferConfig) ? AssignRSS() : rss;                      // If 'Channel' isn't there, we catch it
                    return rss;
                } catch {
                    Console.WriteLine("Error: Failed to read file!");
                    Console.WriteLine("Creating new RSS configuration.");
                    return AssignRSS();
                }
            } else {
                Console.WriteLine("File absent; creating new RSS configuration.");
                return AssignRSS();
            }
        }

        private RSS AssignRSS (){
            Console.WriteLine("Asigning new RSS data.");
            var Channel = new Channel {
                Title = Title,
                Link = Link,
                Description = Description,
                Items = []
            };
            var rss = new RSS {
                Version = Version,
                Channel = Channel
            };
            return rss;
        }
    }                                    // All of this is supposed to recreate the structure of an XML file with the RSS feed by the way

    [XmlRootAttribute(ElementName = "rss")]
    public class RSS {
        [XmlAttribute("version")]
        public string? Version { get; set; }

        
        [XmlElement("channel")]
        public Channel? Channel { get; set; }

        // public RSS() {
        //     Channel = new Channel();
        // }
    }

    public class Channel {
        [XmlElement("title", Order = 1)]
        public required string Title { get; set; }

        [XmlElement("link", Order = 2)]
        public required string Link { get; set; }

        [XmlElement("description", Order = 3)]
        public required string Description { get; set; }

        [XmlElement(ElementName="item", Order = 4)]
        public required List<Item> Items { get; set; }
    }
    public class Item {

        [XmlElement("title", Order = 1)]
        public required string Title { get; set; }

        [XmlElement("link", Order = 2)]
        public string? Link { get; set; }

        [XmlElement("author", Order = 3)]
        public required string Author { get; set; }

        [XmlElement("description", Order = 4)]
        public required string Description { get; set; }

        [XmlElement("origin", Order = 5)]
        public string? Origin { get; set; }
        
        [XmlElement("timestamp", Order = 6)]
        public long? Timestamp { get; set; }

        [XmlElement("enclosure", Order = 7)]
        public required List<Enclosure> Media { get; set; }
    }
    public class Enclosure {
        [XmlIgnore]
        public required string LocalUrl { get; set; }
        
        [XmlAttribute("url")]
        public required string MediaUrl { get; set; }

        [XmlAttribute("length")] 
	    public required int Length { get; set; } 

        [XmlAttribute("type")]
        public required string MediaType { get; set; }
    }
}