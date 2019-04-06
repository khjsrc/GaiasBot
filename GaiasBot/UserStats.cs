using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using Discord;
using Discord.WebSocket;

namespace GaiasBot
{
    static class UserStats
    {
        public static XmlDocument UsersList = new XmlDocument();

        public static event EventHandler ExperienceChanged;
        public static event Func<SocketGuildUser, Task> LevelChanged;

        public static readonly int LevelCap = 250;
        public static readonly float CapRaisePercentage = 0.23f;
        public static readonly string FileName = @"UsersList.xml";

        static UserStats()
        {
            if (File.Exists(FileName))
            {
                UsersList.Load(FileName);
            }
        }

        public static async Task CreateXmlFileAsync(SocketGuildUser user)
        {
            await Task.Run(() => CreateXmlFile(user));
        }

        public static void CreateXmlFile(SocketGuildUser user)
        {
            if (!File.Exists(FileName))
            {
                UsersList = new XmlDocument();
                XmlElement temp = UsersList.CreateElement("root");
                XmlElement guild = UsersList.CreateElement("Guild");
                guild.SetAttribute("GuildID", user.Guild.Id.ToString());
                guild.SetAttribute("JoinedAt", DateTime.Now.ToString());
                temp.AppendChild(guild);
                UsersList.AppendChild(temp);
                UsersList.Save(FileName);
            }
            else
            {
                UsersList.Load(FileName);
                if (UsersList.SelectSingleNode($"/root/Guild[@GuildID = '{user.Guild.Id}']") == null)
                {
                    XmlElement guild = UsersList.CreateElement("Guild");
                    guild.SetAttribute("GuildID", user.Guild.Id.ToString());
                    guild.SetAttribute("JoinedAt", DateTime.Now.ToString());
                    UsersList.DocumentElement?.AppendChild(guild);
                    UsersList.Save(FileName);
                }
            }
        }

        public static async Task AddUserAsync(SocketGuildUser user)
        {
            await Task.Run(() => AddUser(user));
        }

        public static void AddUser(SocketGuildUser user)
        {
            UsersList.Load(FileName);

            if (UsersList.SelectSingleNode($"//Guild[@GuildID = '{user.Guild.Id}']/User[ID/text() = '{user.Id}']") == null)
            {
                XmlElement userElement = UsersList.CreateElement("User");

                XmlElement userName = UsersList.CreateElement("Username");
                userName.InnerText = user.Username;
                userElement.AppendChild(userName);

                XmlElement userId = UsersList.CreateElement("ID");
                userId.InnerText = user.Id.ToString();
                userElement.AppendChild(userId);

                XmlElement userMention = UsersList.CreateElement("Mention");
                userMention.InnerText = user.Mention;
                userElement.AppendChild(userMention);

                XmlElement userCreatedAt = UsersList.CreateElement("CreatedAt");
                userCreatedAt.InnerText = user.CreatedAt.DateTime.ToString();
                userElement.AppendChild(userCreatedAt);

                XmlElement userJoinedAt = UsersList.CreateElement("JoinedAt");
                userJoinedAt.InnerText = (user.JoinedAt == null) ? "n/a" : user.JoinedAt.Value.ToString();
                userElement.AppendChild(userJoinedAt);

                XmlElement userExperience = UsersList.CreateElement("Experience");
                userExperience.InnerText = "0";
                userElement.AppendChild(userExperience);

                UsersList.SelectSingleNode($"//Guild[@GuildID = '{user.Guild.Id}']")?.AppendChild(userElement);
                UsersList.Save(FileName);
            }
        }

        public static async Task UpdateExperienceAsync(SocketMessage msg)
        {
            await Task.Run(() => UpdateExperience(msg));
        }

