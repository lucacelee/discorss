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
        private string Message = M!.Content;

        public string AddMessage() {
            Message = FormatTimestamps(RemoveRoles()) + "\n\n By: " + M.Author!.Username;
            return SetDescription();                             // I'm fairly certain that we won't get an authorless message
        }

        public async Task<Item> ParseMessage() {
            Attachments = (M.Attachments.Count > 0); var Time = M.Timestamp; DefaultTitle = DefaultTitle.Trim() + " ";
            Message = FormatTimestamps(RemoveRoles()) + "\n\n By: " + M.Author!.Username; // Same here
            Item Entry = new() {
                Title = await Task.Run(() => SetTitle(Time)),
                Description = await Task.Run(() => SetDescription()),
                Author = M.Author!.Username,
                Media = [],
                Origin = "Discord",
                Timestamp = Time.ToUnixTimeSeconds()
            };
            Console.WriteLine($"The result of parsing the item.\nTitle: {Entry.Title}, author: {Entry.Author}, Description:\n'{Entry.Description}'");
            return Entry;
        }

        private string SetDescription () {
            Console.WriteLine("Setting the description of the message.");
            if (Message.Length < 100 && Attachments && !Message.Contains('\n'))
                return "";
            Message = FormatLikeXML(Message, false);
            Message = String.Concat(Message[0].ToString().ToUpper(), Message[1..]);
            return Message;
        }

        private string SetTitle (DateTimeOffset time_offset) {
            Console.WriteLine("Setting the title of the item.");
            var CleanMessage = FormatLikeXML(RemoveRoles(), true);
            CleanMessage = String.Concat(CleanMessage[0].ToString().ToUpper(), CleanMessage[1..]);
            var FirstLine = CleanMessage.Split('\n')[0];
            if (FirstLine.Contains('#'))
                FirstLine = FirstLine.Replace('#', ' ');
            if (CleanMessage.Length < 100 && Attachments && !CleanMessage.Contains('\n'))
                return CleanMessage.Trim();
            else if (CleanMessage.Length < 150 && Message.Contains('\n'))
                return FirstLine.Trim();
            else if (FirstLine.Length < 150)
                return FirstLine.Trim();
            else return String.Concat(DefaultTitle, time_offset.DateTime.ToUniversalTime(), " UTC");
        }

        private string RemoveRoles() {
            Console.WriteLine("Removing Discord roles from the string.");
            foreach (string Role in Roles) {
                if (TrimRoles[Roles.IndexOf(Role)]){
                    Console.WriteLine("Trimming for {0} is enabled.", Role);
                    if (Message.StartsWith(Role, StringComparison.Ordinal)) {
                        Console.Write($"Message begins with '{Role}', trimming: ");
                        Message = Message.Remove(0, Role.Length);
                        Message = Message.TrimStart();
                        Console.WriteLine(Message);
                    };
                }
                Message = Message.Replace(Role, RolesReplace[Roles.IndexOf(Role)]);
            }
            return Message;
        }

        private static string FormatTimestamps (string Message) {
            string Timestamp = @"(<t:)(?<Digits>\d+)(:\w>)";
            Console.WriteLine("Reformatting the timestamps.");
            foreach (Match T in Regex.Matches(Message, Timestamp)) {
                string Time = DateTimeOffset.FromUnixTimeSeconds((long)Decimal.Parse(T.Result(@"${Digits}"))).ToUniversalTime().DateTime.ToString();
                Console.WriteLine("Time: {0}", Time);
                Message = Regex.Replace(Message, Timestamp, " " + Time + " UTC");
            }
            return Message;
        }

        private static string FormatLikeXML (string Message, bool RemoveFormatting) {
            string uOnset, uCoda, sOnset, sCoda, cOnset, cCoda, iOnset, iCoda, bOnset, bCoda, jOnset, jCoda, h1Onset, h1Coda, h2Onset, h2Coda, h3Onset, h3Coda;
            uOnset = @"<u>"; uCoda = @"</u>"; sOnset = @"<s>"; sCoda = @"</s>"; bOnset = @"<b>"; bCoda = @"</b>"; iOnset = @"<i>"; iCoda = @"</i>";
            cOnset = "<code>"; cCoda = @"</code>"; jOnset = jCoda = "";
            h1Onset = "<h1>"; h1Coda = "</h1>"; h2Onset = "<h2>"; h2Coda = "</h2>"; h3Onset = "<h3>"; h3Coda = "</h3>";

            if (RemoveFormatting)
                uOnset = uCoda = sOnset = sCoda = cOnset = cCoda = iOnset = iCoda = bOnset = bCoda = jOnset = jCoda = h1Onset = h1Coda = h2Onset = h2Coda = h3Onset = h3Coda = "";

            Message = (!RemoveFormatting) ? Message.Replace("\n", "\n<br>") : Message;

            List<Text> Strings = [];
            if (!RemoveFormatting){
                Strings.Add(new Text {
                    Pattern = @"(#){3}(?<Body>.+)\n",
                    Onset = h3Onset,
                    Coda = h3Coda + '\n',
                    Replacement = @"${Body}"
                });
                Strings.Add(new Text {
                    Pattern = @"(#){2}(?<Body>.+)\n",
                    Onset = h2Onset,
                    Coda = h2Coda + '\n',
                    Replacement = @"${Body}"
                });
                Strings.Add(new Text {
                    Pattern = @"(#)(?<Body>.+)\n",
                    Onset = h1Onset,
                    Coda = h1Coda + '\n',
                    Replacement = @"${Body}"
                });
            }
            Strings.Add(new Text {
                Pattern = @"(_){2}(?<Body>.+)(_){2}",
                Onset = uOnset,
                Coda = uCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(_)(?<Body>.+)(_)",
                Onset = iOnset,
                Coda = iCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(~){2}(?<Body>.+)(~){2}",
                Onset = sOnset,
                Coda = sCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(`){2}(?<Body>.+)(`){2}",
                Onset = cOnset,
                Coda = cCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(`)(?<Body>.+)(`)",
                Onset = cOnset,
                Coda = cCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(\u007c){2}(?<Body>.+)(\u007c){2}",
                Onset = jOnset,
                Coda = jCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(\*){2}(?<Body>.+)(\*){2}",
                Onset = bOnset,
                Coda = bCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(\*)(?<Body>.+)(\*)",
                Onset = iOnset,
                Coda = iCoda,
                Replacement = @"${Body}"
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
            return Message.Replace("<u>", "__").Replace("</u>", "__").Replace("<b>", "**").Replace("</b>", "**").Replace("<i>", "*").Replace("</i>", "*").Replace("<s>", "~~").Replace("</s>", "~~").Replace("<code>", "`").Replace("</code>", "`").Replace("<h1>", "## ").Replace("</h1>", "").Replace("<h2>", "### ").Replace("</h2>", "").Replace("<h3>", "#### ").Replace("</h3>", "").Replace("\n<br>", "\n");
        }
    }
}