using System.Xml.Serialization;

namespace RSS{
    public class XML {
        public static async Task PutDown(string File, RSS rss) {
            XmlSerializer serializer = new(typeof(RSS));
            using FileStream fileStream = new(File, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            serializer.Serialize(fileStream, rss);
            await fileStream.FlushAsync();
        }
        public static RSS GiveBirth(string file, bool preferConfig, string version, string title, string link, string description) {
            if (File.Exists(file)) {                                                // If file exists — deserialise; otherwise create new
                Console.Write("Reading file...");
                XmlSerializer serialiser = new(typeof(RSS));
                using FileStream filestream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (filestream != null) {
                    Console.WriteLine(" Deserialising the XML!");
                    var rss = (RSS)serialiser.Deserialize(filestream)!;
                    if (rss.Version != version || rss.Channel.Title != title || rss.Channel.Link != link || rss.Channel.Description != description)
                        return (preferConfig) ? AssignRSS(version, title, link, description) : rss;
                    return rss;
                } else {
                    Console.WriteLine(" File is EMPTY!!!");
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
        [XmlElement("title")]
        public required string Title { get; set; }

        [XmlElement("link")]
        public required string Link { get; set; }

        [XmlElement("description")]
        public required string Description { get; set; }

        [XmlElement(ElementName="item", Type=typeof(Item))]
        public required List<Item> Items;
    }
    public class Item {

        [XmlElement("title")]
        public required string Title { get; set; }

        [XmlElement("link")]
        public string Link { get; set; }

        [XmlElement("author")]
        public required string Author { get; set; }

        [XmlElement("description")]
        public required string Description { get; set; }

        [XmlElement("origin")]
        public string? Origin { get; set; }

        [XmlElement("enclosure")]
        public required List<Enclosure> Media;
    }
    public class Enclosure {
        public required string LocalUrl { get; set; }
        
        [XmlAttribute("url")]
        public required string MediaUrl { get; set; }

        [XmlAttribute("length")] 
	    public required int Length { get; set; } 

        [XmlAttribute("type")]
        public required string MediaType { get; set; }
    }
}