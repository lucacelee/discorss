using System.Xml.Linq;

namespace RSS{
    public class XML {
        public required string Message { get; set; }
        public static async Task<XElement> Load(string location, string title, string description, string link) {
            XElement RSS;
            try {
                RSS = XElement.Load(location);
            } catch {
                RSS =
                    new XElement("rss version=\"2.0\"");
                new XElement("channel",
                    new XElement("title", title),
                    new XElement("description", description),
                    new XElement("link", link)
                );
            }
            return RSS;
        }
    }
}