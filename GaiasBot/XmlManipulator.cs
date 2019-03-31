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
    static class XmlManipulator //this one was to replace all the manipulations with XML in CustomCommands, DropSheet and UserStats but something went wrong (my laziness, probably)
    {
        //public static XmlReader xmlReader;
        public static XmlDocument XmlDoc = new XmlDocument();

        static XmlManipulator()
        {
            if (XmlDoc == null) XmlDoc.Load("Command.xml");
        }

        /// <summary>
        /// Adds to the specified xml file a string that represents a command for discord chat.
        /// </summary>
        /// <param name="_alias">Name of the command.</param>
        /// <param name="_innerText">The text that the command gives back to the chat.</param>
        /// <param name="args">Any args that can be taken by the command.</param>
        public static void AddToXmlFile(string _alias, string _innerText, string _path = @"Commands.xml", params string[] args)
        {
            if (!File.Exists(_path))
            {
                CreateXmlFile();
            }
            XmlDoc.Load(_path);

            bool check = CheckForDuplicates(_alias);
            XmlElement tempEle = XmlDoc.CreateElement("command");
            tempEle.SetAttribute("alias", _alias);

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    tempEle.SetAttribute("param" + i.ToString(), args[i]); //Dunno why I have made params in this method. W/e.
                }
            }

            tempEle.InnerText = _innerText;
            XmlDoc.DocumentElement.AppendChild(tempEle);

            if (check == false)
            {
                XmlDoc.Save(_path);
            }
        }

        /// <summary>
        /// Creates an xml file in the specified path.
        /// </summary>
        /// <param name="_path">The path to the xml file. Default is current folder.</param>
        public static void CreateXmlFile(string _path = @"Commands.xml")
        {
            if (!File.Exists(_path))
            {
                XmlDoc = new XmlDocument();
                XmlElement temp = XmlDoc.CreateElement("root");
                temp.SetAttribute("channel", "gaias");
                temp.SetAttribute("timeOfCreation", DateTime.Now.ToString());
                XmlDoc.AppendChild(temp);
                XmlDoc.Save(_path);
            }
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

        public static bool CheckForDuplicates(string _alias, string _path = "Commands.xml") //checks for already existing elements with specified attributes.
        {
            bool checker = false; //represents the availability of the _alias attribute in chosen xml file.

            XmlNodeList nodeList = XmlDoc.SelectNodes("/root/command/@alias");

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

        public static string GetAnswer(string _alias, string _path = "Commands.xml")
        {
            if (XmlDoc == null) XmlDoc.Load(_path);

            XmlNode node = XmlDoc.SelectSingleNode("//command[@alias='" + _alias + "']");
            string answer = string.Empty;

            if (node != null)
            {
                answer = node.InnerText;
            }
            return answer;
        }

        public static string[] SplitStrings(string input)
        {
            string[] arr = new string[2];
            int index = input.IndexOf(' ') + 1;
            arr[0] = input.Substring(index, input.IndexOf(' ', index + 1) - index);
            arr[1] = input.Substring(input.IndexOf(' ', index + 1) + 1);
            return arr;
        }
    }
}
