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

#region
/* TODO:
 * 1. Merge GenerateDroplist and GenerateMobCard methods
 * 2. Add threads (or more tasks) to all this shit and create a new one every time the bot reacts to a message and remove all the reactions from the messages after 60 seconds.
 *   2.1. Pass the message ID to a method for a thread and then remove all the reactions from the message? Or pass the whole message object? Probably, just message ID is better.
 */
#endregion

namespace GaiasBot
{
    static class Bot
    {
        internal static DiscordSocketClient _client;

        internal static readonly string ID = ConfigurationManager.AppSettings.Get("botID");
        internal static readonly string Token = ConfigurationManager.AppSettings.Get("token");
        private static readonly string[] rolesHierarchy = ConfigurationManager.AppSettings.Get("rolesHierarchy").Split(',');

        static Bot()
        {
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

        internal static async Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var msg = await arg2.GetMessageAsync(arg1.Id);
            if (arg2.Name == "banana-beta" && msg.Author.IsBot && arg3.Emote.Name == "\u2B07" && !arg3.User.Value.IsBot)
            {
                await (msg as IUserMessage).RemoveAllReactionsAsync();
                //send next paginated message
                var botMessage = await arg2.SendMessageAsync(embed: EmbedPaginator.GetNext());
                if (EmbedPaginator.Operational)
                    await botMessage.AddReactionAsync(new Emoji("\u2B07")).ContinueWith(async (antecedent) =>
                    {
                        await Task.Delay(30000);
                        await botMessage.RemoveAllReactionsAsync();
                        EmbedPaginator.Reset();
                    });
            }
            else
            {
                return;
            }
        }

        internal static async Task OnUserLeft(SocketGuildUser user)
        {
            await UserStats.SetUserExperienceAsync(user, 0);
        }

