using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Discord;
using Discord.WebSocket;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace GaiasBot
{
    static class Bot
    {
        internal static DiscordSocketClient _client;

        internal static readonly string ID; 
        internal static readonly string Token;

        private static readonly string[] rolesHierarchy = ConfigurationManager.AppSettings.Get("rolesHierarchy").Split(',');

        static Bot()
        {
            ID = ConfigurationManager.AppSettings.Get("botID");
            Token = ConfigurationManager.AppSettings.Get("token");
            UserStats.LevelChanged += OnLevelChanged;
        }

        internal static async Task OnUserJoined(SocketGuildUser user)
        {
            await (await user.GetOrCreateDMChannelAsync()).SendMessageAsync(ConfigurationManager.AppSettings.Get("greetingMessage"));
            await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "newbie"));

            if (!File.Exists(UserStats.FileName))
            {
                UserStats.CreateXmlFile(user);
            }

            UserStats.AddUser(user);

            //TODO (done)
            //1. Greeting message. In DM.
            //2. Add user info to the xml file. (ID, username, set exp to 0)
            //3. Add a role named "newbie" ?
        }

        internal static Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            throw new NotImplementedException();
        }

        internal static async Task OnUserLeft(SocketGuildUser user)
        {
            await UserStats.SetUserExperienceAsync(user, 0);
        }

        internal static async Task OnMessageReceived(SocketMessage message)
        {
            #region testing part

            #endregion

            if (message.Content.Split()[0] == "!item")
            {
                if (message.Content.TrimEnd(' ').ToLower() == "!item")
                {
                    await message.Channel.SendMessageAsync("Try to specify an item's name.");
                }
                else
                {
                    var builder = await GenerateItemCard(message);
                    await message.Channel.SendMessageAsync("", false, builder);
                }
            }
            if (message.Content.Split()[0] == "!items")
            {
                var builder = await GenerateDroplist(message);
                await message.Channel.SendMessageAsync("", false, builder);
            }
            if (message.Content.Split()[0] == "!source")
            {
                if (message.Content.TrimEnd(' ').ToLower() == "!source")
                {
                    await message.Channel.SendMessageAsync("Try to specify a source name.");
                }
                else
                {
                    var builder = await GenerateMobCard(message);
                    var botMessage = await message.Channel.SendMessageAsync("", false, builder);

                    //await botMessage.AddReactionAsync(new Emoji("\u2B07"));

                }
            }
            if (message.Content.Split()[0].ToLower() == "!purge" && CheckPermission(message) && message.Content.ToLower().TrimEnd(' ') != "!purge")
            {
                string[] purge = message.Content.Split(' ');
                if (purge[1].Length > 3)
                {
                    var messagesTemp = message.Channel.GetMessagesAsync(Convert.ToUInt64(purge[1]), Direction.After);
                    await messagesTemp.ForEachAsync(async m =>
                    {
                        foreach (IMessage mes in m)
                        {
                            await mes.DeleteAsync();
                        }
                    });
                }
                else
                {
                    var messagesTemp = message.Channel.GetMessagesAsync(Convert.ToInt32(purge[1]));
                    await messagesTemp.ForEachAsync(async m =>
                    {
                        foreach (IMessage mes in m)
                        {
                            await mes.DeleteAsync();
                        }
                    });
                }
            }

            #region experimental
            if (message.Content.StartsWith("!host"))
            {
                await message.Channel.SendMessageAsync("rip ENT :(");
                /*if (message.Content.Split().Length >= 2)
                {
                    _lastHostRequestMessage = message;
                    string region = "atlanta";
                    string usrName = message.Content.Split(' ')[1];
                    if (message.Content.Split(' ').Length == 3 && Regex.IsMatch(message.Content.Split()[2].ToLower(), "[atlanta|ny|la|europe|au|jp|sg]")) region = message.Content.Split()[2].ToLower();
                    var response = await entClient.HostGame(usrName, region);
                    await message.Channel.SendMessageAsync(response.ToMdFormat());
                }
                else
                {
                    await message.Channel.SendMessageAsync("Command syntax: !host <username> [atlanta|ny|la|europe|au|jp|sg]");
                }*/
            }
            #endregion

            #region User exp handler section. Lots of shit are gonna happen here.
            if (message.Content.ToLower() == "!getuserslist") { await RegisterUsersAsync(message); }
            if (message.Content.ToLower().StartsWith("!top"))
            {
                int temp = 0;
                if (Int32.TryParse(message.Content.Substring("!top".Length), out temp))
                {
                    await message.Channel.SendMessageAsync(await GenerateListOfTops(message));
                }
            }

            if (!message.Content.StartsWith("!") && message.Content.Length > 10 && !message.Author.IsBot) { await UserStats.UpdateExperienceAsync(message); }
            #endregion

            //Needs to be reworked. 
            //For instance, make methods to require SocketMessage object and do magic things with the object inside.
            #region Commands handler section.

            if (message.Content.StartsWith("!"))
            {
                if (message.Content.Substring(1).StartsWith("myexp")) await message.Channel.SendMessageAsync($"Your experience is {await UserStats.GetUserExperienceAsync(message.Author as SocketGuildUser)}");
                if (message.Content.Substring(1).StartsWith("mylvl")) await message.Channel.SendMessageAsync($"Your level is {await UserStats.CountLevelAsync(message.Author as SocketGuildUser)}");
                if (message.Content.Substring(1).ToLower().StartsWith("topic"))
                {
                    if (!String.IsNullOrEmpty((message.Channel as ITextChannel).Topic)) await message.Channel.SendMessageAsync($"```{(message.Channel as ITextChannel).Topic}```");
                }

                if (message.Content.ToLower().Substring(1).StartsWith("add"))
                {
                    if (CheckPermission(message))
                    {
                        if (!File.Exists(@"Commands.xml"))
                        {
                            CustomCommands.CreateXmlFile(@"Commands.xml", "GuildName", (message.Author as SocketGuildUser).Guild.Name);
                        }
                        string[] temp = SplitStrings(message.Content);
                        CustomCommands.AddToXmlFile(temp[0], temp[1]);
                        await message.Channel.SendMessageAsync("The command has been added.");
                    }
                }

                if (message.Content.ToLower().Substring(1).StartsWith("remove"))
                {
                    if (CheckPermission(message))
                    {
                        string temp = message.Content.Substring(message.Content.IndexOf(' ') + 1);
                        CustomCommands.RemoveFromXmlFile(temp);
                        await message.Channel.SendMessageAsync("The command has been removed.");
                    }
                }

                if (message.Content.ToLower().Substring(1).ToLower().StartsWith("sendhelp")) //embedded message?
                {
                    var tempEBuilder = new EmbedBuilder()
                        .AddField("Info", ConfigurationManager.AppSettings.Get("helpMessage"))
                        .AddField("Simple commands", "```fix\n" + CustomCommands.GenerateCommandsList() + "```", true)
                        .AddField("Advanced commands", "```fix\n!item\n!items\n!source```", true).Build();
                    await message.Channel.SendMessageAsync("", false, tempEBuilder);
                }
                else
                {
                    string answer = CustomCommands.GetAnswer(message.Content.ToLower().Substring(1));
                    await message.Channel.SendMessageAsync(answer);
                }
            }
            #endregion
        }

        internal static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        internal static bool CheckPermission(SocketMessage msg)
        {
            //I don't know why, but still, it's a lambda expression.
            Func<SocketGuildUser, bool> checkRoles = (user) =>
            {
                bool checker = false;
                foreach (var role in user.Roles)
                {
                    if (role.Name.ToLower() == "admin" || role.Name.ToLower() == "it helper" || role.Name.ToLower() == "manager" || user.Id == 216299219865042944)
                    {
                        checker = true;
                    }
                }
                return checker;
            };

            //And this is LINQ. Dunno which one is better.
            //var matchingRoles = from r in userRoles 
            //                    where r.Name.ToLower() == "admin" || r.Name.ToLower() == "it helper" || r.Name.ToLower() == "manager"
            //                    select r;

            //if (matchingRoles.Count() >= 1 || user.Id == 216299219865042944) checker = true;

            return checkRoles(msg.Author as SocketGuildUser);
        }

        internal static async Task Sayonara(SocketMessage msg)
        {
            string SayonaraMessage = "We are sorry to disturb you, but bad times have come to Gaia's Retaliation official discord server (aka MiroBG's personal server) and we had to do something to stop it.\nHowever, we have created a new one, with democracy, freedom, cookies and other cool stuff (in future). Sincerely, your **Rebel Team**. \n\nFeel free to join the new community! You're always welcome there: https://discord.gg/89GMjzU";

            var msgSender = msg.Author as SocketGuildUser;
            var guildUsers = msgSender.Guild.Users;

            foreach (var user in guildUsers)
            {
                if (user.Username.ToLower() != "banana-bot")
                {
                    if (user.Roles.Count > 0)
                    {
                        bool roleChecker = true;
                        foreach (var role in user.Roles)
                        {
                            if (role.Name.ToLower() == "admin" || role.Name.ToLower() == "manager" || role.Name.ToLower().StartsWith("shumen")) roleChecker = false;
                        }
                        if (roleChecker)
                        {
                            var DMChannel = await user.GetOrCreateDMChannelAsync();

                            await DMChannel.SendMessageAsync(SayonaraMessage);
                            await user.KickAsync("Congrats, Miro, you now have your individual server with your beloved creation called nsfw-channel for people from Shumen.");
                            await msgSender.Guild.AddBanAsync(user.Id, reason: "Kek, have fun unbanning all these people.");
                        }
                    }
                    else
                    {
                        var DM = await user.GetOrCreateDMChannelAsync();
                        await DM.SendMessageAsync(SayonaraMessage);
                        await user.KickAsync(SayonaraMessage);
                        await msgSender.Guild.AddBanAsync(user.Id, reason: "Kek, have fun unbanning all these people.");
                    }
                }
                else
                {
                    continue;
                }
            }
        }

        internal static async Task RegisterUsersAsync(SocketMessage msg)
        {
            //await Task.Run(() => (msg.Author as SocketGuildUser).Guild.DownloadUsersAsync());

            await UserStats.CreateXmlFileAsync(msg.Author as SocketGuildUser);

            foreach (SocketGuildUser user in (msg.Author as SocketGuildUser).Guild.Users)
            {
                await UserStats.AddUserAsync(user);
            }
        }

        public static async Task OnLevelChanged(SocketGuildUser user)
        {
            //var user = msg.Author as SocketGuildUser;
            var userRoles = user.Roles;
            var guildRoles = user.Guild.Roles;

            int experience = await UserStats.GetUserExperienceAsync(user);
            int level = UserStats.CountLevel(experience);

            if (level < rolesHierarchy.Length) //actual solution
            {
                await user.AddRoleAsync(
                    user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == rolesHierarchy[level]));
                await user.RemoveRoleAsync(
                    user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == rolesHierarchy[level - 1]));
            }

            #region shame
            //switch (level)
            //{
            //    case 0:
            //        break;
            //    case 1:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "newbie"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "the crab killer"));
            //        break;
            //    case 2:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "the crab killer"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "sanev's bro"));
            //        break;
            //    case 3:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "sanev's bro"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "doppelganger"));
            //        break;
            //    case 4:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "doppelganger"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "firelord's bane"));
            //        break;
            //    case 5:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "firelord's bane"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "arachnophobe"));
            //        break;
            //    case 6:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "arachnophobe"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "makar? pfff"));
            //        break;
            //    case 7:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "makar? pfff"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "arachnophobe vol. 2"));
            //        break;
            //    default:
            //        await user.RemoveRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "arachnophobe vol. 2"));
            //        await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == "товарищ"));
            //        break;
            //}
            #endregion
        }

        internal static string[] SplitStrings(string input)
        {
            string[] arr = new string[2];
            int index = input.IndexOf(' ') + 1;
            arr[0] = input.Substring(index, input.IndexOf(' ', index + 1) - index);
            arr[1] = input.Substring(input.IndexOf(' ', index + 1) + 1);
            return arr;
        }

        internal static async Task<string> GenerateListOfTops(SocketMessage msg) //ugly af
        {
            return await Task<string>.Run(async () =>
            {
                string output = "```md\n";
                int amount = 1;
                foreach (XContainer member in await UserStats.GetTopAsync(msg))
                {
                    var user = (msg.Author as SocketGuildUser).Guild
                        .GetUser(Convert.ToUInt64(member.Element("ID").Value));
                    string username = String.Empty;
                    if (user != null) { username = user.Username; }
                    else
                    {
                        username = member.Element("Username").Value;
                    }
                    output += "#" + amount + " " + username + '\n';
                    amount++;
                }
                output += "```";
                return output;
            });
        }

        internal static async Task<Embed> GenerateDroplist(SocketMessage message)
        {
            EmbedBuilder eb = new EmbedBuilder
            {
                Color = Color.Gold
            };

            var commandSplitted = message.Content.Split(' ', '-');
            IEnumerable<XElement> items;
            try
            {
                items = await DropSheet.GetItemsByTypeAndLevelAsync(commandSplitted[1], Convert.ToInt32(commandSplitted[2]), Convert.ToInt32(commandSplitted[3]));
                //eb.Title = commandSplitted[1] + " items list within level range: " + commandSplitted[2] + "-" + commandSplitted[3];
            }
            catch (IndexOutOfRangeException) //split the list in a few messages with less than 2k symbols
            {
                try //probably, not the best solution.
                {
                    var rng = new Random();
                    int random1 = rng.Next(0, 51);
                    int random2 = rng.Next(0, 51);
                    if (random1 > random2)
                    {
                        int temp = random2;
                        random2 = random1;
                        random1 = temp;
                    }
                    items = await DropSheet.GetItemsByTypeAndLevelAsync(commandSplitted[1], random1, random2);
                    eb.Title = commandSplitted[1] + " items list within level range: " + random1 + "-" + random2;
                }
                catch (IndexOutOfRangeException)
                {
                    return new EmbedBuilder().AddField("Command syntax", "\n```!items <type> [minLvl-maxLvl]```\nPossible types are: **weapon**, **armor**, **helmet**, **misc**, **accessory**, **mail**, **leather**, **cloth**, **offhand**, **gem**, **totem**, **instrument**, **2handed**, **skull**, **relic**, **book**, **trophy**, **shield**, **chain**, **material**, **artifact**.").Build();
                }
            }

            string itemNames = string.Empty;
            string itemLevels = string.Empty;
            string itemType = string.Empty;

            foreach (var item in items)
            {
                itemNames += item.Element("name").Value + "\n";
                itemLevels += item.Element("level").Value + "\n";
                itemType += item.Element("secondaryType").Value + "\n";
            }

            if (itemNames.Length >= 1023)
            {
                return eb.AddField("Too big", $"The list contains more characters than Discord allows (1024 is the cap).\n" +
                    $"There are {itemNames.Length} characters in the list. Try a smaller level range to get a shorter list.\n" +
                    $"```!items <type> <minLvl-maxLvl>```" +
                    $"The list contains {items.Count()} items btw.").Build();
            }

            #region this doesn't work properly for discord, because it has a limit for messages sent in a short period of time
            /*for (int i = 0; i < items.Count() / 35; i++)
            {
                string itemNames = string.Empty;
                string itemLevels = string.Empty;
                //string itemRarity = string.Empty;
                string itemType = string.Empty;
                var builder = new EmbedBuilder();

                if (items.Count() >= 35 && items.Count() <= 40)
                {
                    foreach (var item in items)
                    {
                        itemNames += item.Element("name").Value + "\n";
                        itemLevels += item.Element("level").Value + "\n";
                        //itemRarity += item.Element("rarity").Value + "\n";
                        itemType += item.Element("secondaryType").Value + "\n";
                    }

                    itemNames = itemNames.TrimEnd('\n');
                    itemLevels = itemLevels.TrimEnd('\n');
                    itemType = itemType.TrimEnd('\n');

                    builder.AddInlineField("Level", itemLevels);
                    //embedBuilder.AddInlineField("Rarity", itemRarity);
                    builder.AddInlineField("Type", itemType);
                    builder.AddInlineField("Item", itemNames);
                }
                else
                {
                    var temp = items.Take(35);
                    items = items.Except(temp);
                    foreach (var item in temp)
                    {
                        itemNames += item.Element("name").Value + "\n";
                        itemLevels += item.Element("level").Value + "\n";
                        //itemRarity += item.Element("rarity").Value + "\n";
                        itemType += item.Element("secondaryType").Value + "\n";
                    }

                    itemNames = itemNames.TrimEnd('\n');
                    itemLevels = itemLevels.TrimEnd('\n');
                    itemType = itemType.TrimEnd('\n');

                    builder.AddInlineField("Level", itemLevels);
                    //embedBuilder.AddInlineField("Rarity", itemRarity);
                    builder.AddInlineField("Type", itemType);
                    builder.AddInlineField("Item", itemNames);
                }
                embedBuilders.Add(builder);
            }*/
            #endregion

            itemNames = itemNames.TrimEnd('\n');
            itemLevels = itemLevels.TrimEnd('\n');
            itemType = itemType.TrimEnd('\n');
            
            eb.AddField("Level", itemLevels, inline:true);
            eb.AddField("Type", itemType, inline:true);
            eb.AddField("Item", itemNames, inline:true);

            return eb.Build();
        }

        internal static async Task<Embed> GenerateItemCard(SocketMessage message)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            XElement item = await DropSheet.GetItemByNameAsync(message.Content.Substring("!item".Length + 1));
            if (item == null)
            {
                return embedBuilder.AddField("Oops", "Looks like there's nothing like the thing you're looking for.").Build();
            }

            if (item.Element("rarity").Value == "Legendary") embedBuilder = embedBuilder.WithColor(255, 85, 82);
            else if (item.Element("rarity").Value == "Rare") embedBuilder = embedBuilder.WithColor(71, 72, 196);
            else if (item.Element("rarity").Value == "Uncommon") embedBuilder.Color = Color.Green;
            else if (item.Element("rarity").Value == "Ethereal") embedBuilder.Color = Color.Orange;
            else embedBuilder.Color = Color.LightGrey;

            embedBuilder.Title = item.Element("name").Value;
            embedBuilder.AddField("Type", item.Element("type").Value + ": " + item.Element("secondaryType").Value, true);
            embedBuilder.AddField("Rarity", item.Element("rarity").Value, true);
            embedBuilder.AddField("Level", item.Element("level").Value, true);
            var stats = from statEle in item.Descendants("stats").Elements()
                        where statEle.Value != "0"
                        select statEle;
            string statForBuilder = string.Empty;
            foreach (var stat in stats)
            {
                statForBuilder += stat.Name + ": " + stat.Value + "\n";
            }
            if (!string.IsNullOrEmpty(statForBuilder))
            {
                embedBuilder.AddField("Stats", statForBuilder, true);
                if (!string.IsNullOrEmpty(item.Element("extra").Value))
                {
                    embedBuilder.AddField("Additional stats", item.Element("extra")?.Value, true);
                }
            }
            embedBuilder.AddField("Source", item.Element("source").Value);
            if (!item.Element("description").Value.ToLower().Contains("no description yet") || item.Element("description").Value == string.Empty)
            {
                embedBuilder.AddField("Description", item.Element("description").Value);
            }

            return embedBuilder.Build();
        }

        internal static async Task<Embed> GenerateMobCard(SocketMessage message)
        {
            EmbedBuilder eb = new EmbedBuilder();
            IEnumerable<XElement> items = await DropSheet.GetItemsByMob(message.Content.Substring("!source".Length + 1));
            var matchingSources = await DropSheet.GetMatchingMobs(message.Content.Substring("!source".Length + 1));
            if (matchingSources.Count() == 0)
            {
                return eb.AddField("Oops", "Looks like there's nothing like the thing you're looking for.").Build();
            }

            string title = "Matching sources: ";
            foreach (string source in matchingSources)
            {
                title += source + ", ";
            }
            title = title.TrimEnd(',', ' ');
            eb.Title = title;
            //eb.AddField("Matching sources", title);
            eb.Color = Color.DarkGreen;

            string itemNames = string.Empty;
            string itemLevels = string.Empty;
            string itemType = string.Empty;
            foreach (var item in items)
            {
                itemNames += item.Element("name").Value + '\n';
                itemLevels += item.Element("level").Value + "\n";
                itemType += item.Element("secondaryType").Value + "\n";
            }

            eb.AddField("Level", itemLevels, true);
            eb.AddField("Type", itemType, true);
            eb.AddField("Name", itemNames, true);
            return eb.Build();
        }
    }
}