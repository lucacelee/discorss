using System.Xml.Serialization;

namespace RSS{
    public class XML {
        public static RSS GiveBirth(string file, bool preferConfig, string version, string title, string link, string description) {
            if (File.Exists(file)) {                                                // If file exists — deserialise; otherwise create new
                Console.Write("Reading file...");
                XmlSerializer serialiser = new(typeof(RSS));
                using FileStream filestream = new(file, FileMode.Open);
                if (filestream != null) {
                    Console.WriteLine(" Deserialising the XML!");
                    var rss = (RSS)serialiser.Deserialize(filestream)!;
                    if (rss.Version != version || rss.Channel.Title != title || rss.Channel.Link != link || rss.Channel.Description != description)
                        return (preferConfig) ? AssignRSS(rss, version, title, link, description) : rss;
                    return rss;
                } else {
                    Console.WriteLine(" File is EMPTY!!!");
                    Console.WriteLine("Creating new RSS configuration.");
                    RSS rss = new ();
                    return AssignRSS(rss, version, title, link, description);
                }
            } else {
                Console.WriteLine("File absent; creating new RSS configuration.");
                RSS rss = new();
                return AssignRSS(rss, version, title, link, description);
            }
        }

        private static RSS AssignRSS (RSS rss, string version, string title, string link, string description){
            Console.WriteLine("Asigning new RSS data.");
            rss.Version = version; rss.Channel.Title = title; rss.Channel.Link = link; rss.Channel.Description = description;
            return rss;
        }
    }                                    // All of this is supposed to recreate the structure of an XML file with the RSS feed by the way

    [XmlRootAttribute(ElementName = "rss")]
    public class RSS {
        [XmlAttribute("version")]
        public string Version { get; set; }

        
        [XmlElement("channel")]
        public Channel Channel { get; set; }

        public RSS() {
            Channel = new Channel();
        }
    }

    public class Channel {
        [XmlElement("title")]
        public string Title { get; set; }

        [XmlElement("link")]
        public string Link { get; set; }

        [XmlElement("description")]
        public string Description { get; set; }

        [XmlElement(ElementName="item", Type=typeof(Item))]
        public List<Item> Items;
    }
    public class Item {
        [XmlElement("title")]
        public string Title { get; set; }

        [XmlElement("link")]
        public string Link { get; set; }

        [XmlElement("author")]
        public string Author { get; set; }

        [XmlElement("description")]
        public string Description { get; set; }

        [XmlElement("enclosure")]
        public List<Enclosure> Media;
    }
    public class Enclosure {
        public string LocalUrl { get; set; }
        
        [XmlAttribute("url")]
        public string MediaUrl { get; set; }

        [XmlAttribute("length")] 
	    public int Length { get; set; } 

        [XmlAttribute("type")]
        public string MediaType { get; set; }
    }
}