        internal static async Task OnMessageReceived(SocketMessage message)
        {
            #region testing part
            if (message.Content.StartsWith("!"))
            {
                string request = message.Content.TrimEnd(' ').Substring(1).Split()[0].ToLower();
                switch (request)
                {
                    case ("say"):
                        {
                            var botMsg = await message.Channel.SendMessageAsync(message.Content.Substring(5));
                        }
                        break;
                    case ("role"):
                        {
                            SocketGuildUser user = message.Author as SocketGuildUser;
                            int userLevel = await UserStats.CountLevelAsync(user);
                            if (message.Content.Split()[1].ToLower() == "reset")
                            {
                                var roleToRemove = user.Roles.FirstOrDefault(x =>
                                {
                                    bool checker = false;
                                    foreach (string r in rolesHierarchy)
                                    {
                                        if (x.Name.ToLower() == r.ToLower()) checker = true;
                                    }
                                    return checker;
                                });
                                if (roleToRemove != null) await user.RemoveRoleAsync(roleToRemove);
                                if (userLevel <= rolesHierarchy.Length)
                                    await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == rolesHierarchy[userLevel]));
                                else
                                    await user.AddRoleAsync(user.Guild.Roles.FirstOrDefault(x => x.Name.ToLower() == rolesHierarchy[rolesHierarchy.Length - 1]));
                            }
                            else
                            {
                                string requestedRole = message.Content.ToLower().Substring(6);
                                string matchingRole = Char.IsDigit(requestedRole[0]) ?
                                    rolesHierarchy[Convert.ToInt32(requestedRole)] :
                                    rolesHierarchy.FirstOrDefault(r => r.Contains(requestedRole));

                                if (matchingRole != null)
                                {
                                    if (userLevel >= Array.IndexOf(rolesHierarchy, matchingRole))
                                    {
                                        var roleToRemove = user.Roles.FirstOrDefault(x =>
                                        {
                                            bool checker = false;
                                            foreach (string r in rolesHierarchy)
                                            {
                                                if (x.Name.ToLower() == r.ToLower()) checker = true;
                                            }
                                            return checker;
                                        });
                                        if (roleToRemove != null) await user.RemoveRoleAsync(roleToRemove);
                                        var role = user.Guild.Roles.FirstOrDefault(r => r.Name.ToLower() == matchingRole);
                                        await user.AddRoleAsync(role);
                                    }
                                    else
                                    {
                                        await message.Channel.SendMessageAsync($"You aren't experienced enough to have \"{matchingRole.Normalize()}\" title.");
                                    }
                                }
                            }
                        }
                        break;
                    case ("item"):
                        {
                            if (message.Content.Length == 5)
                            {
                                await message.Channel.SendMessageAsync("Try to specify an item's name.");
                            }
                            else
                            {
                                Embed e = await GenerateItemCard(message);
                                await message.Channel.SendMessageAsync("", false, e);
                            }
                        }
                        break;
                    case ("items"):
                        {
                            Embed e = await GenerateDroplist(message);
                            var botMessage = await message.Channel.SendMessageAsync(embed: e);

                            if (EmbedPaginator.Operational)
                                await botMessage.AddReactionAsync(new Emoji("\u2B07")).ContinueWith(async (antecedent) =>
                                {
                                    await Task.Delay(30000);
                                    await botMessage.RemoveAllReactionsAsync();
                                    EmbedPaginator.Reset();
                                });
                        }
                        break;
                    case ("source"):
                        {
                            if (message.Content.TrimEnd(' ').ToLower() == "!source")
                            {
                                await message.Channel.SendMessageAsync("Try to specify a source name.");
                            }
                            else
                            {
                                var e = await GenerateMobCard(message);
                                var botMessage = await message.Channel.SendMessageAsync(embed: e);

                                if (EmbedPaginator.Operational)
                                    await botMessage.AddReactionAsync(new Emoji("\u2B07")).ContinueWith(async (antecedent) =>
                                    {
                                        await Task.Delay(30000);
                                        await botMessage.RemoveAllReactionsAsync();
                                        EmbedPaginator.Reset();
                                    });
                            }
                        }
                        break;
                    case ("purge"):
                        {
                            if (CheckPermission(message) && message.Content.ToLower().TrimEnd(' ') != "!purge")
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
                        }
                        break;
                    case ("myexp"):
                        {
                            await message.Channel.SendMessageAsync($"Your experience is {await UserStats.GetUserExperienceAsync(message.Author as SocketGuildUser)}");
                        }
                        break;
                    case ("mylvl"):
                        {
                            await message.Channel.SendMessageAsync($"Your level is {await UserStats.CountLevelAsync(message.Author as SocketGuildUser)}");
                        }
                        break;
                    case ("topic"):
                        {
                            if (!String.IsNullOrEmpty((message.Channel as ITextChannel).Topic))
                                await message.Channel.SendMessageAsync($"```{(message.Channel as ITextChannel).Topic}```");
                        }
                        break;
                    case ("getuserslist"):
                        {
                            await RegisterUsersAsync(message);
                        }
                        break;
                    case ("top"):
                        {
                            int temp = 0;
                            if (Int32.TryParse(message.Content.Substring("!top".Length), out temp))
                            {
                                await message.Channel.SendMessageAsync(await GenerateListOfTops(message));
                            }
                        }
                        break;
                    case ("add"):
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
                        break;
                    case ("remove"):
                        {
                            if (CheckPermission(message))
                            {
                                string temp = message.Content.Substring(message.Content.IndexOf(' ') + 1);
                                CustomCommands.RemoveFromXmlFile(temp);
                                await message.Channel.SendMessageAsync("The command has been removed.");
                            }
                        }
                        break;
                    case ("sendhelp"):
                        {
                            Embed e = new EmbedBuilder()
                        .AddField("Info", ConfigurationManager.AppSettings.Get("helpMessage"))
                        .AddField("Simple commands", "```fix\n" + CustomCommands.GenerateCommandsList() + "```", true)
                        .AddField("Advanced commands", "```fix\n!item\n!items\n!source```", true).Build();
                            await message.Channel.SendMessageAsync("", false, e);
                        }
                        break;
                    default:
                        {
                            string answer = CustomCommands.GetAnswer(message.Content.ToLower().Substring(1));
                            await message.Channel.SendMessageAsync(answer);
                        }
                        break;
                }
            }
            else
            {
                //count exp here?
                if (message.Content.Length > 10 && !message.Author.IsBot) { await UserStats.UpdateExperienceAsync(message); }
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
            Func<SocketGuildUser, bool> checkRoles = (user) =>
            {
                bool checker = false;
                foreach (var role in user.Roles)
                {
                    if (role.Name.ToLower() == "admin" || role.Name.ToLower() == "moderator" || role.Name.ToLower() == "developer" || user.Id == 216299219865042944)
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
            await UserStats.CreateXmlFileAsync(msg.Author as SocketGuildUser);

            foreach (SocketGuildUser user in (msg.Author as SocketGuildUser).Guild.Users)
            {
                await UserStats.AddUserAsync(user);
            }
        }

        public static async Task OnLevelChanged(SocketGuildUser user)
        {
            var userRoles = user.Roles;
            var guildRoles = user.Guild.Roles;

            int experience = await UserStats.GetUserExperienceAsync(user);
            int level = UserStats.CountLevel(experience);
            //if the user's roles don't contain the highest possible role, don't promote the user
            if (level < rolesHierarchy.Length && userRoles.FirstOrDefault(r => r.Name.ToLower() == rolesHierarchy[level]) != null) //actual solution
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
            }
            catch (IndexOutOfRangeException)
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

            if (itemNames.Length >= 1024)
            {
                EmbedPaginator.Process(items.ToList());
                return EmbedPaginator.GetNext();
                /*return eb.AddField("Too big", $"The list contains more characters than Discord allows (1024 is the cap).\n" +
                    $"There are {itemNames.Length} characters in the list. Try a smaller level range to get a shorter list.\n" +
                    $"```!items <type> <minLvl-maxLvl>```" +
                    $"The list contains {items.Count()} items btw.").Build();*/
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

            eb.AddField("Level", itemLevels, true);
            eb.AddField("Type", itemType, true);
            eb.AddField("Item", itemNames, true);

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
            if (itemNames.Length >= 1024)
            {
                EmbedPaginator.Process(items.ToList());
                return EmbedPaginator.GetNext();
            }
            eb.AddField("Level", itemLevels, true);
            eb.AddField("Type", itemType, true);
            eb.AddField("Name", itemNames, true);
            return eb.Build();
        }
    }
}