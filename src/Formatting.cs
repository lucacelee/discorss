using System.Text.RegularExpressions;
using RSS;

namespace Formatting {
    public class Markdown(DSharpPlus.Entities.DiscordMessage M)
    {
        public required List<string> Roles { get; set; }
        public required List<string> RolesReplace { get; set; }
        public required List<bool> TrimRoles { get; set; }
        public required string DefaultTitle { get; set; }
        private bool Attachments { get; set; }
        public required string Message { get; set; }
        public string? CustomLinkRoot { get; set; }

        public string AddMessage() {
            Message = FormatTimestamps(RemoveRoles()) + "\nBy: _" + M.Author!.Username + "_";
            return SetDescription();                             // I'm fairly certain that we won't get an authorless message
        }

        public async Task<Item> ParseMessage() {
            Attachments = (M.Attachments.Count > 0); var Time = M.Timestamp; DefaultTitle = DefaultTitle.Trim() + " ";
            Message = FormatTimestamps(RemoveRoles()); // Same here
            Item Entry = new() {
                Title = await Task.Run(() => SetTitle(Time)),
                Description = await Task.Run(() => SetDescription()),
                Author = M.Author!.Username,
                Media = [],
                Origin = "Discord",
                DiscordLink = M.JumpLink.ToString(),
                Timestamp = Time.ToUnixTimeSeconds(),
                PubDate = Time.UtcDateTime.ToString() + " UTC"
            };
            Console.WriteLine($"The result of parsing the item.\nTitle: {Entry.Title}, author: {Entry.Author}, Description:\n'{Entry.Description}'");
            return Entry;
        }

        private string SetDescription () {
            Console.WriteLine("\nSetting the description of the message.");
            if (Message.Length < 100 && Attachments && !Message.Contains('\n'))     // Why? Look down.
                return "";
            if (Message.Contains('\n')){
                string FirstLine = Message.Split('\n')[0];
                if (FirstLine.Length < 150)
                    Message = Message.Replace(FirstLine + "\n", "");
            }
            Message = FormatLikeXML(Message, false, CustomLinkRoot!);
            Message = String.Concat(Message[0].ToString().ToUpper(), Message[1..]);
            return Message;
        }

        private string SetTitle (DateTimeOffset TimeOffset) {
            Console.WriteLine("\nSetting the title of the item.");
            var CleanMessage = FormatLikeXML(RemoveRoles(), true, CustomLinkRoot!);
            CleanMessage = String.Concat(CleanMessage[0].ToString().ToUpper(), CleanMessage[1..]);
            var FirstLine = CleanMessage.Split('\n')[0];
            if (FirstLine.Contains('#'))
                FirstLine = FirstLine.Replace('#', ' ');
            if (CleanMessage.Length < 100 && Attachments && !CleanMessage.Contains('\n'))
                return CleanMessage.Trim();                                         // You see this here?
            else if (CleanMessage.Length < 150 && Message.Contains('\n'))           // It becomes the title
                return FirstLine.Trim();
            else if (FirstLine.Length < 150)
                return FirstLine.Trim();
            else return String.Concat(DefaultTitle, TimeOffset.UtcDateTime, " UTC");
        }

        private string RemoveRoles() {
            Console.WriteLine("\nRemoving Discord roles from the string.");
            foreach (string Role in Roles) {
                if (TrimRoles[Roles.IndexOf(Role)]){
                    Console.WriteLine("Trimming for {0} is enabled.", Role);
                    if (Message.StartsWith(Role, StringComparison.Ordinal)) {
                        Console.Write($"Message begins with '{Role}', trimming: ");
                        Message = Message[Role.Length..].TrimStart();
                        Console.WriteLine(Message);
                    };
                }
                Message = Message.Replace(Role, RolesReplace[Roles.IndexOf(Role)]);
                Console.WriteLine("Replacing '{0}' with '{1}'", Role, RolesReplace[Roles.IndexOf(Role)]);
            }
            return Message;
        }

