using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Xml;
using System.Xml.Linq;

namespace GaiasBot
{
    public static class EmbedPaginator
    {
        private static List<XElement> items;

        public static bool Operational { get; private set; }

        private static int counter;

        private static int itemsPerMessage = 30;

        public static void Process(List<XElement> list)
        {
            items = list;
            Operational = true;
        }

        public static Embed GetNext()
        {
            EmbedBuilder eb = new EmbedBuilder();
            string levels = string.Empty;
            string types = string.Empty;
            string names = string.Empty;

            for (int i = 0; i < itemsPerMessage; i++)
            {
                if (itemsPerMessage * counter + i < items.Count)
                {
                    levels += items[itemsPerMessage * counter + i].Element("level").Value + "\n";
                    types += items[itemsPerMessage * counter + i].Element("secondaryType").Value + "\n";
                    names += items[itemsPerMessage * counter + i].Element("name").Value + "\n";
                }
                else
                {
                    Reset();
                    break;
                }
            }
            levels = levels.TrimEnd('\n');
            types = types.TrimEnd('\n');
            names = names.TrimEnd('\n');

            eb.AddField("Level", levels, true);
            eb.AddField("Type", types, true);
            eb.AddField("Item", names, true);

            counter++;
            return eb.Build();
        }

        internal static void Reset()
        {
            Operational = false;
            items = null;
            counter = 0;
        }
    }
}
