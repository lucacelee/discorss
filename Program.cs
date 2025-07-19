using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Http.Headers;
using System.Net.Quic;
using System.Security.Authentication.ExtendedProtection;
using DSharpPlus;
using Tommy;
class Program {
    static async Task Main(string[] args) {
        // Console.WriteLine("Hello, World!");
        string stringID;
        string token;
        string configdir;
        string message;

        if (args.Length == 0) {
            Console.WriteLine("No command line arguments passed, looking for config.toml in the default directory.");
            configdir = "config.toml";
        } else if (args.Length == 1) {
            if (args[0] == "--help" || args[0] == "-h") {
                Console.WriteLine("discorss v0.1 - a programm to sync your Discord channel with an RSS feed\nCommand line arguments: [config.toml location]");
                return;
            } else {
                configdir = args[0];
                Console.WriteLine(args[0]);
            }
        } else {
            Console.WriteLine("Too many arguments! Looking for config.toml in the default directory.");
            configdir = "config.toml";
        }

        TomlTable table;
        try {
            using StreamReader reader = File.OpenText(configdir);
            try {
                table = TOML.Parse(reader);
            }
            catch (TomlParseException ex) {
                table = ex.ParsedTable;
                Console.WriteLine($"Unable to read the config file properly. Attempting to parse anyway.\nThe following exception occurred: {ex}");

                foreach(TomlSyntaxException syntaxEx in ex.SyntaxErrors)
                    Console.WriteLine($"Error on {syntaxEx.Column}:{syntaxEx.Line}: {syntaxEx.Message}");
            }
            token = table["Discord"]["token"];
            stringID = table["Discord"]["channel"];
        } catch {
            Console.WriteLine("Error: unable to find the config file.");
            return;
        }

        ulong ChannelID = (ulong)Decimal.Parse(stringID);

        DiscordClientBuilder builder = DiscordClientBuilder.CreateDefault(token, DiscordIntents.AllUnprivileged | DiscordIntents.MessageContents);
        builder.ConfigureEventHandlers(
            b => b.HandleMessageCreated(async (s, e) => {
                if (e.Channel.Id == ChannelID) 
                Console.WriteLine($"Message received: «{message = e.Message.Content}»");
            })
        );

        await builder.ConnectAsync();
        await Task.Delay(-1);
    }
}