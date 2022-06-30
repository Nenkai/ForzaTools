using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Xml;
using Syroot.BinaryData;

namespace BXML
{
    public class BXML
    {
        public List<string> Strings { get; set; }
        public CXMLNode RootNode { get; set; }
        
        public static BXML ReadFile(string file)
        {
            using var fs = new FileStream(file, FileMode.Open);
            using var bs = new BinaryStream(fs);

            var bxml = new BXML();

            uint magic = bs.ReadUInt32();
            if (magic != 0x4C4D5842)
                throw new InvalidDataException("Not a BXML file. (Magic did not match)");
            
            byte version = bs.Read1Byte();
            if (version > 2)
                throw new InvalidDataException($"BXML Version is above maximum supported ({version} > 2).");

            int stringCount = bs.ReadInt32();
            int stringTableSize = bs.ReadInt32();

            bxml.Strings = new List<string>(stringCount);
            for (var i = 0; i < stringCount; i++)
                bxml.Strings.Add(bs.ReadString(StringCoding.Int16CharCount));

            byte rootFlag = bs.Read1Byte();
            bxml.RootNode = bxml.ReadNode(bs);

            return bxml;
        }

        public void SerializeToTextXML(string outputPath)
        {
            using (XmlWriter writer = XmlWriter.Create(outputPath, new XmlWriterSettings() { Indent = true }))
            {
                WriteNode(writer, RootNode);
            }
        }

        private void WriteNode(XmlWriter writer, CXMLNode node)
        {
            writer.WriteStartElement(node.Name);

            foreach (var attr in node.Attributes)
                writer.WriteAttributeString(attr.Key, attr.Value);

            if (node.ChildNodes != null)
            {
                foreach (var cnode in node.ChildNodes)
                    WriteNode(writer, cnode);
            }

            writer.WriteEndElement();
            writer.Flush();
        }

        public CXMLNode ReadNode(BinaryStream bs)
        {
            var node = new CXMLNode();

            byte nodeFlags = bs.Read1Byte();
            if ((nodeFlags & 0x02) != 0) // attributes
            {
                node.Name = ReadStringIndex(bs);

                byte attrCount = bs.Read1Byte();
                node.Attributes = new List<CXMLAttribute>(attrCount);

                for (var i = 0; i < attrCount; i++)
                {
                    var attr = new CXMLAttribute();
                    attr.Key = ReadStringIndex(bs);
                    attr.Value = ReadStringIndex(bs);
                    node.Attributes.Add(attr);
                }
            }

            if ((nodeFlags & 0x04) != 0) // attributes
            {
                short nodeCount = bs.ReadInt16();
                node.ChildNodes = new List<CXMLNode>(nodeCount);
                for (var i = 0; i < nodeCount; i++)
                {
                    var childNode = ReadNode(bs);
                    node.ChildNodes.Add(childNode);
                }
            }

            return node;
        }

        private string ReadStringIndex(BinaryStream bs)
        {
            if (Strings.Count > ushort.MaxValue)
            {
                int idx = bs.ReadInt32();
                return Strings[idx];
            }
            else if (Strings.Count > byte.MaxValue)
            {
                ushort idx = bs.ReadUInt16();
                return Strings[idx];
            }
            else
            {
                byte idx = bs.Read1Byte();
                return Strings[idx];
            }
        }
    }

    public class CXMLNode
    {
        public List<CXMLNode> ChildNodes { get; set; }
        public List<CXMLAttribute> Attributes { get; set; }
        public string Name { get; set; }
    }

    public class CXMLAttribute
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
