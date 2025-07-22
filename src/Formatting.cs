using System.Net.NetworkInformation;
using System.Reflection.Emit;
using DSharpPlus;
using RSS;

namespace Formatting {
    public class Discord {
        public static async Task<Item> ParseMessage(DSharpPlus.Entities.DiscordMessage M, List<string> roles, List<string> roles_replace, string title_default) {
            var attachements = (M.Attachments.Count > 0); var time = M.Timestamp; title_default = title_default.Trim() + " ";
            var message = RemoveRoles(M.Content, roles, roles_replace);
            // Console.WriteLine($"The message without roles:\n '{message}'");
            Item Message = new() {
                Title = await SetTitle(message, attachements, time, roles, roles_replace, title_default),
                Description = await SetDescription(message, attachements),
                Author = M.Author!.Username
            };
            Console.WriteLine($"The result of parsing the item.\nTitle: {Message.Title}, author: {Message.Author}, Description:\n'{Message.Description}'");
            return Message;
        }

        private static async Task<string> SetDescription(string message, bool attachements) {
            Console.WriteLine("Setting the description of the message.");
            if (message.Length < 100 && attachements && !message.Contains('\n'))
                return "";
            string[] lines = message.Split('\n');       // Removing all of the possible leading hashtags from the Discord message
            for (int i = 0; i < lines.Length; i++) {
                lines[i] = (lines[i].StartsWith('#')) ? SplitOnWhitespace(lines[i], 2, 1, ' ') : lines[i];  // Couldn't use a foreach loop neatly
            } message = String.Join("\n", lines);
            message = FormatLikeXML(message, false);
            return message;
        }

        private static async Task<string> SetTitle(string message, bool attachements, DateTimeOffset time_offset, List<string> roles, List<string> roles_replace, string title_default) {
            Console.WriteLine("Setting the title of the item.");
            var CleanMessage = FormatLikeXML(RemoveRoles(message, roles, roles_replace), true);
            var FirstLine = SplitOnWhitespace(CleanMessage, 2, 0, '\n');
            if (message.Length < 100 && attachements && !message.Contains('\n'))
                return CleanMessage;
            else if (message.Length < 150 && message.Contains('\n'))
                return FirstLine;
            else if (message.StartsWith('#'))
                return SplitOnWhitespace(FirstLine, 2, 1, ' ');
            else if (FirstLine.Length < 150)
                return FirstLine;
            else return String.Concat(title_default, time_offset.DateTime.ToUniversalTime());
        }

        private static string RemoveRoles(string message, List<string> roles, List<string> roles_replace) {
            Console.WriteLine("Removing Discord roles from the string.");
            foreach (string role in roles) {
                if (message.StartsWith(role, StringComparison.Ordinal)){
                    Console.Write($"Message begins with '{role}', trimming: ");
                    message = message.Remove(0, role.Length);
                    message = message.TrimStart();
                    Console.WriteLine(message);
                };
                message = message.Replace(role, roles_replace[roles.IndexOf(role)]);
            }
            return message;
        }

        private static string SplitOnWhitespace (string message, int parts, int part, char space) {
            Console.WriteLine($"Splitting the string! Parts: {parts}, chosing №{part}.");
            return message.Split(space, parts, StringSplitOptions.TrimEntries)[part];
        }