        private static string FormatTimestamps (string Message) {
            string Timestamp = @"(<t:)(?<Digits>\d+):(?<Type>\w)>";
            Console.WriteLine("Reformatting the timestamps. \nFound timestamps: ");
            foreach (Match T in Regex.Matches(Message, Timestamp)) {
                DateTimeOffset TimeUTC = DateTimeOffset.FromUnixTimeSeconds((long)Decimal.Parse(T.Result(@"${Digits}"))).ToUniversalTime().DateTime;
                string ConvertedTime = "";
                Console.Write(T + " ");
                string Result = T.Result(@"${Type}");
                Console.Write(Result + " ");
                switch (Result) {
                    case "t":
                        ConvertedTime = TimeUTC.ToString("HH:mm ") + "GMT";
                        break;
                    case "T":
                        ConvertedTime = TimeUTC.ToString("HH:mm:ss ") + "GMT";
                        break;
                    case "d":
                        ConvertedTime = TimeUTC.ToString("dd.MM.yyyy");
                        break;
                    case "D":
                        ConvertedTime = TimeUTC.ToString("dd MMMM yyyy");
                        break;
                    case "f":
                        ConvertedTime = TimeUTC.ToString("dd MMMM yyyy HH:mm ") + "GMT";
                        break;
                    case "F":
                        ConvertedTime = TimeUTC.ToString("dddd, dd MMMM yyyy HH:mm ") + "GMT";
                        break;
                    case "R":
                        string TimeOffset = (TimeUTC - DateTime.UtcNow).ToString();
                        ConvertedTime = Regex.Replace((TimeOffset[0] == '-') ? (TimeOffset[1..] + " ago") : ("in " + TimeOffset), @"(\.\d+)$", "").Replace(".", " days ");
                        break;
                } 
                Console.WriteLine("Time: {0}", ConvertedTime);
                Regex Rgx = new(Timestamp);
                Message = Rgx.Replace(Message, ConvertedTime, 1); 
            }
            return Message;
        }

