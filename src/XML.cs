using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using DSharpPlus;
using DSharpPlus.Entities;
using Formatting;

namespace RSS{
    public class XML
    {
        private static bool InWriting { get; set; }
        public static string FilePath { get; set; }
        public static async void UpdateFile(object sender, FileSystemEventArgs e) {
            if (Program.RelayingRSS)
                return;
            Console.WriteLine("File update received!");
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;
            if (InWriting)
                return;

            Console.WriteLine("RSS feed updated! Reading changes.");
            XmlSerializer serialiser = new(typeof(RSS));
            using FileStream filestream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (filestream.Position > 0)
                    filestream.Position = 0;
            var rss = (RSS)serialiser.Deserialize(filestream)!;

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
            DiscordChannel Channel = await Program.Client.GetChannelAsync(Program.ChannelID);
            var Message = new DiscordMessageBuilder()
                .WithContent(await Markup.Format(Item.Description, Item.Title, Item.Author));
            foreach (var Embed in Embeds)
                Message.AddEmbed(Embed);
            await Message.SendAsync(Channel);
            await Task.Delay(500);
            Program.RelayingRSS = false;
        }
        public static async Task PutDown(RSS rss) {
            InWriting = true;
            XmlSerializer serializer = new(typeof(RSS));
            using FileStream fileStream = new(FilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            serializer.Serialize(fileStream, rss);
            await fileStream.FlushAsync();
            await Task.Delay(500);
            InWriting = false;
        }
        public static RSS GiveBirth(bool preferConfig, string version, string title, string link, string description) {
            if (File.Exists(FilePath)) {                                                // If file exists — deserialise; otherwise create new
                Console.Write("Reading file...");
                XmlSerializer serialiser = new(typeof(RSS));
                using FileStream filestream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (filestream.Position > 0)
                    filestream.Position = 0;
                try {
                    Console.WriteLine(" Deserialising the XML!");
                    var rss = (RSS)serialiser.Deserialize(filestream)!;
                    if (rss.Version != version || rss.Channel.Title != title || rss.Channel.Link != link || rss.Channel.Description != description)
                        return (preferConfig) ? AssignRSS(version, title, link, description) : rss;
                    return rss;
                } catch {
                    Console.WriteLine("Error: Failed to read file!");
                    Console.WriteLine("Creating new RSS configuration.");
                    return AssignRSS(version, title, link, description);
                }
            } else {
                Console.WriteLine("File absent; creating new RSS configuration.");
                return AssignRSS(version, title, link, description);
            }
        }

        private static RSS AssignRSS (string version, string title, string link, string description){
            Console.WriteLine("Asigning new RSS data.");
            var Channel = new Channel {
                Title = title,
                Link = link,
                Description = description,
                Items = []
            };
            var rss = new RSS {
                Version = version,
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
        public string Link { get; set; }

        [XmlElement("author", Order = 3)]
        public required string Author { get; set; }

        [XmlElement("description", Order = 4)]
        public required string Description { get; set; }

        [XmlElement("origin", Order = 5)]
        public string? Origin { get; set; }

        [XmlElement("enclosure", Order = 6)]
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