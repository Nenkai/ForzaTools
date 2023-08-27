using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using System.Xml;
using Syroot.BinaryData;
using System.Xml.Linq;

namespace ForzaTools.BinaryXML
{
    public class BXML
    {
        public List<string> Strings { get; set; } = new List<string>();
        public CXMLNode RootNode { get; set; }

        public const uint MAGIC = 0x4C4D5842; // BXML
        public const byte VERSION_CURRENT = 2;

        public void LoadFromBinaryFile(string file)
        {
            using var fs = new FileStream(file, FileMode.Open);
            using var bs = new BinaryStream(fs);

            LoadFromStream(fs);
        }

        public void LoadFromStream(Stream stream)
        {
            using var bs = new BinaryStream(stream);

            uint magic = bs.ReadUInt32();
            if (magic != MAGIC)
                throw new InvalidDataException("Not a BXML file. (Magic did not match)");

            byte version = bs.Read1Byte();
            if (version > VERSION_CURRENT)
                throw new InvalidDataException($"BXML Version is above maximum supported ({version} > 2).");

            int stringCount = bs.ReadInt32();
            int stringTableSize = bs.ReadInt32();

            Strings = new List<string>(stringCount);
            for (var i = 0; i < stringCount; i++)
                Strings.Add(bs.ReadString(StringCoding.Int16CharCount));

            byte rootFlag = bs.Read1Byte();
            RootNode = ReadBinaryXmlNode(bs);
        }

        public static void SerializeXMLToBXML(string xmlFile, string outputPath)
        {
            var doc = new XmlDocument();
            doc.Load(xmlFile);

            var bxml = new BXML();
            bxml.CreateFromXmlNode(doc);

            using var fs = new FileStream(outputPath, FileMode.Create);
            bxml.Serialize(fs);
        }

        private CXMLNode RegisterXmlNode(XmlNode node)
        {
            var bxmlNode = new CXMLNode();
            bxmlNode.Name = node.Name;
            bxmlNode.Flags |= CXmlNodeFlags.IsNode;

            foreach (XmlAttribute attr in node.Attributes)
            {
                bxmlNode.Flags |= CXmlNodeFlags.HasAttributes;
                bxmlNode.Attributes.Add(new CXMLAttribute(attr.Name, attr.Value));
            }

            foreach (XmlNode childNode in node.ChildNodes)
            {
                bxmlNode.Flags |= CXmlNodeFlags.HasChildNodes;
                var childBxmlNode = RegisterXmlNode(childNode);
                bxmlNode.ChildNodes.Add(childBxmlNode);
            }

            return bxmlNode;
        }

        public void CreateFromXmlNode(XmlDocument doc)
        {
            foreach (XmlNode n in doc.ChildNodes)
            {
                if (n is XmlDeclaration)
                    continue;

                RootNode = RegisterXmlNode(n);
            }

            BuildStringList(RootNode);
            Strings.Sort(AlphaNumStringComparer.Default);
        }

        private void BuildStringList(CXMLNode node)
        {
            AddString(node.Name);
            foreach (CXMLAttribute attr in node.Attributes)
            {
                AddString(attr.Name);
                AddString(attr.Value);
            }

            foreach (CXMLNode childNode in node.ChildNodes)
            {
                BuildStringList(childNode);
            }
        }


        /// <summary>
        /// Serializes to binary to the provided stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Serialize(Stream stream)
        {
            using var bs = new BinaryStream(stream, ByteConverter.Little);
            bs.WriteUInt32(MAGIC);
            bs.WriteByte(VERSION_CURRENT);
            bs.WriteInt32(Strings.Count);
            bs.Position += 4;

            long baseTableOffset = bs.Position;
            for (int i = 0; i < Strings.Count; i++)
                bs.WriteString(Strings[i], StringCoding.Int16CharCount);
            long endStringTableOffset = bs.Position;

            bs.Position = baseTableOffset - 4;
            bs.WriteUInt32((uint)(endStringTableOffset - baseTableOffset));
            bs.Position = endStringTableOffset;

            bs.WriteByte(1);
            SerializeNode(bs, RootNode);
        }