        private static string FormatLikeXML (string Message, bool RemoveFormatting, string CustomLinkRoot) {
            Console.WriteLine("Message before formatting:\n{0}\n", Message);
            string uOnset, uCoda, sOnset, sCoda, cOnset, cCoda, iOnset, iCoda, bOnset, bCoda, jOnset, jCoda, h1Onset, h1Coda, h2Onset, h2Coda, h3Onset, h3Coda;
            uOnset = @"<u>"; uCoda = @"</u>"; sOnset = @"<s>"; sCoda = @"</s>"; bOnset = @"<b>"; bCoda = @"</b>"; iOnset = @"<i>"; iCoda = @"</i>";
            cOnset = "<code>"; cCoda = @"</code>"; jOnset = jCoda = "";
            h1Onset = "<h1>"; h1Coda = "</h1>"; h2Onset = "<h2>"; h2Coda = "</h2>"; h3Onset = "<h3>"; h3Coda = "</h3>";

            if (RemoveFormatting)
                uOnset = uCoda = sOnset = sCoda = cOnset = cCoda = iOnset = iCoda = bOnset = bCoda = jOnset = jCoda = h1Onset = h1Coda = h2Onset = h2Coda = h3Onset = h3Coda = "";

            Message = (!RemoveFormatting) ? Message.Replace("\n", "\n<br>") : Message;

            List<Text> Strings = [];        // I want to clarify that 'Strings' is a list of configurations, which all then get executed one by one in
            if (!RemoveFormatting){         // the order in which they are added. That's why placing the more complicated patterns first is important;
                Strings.Add(new Text {
                    Pattern = @"(?:^|<br>)(#{3})[ \t]*(?<Body>.+)\s*(?:<br>)?",
                    Onset = h3Onset,
                    Coda = h3Coda,
                    Replacement = @"${Body}"
                });
                Strings.Add(new Text {
                    Pattern = @"(?:^|<br>)(#{2})[ \t]*(?<Body>.+)\s*(?:<br>)?",
                    Onset = h2Onset,
                    Coda = h2Coda,
                    Replacement = @"${Body}"
                });
                Strings.Add(new Text {
                    Pattern = @"(?:^|<br>)(#{1})[ \t]*(?<Body>.+)\s*(?:<br>)?",
                    Onset = h1Onset,
                    Coda = h1Coda,
                    Replacement = @"${Body}"
                });
            }
            Strings.Add(new Text {                                                  // Matching a Discord jump link
                Pattern = @"(?<!.*\[.+\]\()https://discord.com/channels/(?<CustomLink>\d+/\d+/\d+)(?:\b|$)",
                Onset = "",
                Coda = "",
                Replacement = "[[Discord Link!]](https://discord.com/channels/${CustomLink})"
            });
            Strings.Add(new Text {                                                  // Matching a link
                Pattern = @"(?<!.*\[.+\]\()https://(?<Link>.+?\.[\d\w\./\?=-]+)(?:\b|$)",
                Onset = "",
                Coda = "",
                Replacement = "<a href=\"https://${Link}\">https://${Link}</a>"
            });
            Strings.Add(new Text {
                Pattern = @"(\[)(?<Title>.*)(\])(\()(?<Link>.*)(\))",
                Onset = "",
                Coda = "",
                Replacement = "<a href=\"${Link}\">${Title}</a>"
            });
            Strings.Add(new Text {
                Pattern = @"(\*){3}(?<Body1>.*)(\*){2}(?<Body2>.*)(\*){1}",
                Onset = "",
                Coda = "",
                Replacement = iOnset + bOnset + @"${Body1}" + bCoda + "${Body2}" + iCoda
            });
            Strings.Add(new Text {
                Pattern = @"(\*){1}(?<Body1>.*)(\*){2}(?<Body2>.*)(\*){3}",
                Onset = "",
                Coda = "",
                Replacement = iOnset + @"${Body1}" + bOnset + "${Body2}" + bCoda + iCoda
            });
            Strings.Add(new Text {
                Pattern = @"(\*){3}(?<Body>.*?)(\*){3}",
                Onset = iOnset + bOnset,
                Coda = bCoda + iOnset,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(\*){2}(?<Body>.*?)(\*){2}",
                Onset = bOnset,
                Coda = bCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(\*){1}(?<Body>.*?)(\*){1}",
                Onset = iOnset,
                Coda = iCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(_){2}(?<Body>.*?)(_){2}",
                Onset = uOnset,
                Coda = uCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(_){1}(?<Body>.*?)(_){1}",
                Onset = iOnset,
                Coda = iCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(~){2}(?<Body>.*?)(~){2}",
                Onset = sOnset,
                Coda = sCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(?s)(`){3}(?<Body>.*?)(`){3}",
                Onset = cOnset,
                Coda = cCoda + "<br>\n",
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(`){2}(?<Body>.*?)(`){2}",
                Onset = cOnset,
                Coda = cCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(`){1}(?<Body>.*?)(`){1}",
                Onset = cOnset,
                Coda = cCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(\u007c){2}(?<Body>.*?)(\u007c){2}",
                Onset = jOnset,
                Coda = jCoda,
                Replacement = @"${Body}"
            });
            foreach (var Extract in Strings)
                Message = Extract.Replace(Message);
            return Message;
        }
    }

    class Text {
        public required string Pattern;
        public required string Onset;
        public required string Coda;
        public required string Replacement;

        public string Replace(string Message) {
            return Regex.Replace(Message, Pattern, Onset + Replacement + Coda);
        }
    }

    public class Markup {
        public static string Format(string Description, string Title, string Author) {
            string Header = "# " + Title + "\n";
            string Body = Reformat(Description);
            return String.Concat(Header, Body, "\nBy: ", Author);
        }

        private static string Reformat(string Message) {
            return Message.Replace("<u>", "__").Replace("</u>", "__").Replace("<b>", "**").Replace("</b>", "**").Replace("<i>", "*").Replace("</i>", "*").Replace("<s>", "~~").Replace("</s>", "~~").Replace("<code>", "`").Replace("</code>", "`").Replace("<h1>", "## ").Replace("</h1>", "").Replace("<h2>", "### ").Replace("</h2>", "").Replace("<h3>", "#### ").Replace("</h3>", "").Replace("\n", "").Replace("<br>", "\n");
        }
    }
}