        private static string FormatLikeXML (string message, bool RemoveFormatting) {
            string uOnset, uCoda, sOnset, sCoda, cOnset, cCoda, iOnset, iCoda, bOnset, bCoda, itOnset, itCoda;
            uOnset = "<u>"; uCoda = "</u>"; sOnset = "<s>"; sCoda = "</s>"; bOnset = "<b>"; bCoda = "</b>"; itOnset = "<i>"; itCoda = "</i>";
            cOnset = cCoda = iOnset = iCoda = "";

            if (RemoveFormatting) 
                uOnset = uCoda = sOnset = sCoda = cOnset = cCoda = iOnset = iCoda = bOnset = bCoda = itOnset = itCoda = "";
            
            message += " ";
            int AsteriskCount = 0;                              // Converting Discord's formatting into XML/HTML formatting (e.g. **hey** ➜ <b>hey</b>)
            bool StrikeThrough, Code, Invisible, Underlined;    // also removing the 'spoiler' and 'codeblock' formatting altogether
            StrikeThrough = Code = Invisible = Underlined = false;

            int Tilde, Grave, Bar, Line;
            Tilde = Grave = Bar = Line = 0;

            bool bold, italic; bold = italic = false;
            for (int j = 0; j < message.Length; j++) {
                switch (message[j]) {
                    case '*':
                        Console.WriteLine($"Found an asterisk! Current count: {AsteriskCount}");
                        AsteriskCount++;
                        break;
                    case '~':
                        Tilde++;
                        break;
                    case '_':
                        Line++;
                        break;
                    case '`':
                        Grave++;
                        break;
                    case '|':
                        Bar++;
                        break;
                    default:
                        (message, Tilde, StrikeThrough) = DecipherSymbols(Tilde, message, StrikeThrough, sOnset, sCoda, 2, j);
                        (message, Line, Underlined) = DecipherSymbols(Line, message, Underlined, uOnset, uCoda, 2, j);
                        (message, Grave, Code) = DecipherSymbols(Grave, message, Code, cOnset, cCoda, 1, j);
                        (message, Bar, Invisible) = DecipherSymbols(Bar, message, Invisible, iOnset, iCoda, 2, j);

                        (message, bold, italic) = DecypherAsterisks(message, j, AsteriskCount, bold, italic, bOnset, bCoda, itOnset, itCoda);
                        AsteriskCount = 0;
                        
                        break;
                }
            } return message.TrimEnd();
        }

        private static (string, int, bool) DecipherSymbols (int Symbols, string Message, bool Styled, string Onset, string Coda, int Count, int Index) {
            if (Symbols == Count){
                Symbols = 0;
                if (Styled) {
                    Styled = false;
                    Message = Message.Remove(Index-Count, Count);
                    Message = Message.Insert(Index-Count, Coda);
                } else {
                    Styled = true;
                    Message = Message.Remove(Index-Count, Count);
                    Message = Message.Insert(Index-Count, Onset);
                }
            } else if (Symbols == 1)
                Symbols = 0;
            return (Message, Symbols, Styled);
        }

        private static (string, bool, bool) DecypherAsterisks (string A, int index, int count, bool bold, bool italic, string bOnset, string bCoda, string iOnset, string iCoda) {
            if (count > 0) {
                int position = index - count;
                Console.Write($"Decyphering asterisks from the discord message!\nAsterisk found at character №{position}, where {index} is the last char out of {count}.");
                A = A.Remove(position, count);
                switch (count) {
                    case 1:
                        if (italic) {
                            A = A.Insert(position, iCoda);
                            italic = false;
                        } else {
                            A = A.Insert(position, iOnset);
                            italic = true;
                        } break;
                    case 2:
                        if (bold) {
                            A = A.Insert(position, bCoda);
                            bold = false;
                        } else {
                            A = A.Insert(position, bOnset);
                            bold = true; 
                        } break;
                    case 3:
                        switch (bold, italic){
                            case (true, true):
                                A = A.Insert(position, (iCoda + bCoda));    // ***Hey*** ➜ <b><i>hey</i></b>
                                bold = italic = false;
                                break;
                            case (true, false):
                                A = A.Insert(position, (bCoda + iOnset));     // **Wow***interesting* ➜ <b>Wow</b><i>interesting</i>
                                bold = true; italic = false;
                                break;
                            case (false, true):
                                A = A.Insert(position, (iCoda + bOnset));     // *Woah***incredible** ➜ <i>Woah</i><b>incredible</b>
                                bold = false; italic = true;
                                break;
                            case (false, false):
                                A = A.Insert(position, (bOnset + iOnset));      // **Hey*** ➜ <b><i>hey</i></b>
                                bold = italic = true;
                                break;                                  // I know that some of those don't work in Discord, but you never know
                        } break;                                        // maybe someone thought they would, and now you have a message like that
                } 
            } return (A, bold, italic);
        }
    }
}