        private void SerializeNode(BinaryStream bs, CXMLNode node)
        {
            bs.WriteByte((byte)node.Flags);
            SerializeStringIndex(bs, node.Name);

            if (node.Flags.HasFlag(CXmlNodeFlags.HasAttributes))
            {
                bs.WriteByte((byte)node.Attributes.Count);

                foreach (CXMLAttribute attr in node.Attributes)
                {
                    SerializeStringIndex(bs, attr.Name);
                    SerializeStringIndex(bs, attr.Value);
                }
            }

            if (node.Flags.HasFlag(CXmlNodeFlags.HasChildNodes))
            {
                bs.WriteUInt16((ushort)node.ChildNodes.Count);

                foreach (CXMLNode childNode in node.ChildNodes)
                    SerializeNode(bs, childNode);
            }
        }

        private void SerializeStringIndex(BinaryStream bs, string str)
        {
            int index = Strings.IndexOf(str);
            if (Strings.Count > ushort.MaxValue)
            {
                bs.WriteInt32(index);
            }
            else if (Strings.Count > byte.MaxValue)
            {
                bs.WriteUInt16((ushort)index);
            }
            else
            {
                bs.WriteByte((byte)index);
            }
        }

        public void SerializeToTextXML(string outputPath)
        {
            using (XmlWriter writer = XmlWriter.Create(outputPath, new XmlWriterSettings() { Indent = true }))
            {
                WriteXmlNode(writer, RootNode);
            }
        }

        private void WriteXmlNode(XmlWriter writer, CXMLNode node)
        {
            writer.WriteStartElement(node.Name);

            if (node.Attributes is not null)
            {
                foreach (var attr in node.Attributes)
                    writer.WriteAttributeString(attr.Name, attr.Value);
            }

            if (node.ChildNodes != null)
            {
                foreach (var cnode in node.ChildNodes)
                    WriteXmlNode(writer, cnode);
            }

            writer.WriteEndElement();
            writer.Flush();
        }

        public CXMLNode ReadBinaryXmlNode(BinaryStream bs)
        {
            var node = new CXMLNode();

            node.Flags = (CXmlNodeFlags)bs.Read1Byte();
            node.Name = ReadStringIndex(bs);

            if (node.Flags.HasFlag(CXmlNodeFlags.HasAttributes)) // attributes
            {
                byte attrCount = bs.Read1Byte();
                node.Attributes = new List<CXMLAttribute>(attrCount);

                for (var i = 0; i < attrCount; i++)
                {
                    string key = ReadStringIndex(bs);
                    string value = ReadStringIndex(bs);
                    node.Attributes.Add(new CXMLAttribute(key, value));
                }
            }

            if (node.Flags.HasFlag(CXmlNodeFlags.HasChildNodes))
            {
                short nodeCount = bs.ReadInt16();
                node.ChildNodes = new List<CXMLNode>(nodeCount);
                for (var i = 0; i < nodeCount; i++)
                {
                    var childNode = ReadBinaryXmlNode(bs);
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

        private void AddString(string str)
        {
            if (!Strings.Contains(str))
                Strings.Add(str);
        }
    }

    public class CXMLNode
    {
        public CXmlNodeFlags Flags { get; set; }
        public List<CXMLNode> ChildNodes { get; set; } = new();
        public List<CXMLAttribute> Attributes { get; set; } = new();
        public string Name { get; set; }
    }

    public record CXMLAttribute(string Name, string Value);

    public enum CXmlNodeFlags : byte
    {
        IsNode = 1 << 0,
        HasAttributes = 1 << 1,
        HasChildNodes = 1 << 2,
    }
}
