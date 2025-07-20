using System.Xml.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Buffers;
using System.ComponentModel;
using Polly.CircuitBreaker;
using System.Net;

namespace RSS{
    public class XML {
        public RSS GiveBirth(string file, bool preferConfig, string version, string title, string link, string description) {
            if (File.Exists(file)) {                                                // If file exists — deserialise; otherwise create new
                Console.Write("Reading file...");
                XmlSerializer serialiser = new XmlSerializer(typeof(RSS));
                using (FileStream filestream = new FileStream(file, FileMode.Open)) {
                    if (filestream != null) {
                        Console.WriteLine(" Deserialising the XML!");
                        var rss = (RSS)serialiser.Deserialize(filestream);
                        if (rss.Version != version || rss.Channel.title != title || rss.Channel.link != link || rss.Channel.description != description)
                            return (preferConfig) ? assignRSS(rss, version, title, link, description) : rss;
                        return rss;
                    } else {
                        Console.WriteLine(" File is EMPTY!!!");
                        Console.WriteLine("Creating new RSS configuration.");
                        RSS rss = new RSS();
                        return assignRSS(rss, version, title, link, description);
                    }
                }
            } else {
                Console.WriteLine("File absent; creating new RSS configuration.");
                RSS rss = new RSS();
                return assignRSS(rss, version, title, link, description);
            }
        }

        private RSS assignRSS (RSS rss, string version, string title, string link, string description){
            Console.WriteLine("Asigning new RSS data.");
            rss.Version = version; rss.Channel.title = title; rss.Channel.link = link; rss.Channel.description = description;
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
        public string title { get; set; }

        [XmlElement("link")]
        public string link { get; set; }

        [XmlElement("description")]
        public string description { get; set; }

        [XmlElement(ElementName="item", Type=typeof(Item))]
        public List<Item> Items;
    }
    public class Item {
        [XmlElement("title")]
        public string title { get; set; }

        [XmlElement("link")]
        public string link { get; set; }

        [XmlElement("author")]
        public string author { get; set; }

        [XmlElement("description")]
        public string description { get; set; }

        [XmlElement("enclosure")]
        public List<Enclosure> media;
    }
    public class Enclosure {
        [XmlAttribute("url")]
        public string media_url { get; set; }

        [XmlAttribute("length")] 
	    public int length { get; set; } 

        [XmlAttribute("type")]
        public string media_type { get; set; }
    }
}