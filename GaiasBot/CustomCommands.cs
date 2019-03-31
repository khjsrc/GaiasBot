using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace GaiasBot
{
    static class CustomCommands
    {
        //public static XmlReader xmlReader;
        public static XmlDocument CommandsList = new XmlDocument();

        static CustomCommands() //Static constructors are being called once when the code uses its class. The ctor is redundant here.
        {
            if (File.Exists("Commands.xml"))
            {
                CommandsList.Load("Commands.xml");
            }
        }

        /// <summary>
        /// Creates an xml file in the specified path.
        /// </summary>
        /// <param name="_path">The path to the xml file. Default is current folder.</param>
        public static void CreateXmlFile(string _path, string _rootAttName, string _rootAttValue)
        {
            if (!File.Exists(_path))
            {
                CommandsList = new XmlDocument();
                XmlElement temp = CommandsList.CreateElement("root");
                temp.SetAttribute(_rootAttName, _rootAttValue);
                temp.SetAttribute("timeOfCreation", DateTime.Now.ToString());
                CommandsList.AppendChild(temp);
                CommandsList.Save(_path);
            }
        }

        /// <summary>
        /// Adds to the specified xml file a string that represents a command for discord chat.
        /// </summary>
        /// <param name="_alias">Name of the command.</param>
        /// <param name="_innerText">The text that the command gives back to the chat.</param>
        /// <param name="args">Any args that can be taken by the command.</param>
        public static void AddToXmlFile(string _alias, string _innerText, string _path = @"Commands.xml", params string[] args)
        {
            //if (CommandsList == null) CommandsList.Load(_path);

            bool check = CheckForDuplicates(_alias);
            XmlElement tempEle = CommandsList.CreateElement("command");
            tempEle.SetAttribute("alias", _alias);

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    tempEle.SetAttribute("param" + i.ToString(), args[i]); //Dunno why I have made params in this method. W/e.
                }
            }

            tempEle.InnerText = _innerText;
            CommandsList.DocumentElement.AppendChild(tempEle);

            if (check == false)
            {
                CommandsList.Save(_path);
            }
        }

        /// <summary>
        /// Removes an element from the specified xml file.
        /// </summary>
        /// <param name="_alias"></param>
        /// <param name="_innerText"></param>
        /// <param name="_path"></param>
        /// <param name="args"></param>
        public static void RemoveFromXmlFile(string _alias, string _path = @"Commands.xml")
        {
            CommandsList.Load(_path);
            XmlNode node = CommandsList.SelectSingleNode($"//command[@alias='{_alias }']");
            node.ParentNode.RemoveChild(node);
            //XmlElement tempEle = XmlDoc.CreateElement("command");
            //tempEle.SetAttribute("alias", _alias);
            //XmlDoc.DocumentElement.AppendChild(tempEle);
            CommandsList.Save(_path);
            Console.WriteLine($"A node with value = \"{_alias}\" have been removed from the file.");
        }

        //public static bool CheckForDuplicates(string _alias, string _path = "Commands.xml")
        //{
        //    bool checker = false;
        //    XElement root = XElement.Load(_path);
        //    IEnumerable<XElement> els =
        //        from el in root.Elements("command")
        //        where (string)el.Attribute("alias") == _alias
        //        select el;
        //    foreach (XElement el in els)
        //    {
        //        foreach (XAttribute att in el.Attributes())
        //        {
        //            if (att.ToString() == _alias)
        //            {
        //                checker = true;
        //            }
        //        }
        //    }
        //    return checker;
        //}

        /// <summary>
        /// Checks for duplicates in the commands.xml file.
        /// </summary>
        /// <param name="_alias"></param>
        /// <param name="_path"></param>
        /// <returns></returns>
        public static bool CheckForDuplicates(string _alias, string _path = @"Commands.xml") //checks for already existing elements with specified attributes.
        {
            bool checker = false; //represents the availability of the _alias attribute in chosen xml file.

            XmlNodeList nodeList = CommandsList.SelectNodes("/root/command/@alias");

            if (nodeList != null)
            {
                foreach (XmlNode n in nodeList)
                {
                    if (n.Value.ToLower() == _alias.ToLower())
                    {
                        checker = true;
                    }
                }
            }
            return checker;
        }

        /// <summary>
        /// Gets an answer for the command taken from the chat.
        /// </summary>
        /// <param name="_alias"></param>
        /// <param name="_path"></param>
        /// <returns></returns>
        public static string GetAnswer(string _alias, string _path = @"Commands.xml")
        {
            if (CommandsList == null) CommandsList.Load(_path);

            XmlNode node = CommandsList.SelectSingleNode($"//command[@alias='{_alias}']");
            string answer = string.Empty;

            if (node != null)
            {
                answer = node.InnerText;
            }
            return answer;
        }

        /// <summary>
        /// Splits the input string to add the command to the xml file.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static IEnumerable<string> SplitStrings(string input)
        {
            List<string> temp = new List<string>();
            temp.AddRange(input.Split(' ', ',', '.', '-'));
            return temp;
        }

        /// <summary>
        /// Returns the full list of existing commands from the specified .xml file.
        /// </summary>
        /// <returns>Full list of existing commands.</returns>
        public static string GenerateCommandsList() //Rework it with LINQ?
        {
            List<string> temp = new List<string>();
            XmlNodeList nodeList = CommandsList.SelectNodes("/root/command/@alias"); //chooses only alias' attribute values
            string output = "```fix\nCommands list: ";

            foreach (XmlNode node in nodeList)
            {
                temp.Add(node.Value);
            }
            temp.Sort();
            foreach(string s in temp)
            {
                output += "\n!" + s;
            }
            output += "```";
            return output;
        }
    }
}