        public static void UpdateExperience(SocketMessage msg)
        {
            UsersList.Load(FileName);

            SocketGuildUser user = msg.Author as SocketGuildUser;

            XmlNode userExp = UsersList.SelectSingleNode($"/root/Guild[@GuildID = '{user.Guild.Id}']/User[ID/text() = '{user.Id}']/Experience");// /root/User[Username/text() = 'khj']/Experience
            if (userExp != null)
            {
                userExp.ParentNode.RemoveChild(userExp);
                int oldExp = Convert.ToInt32(userExp.InnerText);
                int newExp = 0;//oldExp + ((msg.Content.Length / 10 > 5) ? 5 : msg.Content.Length / 10);

                if (msg.Channel.Name == "looking-for-game") newExp = oldExp + ((msg.Content.Length / 10 > 5) ? 5 : msg.Content.Length / 10);
                else if (msg.Channel.Name == "general") newExp = oldExp + ((msg.Content.Length / 20 > 10) ? 10 : msg.Content.Length / 20);
                else if (msg.Channel.Name == "bug-report") newExp = oldExp + msg.Content.Length / 10;
                else newExp = oldExp;

                userExp.InnerText = newExp.ToString();
                UsersList.SelectSingleNode($"/root/Guild[@GuildID = '{user.Guild.Id}']/User[ID/text() = '{user.Id}']")?.AppendChild(userExp);
                UsersList.Save(FileName);

                if (CountLevel(oldExp) != CountLevel(newExp) && LevelChanged != null) { LevelChanged(user); }
            }
            else { AddUser(user); }
        }

        public static async Task SetUserExperienceAsync(SocketGuildUser user, int amount = 0)
        {
            await Task.Run(() =>
            {
                XmlNode userExp =
                    UsersList.SelectSingleNode(
                        $"/root/Guild[@GuildID = '{user.Guild.Id}']/User[ID/text() = '{user.Id}']/Experience");

                userExp.ParentNode.RemoveChild(userExp);
                userExp.InnerText = "0";
                UsersList.SelectSingleNode($"/root/Guild[@GuildID = '{user.Guild.Id}']/User[ID/text() = '{user.Id}']")?.AppendChild(userExp);
                UsersList.Save(FileName);
            });
        }

        public static async Task<int> GetUserExperienceAsync(SocketGuildUser user)
        {
            return await Task.Run<int>(() => GetUserExperience(user));
        }

        public static int GetUserExperience(SocketGuildUser user)
        {
            UsersList.Load(FileName);

            XmlNode userExp = UsersList.SelectSingleNode($"/root/Guild[@GuildID = '{user.Guild.Id}']/User[ID/text() = '{user.Id}']/Experience");
            int exp = Convert.ToInt32(userExp.InnerText);

            return exp;
        }

        /// <summary>
        /// Returns the level of a user. 0 is for newbie, 1 is for the crab killer and so on.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static async Task<int> CountLevelAsync(SocketGuildUser user)
        {
            int level = 0;
            await Task.Run(async () =>
            {
                int exp = await GetUserExperienceAsync(user);

                while (exp - Convert.ToInt32(Math.Round(LevelCap + LevelCap * level * CapRaisePercentage)) >= 0)
                {
                    exp -= Convert.ToInt32(Math.Round(LevelCap + LevelCap * level * CapRaisePercentage));
                    level++;
                }
            });
            return level;
        }

        public static int CountLevel(int experience)
        {
            int level = 0;
            while (experience - Convert.ToInt32(Math.Round(LevelCap + LevelCap * level * CapRaisePercentage)) >= 0)
            {
                experience -= Convert.ToInt32(Math.Round(LevelCap + LevelCap * level * CapRaisePercentage));
                level++;
            }
            return level;
        }

        internal static async Task<IEnumerable> GetTopAsync(SocketMessage msg)
        {
            return await Task<IEnumerable>.Run(() => 
            {
                int amount = Convert.ToInt32(msg.Content.Substring("!top".Length)) < (msg.Author as SocketGuildUser).Guild.MemberCount ? Convert.ToInt32(msg.Content.Substring("!top".Length)) : 10;

                XDocument xmldoc = XDocument.Load(FileName);
                var list = (from userEle in xmldoc.Root.Descendants("User")
                            where userEle.Parent.Attribute("GuildID").Value == (msg.Author as SocketGuildUser).Guild.Id.ToString()
                            where (int)userEle.Element("Experience") >= 0
                            orderby (int)userEle.Element("Experience")
                            select userEle).Reverse().Take(amount);//.Distinct();

                //List<ulong> output = new List<ulong>();
                //foreach (var item in list)
                //{
                //    output.Add(Convert.ToUInt64(item.Element("ID").Value));
                //}

                //return output;
                return list;
            });
        }
    }
}
