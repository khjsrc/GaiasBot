using System;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GaiasBot
{
    static class DropSheet
    {
        private static XmlDocument dropSheet = new XmlDocument();
        private static readonly string FileName = @"droplistFull.xml";

        static DropSheet()
        {
            dropSheet.Load(FileName);
        }

        public static async Task<IEnumerable<XElement>> GetItemsByTypeAsync(string type)
        {
            return await Task<IEnumerable<XElement>>.Run(() =>
            {
                XDocument droplist = XDocument.Load(FileName);
                var list = from itemEle in droplist.Root.Descendants("item")
                           where itemEle.Element("type").Value.ToLower().Contains(type.ToLower()) || itemEle.Element("secondaryType").Value.ToLower().Contains(type.ToLower())
                           orderby (int)itemEle.Element("level"), itemEle.Element("secondaryType").Value
                           select itemEle;

                return list;
            });
        }

        public static async Task<IEnumerable<XElement>> GetItemsByLevelAsync(int min = 0, int max = 50)
        {
            return await Task<IEnumerable<XElement>>.Run(() =>
            {
                XDocument droplist = XDocument.Load(FileName);
                var list = from itemEle in droplist.Root.Descendants("item")
                           where Convert.ToInt32(itemEle.Element("level").Value) >= min && Convert.ToInt32(itemEle.Element("level").Value) <= max
                           orderby (int)itemEle.Element("level"), itemEle.Element("secondaryType").Value
                           select itemEle;
                return list;
            });
        }

        public static async Task<IEnumerable<XElement>> GetItemsByTypeAndLevelAsync(string type, int min = 0, int max = 50)
        {
            return await Task<IEnumerable<XElement>>.Run(async () =>
            {
                var itemsByLvl = await GetItemsByLevelAsync(min, max);
                var itemsByType = await GetItemsByTypeAsync(type);
                var intersection = itemsByLvl.Cast<XNode>().Intersect(itemsByType.Cast<XNode>(), new XNodeEqualityComparer()); //This is how intersection of two IEnumerable<XElement> works. Yes, the casting to XNode is important, the XElements can't be compared when XNodes can.
                return intersection.Cast<XElement>();
            });
        }

        public static async Task<IEnumerable<XElement>> GetUndefinedItemsAsync()
        {
            return await Task<IEnumerable<XElement>>.Run(() =>
            {
                XDocument droplist = XDocument.Load(FileName);
                return from itemEle in droplist.Descendants("item")
                       where ((int)itemEle.Element("level") == 0 || itemEle.Element("type").Value.StartsWith("?") || itemEle.Element("rarity").Value.StartsWith("?")) && itemEle.Element("type").Value != "material"
                       select itemEle;
            });
        }

        public static async Task<XElement> GetItemByNameAsync(string name)
        {
            return await Task<XElement>.Run(() =>
            {
                XDocument droplist = XDocument.Load(FileName);
                return (from itemEle in droplist.Descendants("item")
                        where itemEle.Element("name").Value.ToLower().Contains(name.ToLower())
                        select itemEle).FirstOrDefault();
            });
        }

        public static async Task<IEnumerable<XElement>> GetItemsByMob(string mobName)
        {
            return await Task<IEnumerable<XElement>>.Run(() =>
            {
                XDocument droplist = XDocument.Load(FileName);
                return from itemEle in droplist.Descendants("item")
                       where itemEle.Element("source").Value.ToLower().Contains(mobName.ToLower())
                       orderby (int)itemEle.Element("level"), itemEle.Element("secondaryType").Value
                       select itemEle;
            });
        }

        public static async Task<IEnumerable<string>> GetMatchingMobs(string mobName)
        {
            return await Task<IEnumerable<string>>.Run(() =>
            {
                List<string> result= new List<string>();
                XDocument droplist = XDocument.Load(FileName);
                var itemElements = from itemEle in droplist.Descendants("item")
                    where itemEle.Element("source").Value.ToLower().Contains(mobName.ToLower())
                    select itemEle;

                foreach (XElement item in itemElements)
                {
                    string[] mobs = item.Element("source").Value.Split(',', ':', '\n');
                    foreach (string mob in mobs)
                    {
                        if(result.Contains(mob.TrimStart(' '))) break;
                        if (mob.ToLower().Contains(mobName.ToLower()))
                        {
                            string mobTrimmed = mob.TrimStart(' ');
                            result.Add(mobTrimmed);
                            //break;
                        }
                    }
                }

                return result;
            });
        }
    }
}
