using System.Text.RegularExpressions;
using RSS;

namespace Formatting {
    public class Discord {
        public static async Task<string> AddMessage(DSharpPlus.Entities.DiscordMessage M, List<string> roles, List<string> roles_replace) {
            var Message = FormatTimestamps(Dehashtagise(RemoveRoles(M.Content, roles, roles_replace)));
            return await SetDescription(Message, (M.Attachments.Count > 0));
        }
        public static async Task<Item> ParseMessage(DSharpPlus.Entities.DiscordMessage M, List<string> roles, List<string> roles_replace, string title_default) {
            var attachements = (M.Attachments.Count > 0); var time = M.Timestamp; title_default = title_default.Trim() + " ";
            var message = FormatTimestamps(Dehashtagise(RemoveRoles(M.Content, roles, roles_replace)));
            Item Message = new() {
                Title = await SetTitle(message, attachements, time, roles, roles_replace, title_default),
                Description = await SetDescription(message, attachements),
                Author = M.Author!.Username,
                Origin = "Discord"
            };
            Console.WriteLine($"The result of parsing the item.\nTitle: {Message.Title}, author: {Message.Author}, Description:\n'{Message.Description}'");
            return Message;
        }

        private static async Task<string> SetDescription (string message, bool attachements) {
            Console.WriteLine("Setting the description of the message.");
            if (message.Length < 100 && attachements && !message.Contains('\n'))
                return "";
            string[] lines = message.Split('\n');       // Removing all of the possible leading hashtags from the Discord message
            for (int i = 0; i < lines.Length; i++) {
                lines[i] = (lines[i].StartsWith('#')) ? SplitOnWhitespace(lines[i], 2, 1, ' ') : lines[i];  // Couldn't use a foreach loop neatly
            }
            message = FormatLikeXML(EscapeXML(String.Join("\n", lines)), false);
            return message;
        }

        private static async Task<string> SetTitle (string message, bool attachements, DateTimeOffset time_offset, List<string> roles, List<string> roles_replace, string title_default) {
            Console.WriteLine("Setting the title of the item.");
            var CleanMessage = FormatLikeXML(RemoveRoles(message, roles, roles_replace), true);
            var FirstLine = SplitOnWhitespace(CleanMessage, 2, 0, '\n');
            if (message.Length < 100 && attachements && !message.Contains('\n'))
                return EscapeXML(CleanMessage);
            else if (message.Length < 150 && message.Contains('\n'))
                return EscapeXML(FirstLine);
            else if (message.StartsWith('#'))
                return EscapeXML(SplitOnWhitespace(FirstLine, 2, 1, ' '));
            else if (FirstLine.Length < 150)
                return EscapeXML(FirstLine);
            else return String.Concat(title_default, time_offset.DateTime.ToUniversalTime(), " UTC");
        }

        private static string RemoveRoles(string message, List<string> roles, List<string> roles_replace) {
            Console.WriteLine("Removing Discord roles from the string.");
            foreach (string role in roles) {
                if (message.StartsWith(role, StringComparison.Ordinal)) {
                    Console.Write($"Message begins with '{role}', trimming: ");
                    message = message.Remove(0, role.Length);
                    message = message.TrimStart();
                    Console.WriteLine(message);
                };
                message = message.Replace(role, roles_replace[roles.IndexOf(role)]);
            }
            return message;
        }

        private static string Dehashtagise (string Message) {
            Text Extract = new() {
                Pattern = @"(#){1,3}\s(?<Body>.+)\s(#){1,3}",
                Onset = "",
                Coda = "",
                Replacement = @"\*\*${Body}\*\*"
            };
            return Extract.Replace(Message);
        }

        private static string FormatTimestamps (string Message) {
            string Timestamp = @"\s(<t:)(?<Digits>\d+)(:\w>)\s";
            Console.WriteLine("Reformatting the timestamps.");
            foreach (Match T in Regex.Matches(Message, Timestamp)) {
                string Time = DateTimeOffset.FromUnixTimeSeconds((long)Decimal.Parse(T.Result(@"${Digits}"))).ToUniversalTime().DateTime.ToString();
                Console.WriteLine("Time: {0}", Time);
                Message = Regex.Replace(Message, Timestamp, " " + Time + " UTC");
            }
            return Message;
        }

        private static string EscapeXML (string Message) {
            return Message.Replace("'", "&apos").Replace("\"", "&quot").Replace("<", "&lt").Replace(">", "&gt");
        }

        private static string SplitOnWhitespace (string message, int parts, int part, char space) {
            Console.WriteLine($"Splitting the string! Parts: {parts}, chosing №{part}.");
            return message.Split(space, parts, StringSplitOptions.TrimEntries)[part];
        }

        private static string FormatLikeXML (string Message, bool RemoveFormatting) {
            string uOnset, uCoda, sOnset, sCoda, cOnset, cCoda, iOnset, iCoda, bOnset, bCoda, jOnset, jCoda;
            uOnset = @"<u>"; uCoda = @"</u>"; sOnset = @"<s>"; sCoda = @"</s>"; bOnset = @"<b>"; bCoda = @"</b>"; iOnset = @"<i>"; iCoda = @"</i>";
            cOnset = "<code>"; cCoda = @"</code>"; jOnset = jCoda = "";

            if (RemoveFormatting)
                uOnset = uCoda = sOnset = sCoda = cOnset = cCoda = iOnset = iCoda = bOnset = bCoda = jOnset = jCoda = "";

            List<Text> Strings = [];
            Strings.Add(new Text {
                Pattern = @"(_){2}(?<Body>.+)(_){2}",
                Onset = uOnset,
                Coda = uCoda,
                Replacement = @"${Body}"
            });
            Strings.Add(new Text {
                Pattern = @"(_)(?<Body>.+)(_)",
                Onset = uOnset,
                Coda = uCoda,
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
}