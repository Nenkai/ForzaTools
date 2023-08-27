using System.Xml;

namespace ForzaTools.BinaryXML
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Forza BXML <-> XML by Nenkai");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("- https://twitter.com/Nenkaai");
            Console.WriteLine("-----------------------------");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: <bxml or xml files>");
                Console.WriteLine("NOTE: Files will be automatically converted to binary or plain text");
            }

            foreach (var arg in args)
            {
                if (!File.Exists(arg))
                {
                    Console.WriteLine($"Skipping '{arg}', file does not exist");
                    continue;
                }

                using var fs = new FileStream(arg, FileMode.Open);
                using var bs = new BinaryReader(fs);

                if (fs.Length < 4)
                {
                    Console.WriteLine($"Skipping '{arg}', file too small");
                    continue;
                }

                if (bs.ReadUInt32() == BXML.MAGIC)
                {
                    try
                    {
                        fs.Position = 0;
                        var bxml = new BXML();
                        bxml.LoadFromStream(fs);

                        string output = arg + ".xml";
                        bxml.SerializeToTextXML(arg + ".xml");

                        Console.WriteLine($"Converted BXML '{arg}' -> XML");
                    } 
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to convert '{arg}' to XML - {e.Message}");
                    }
                }
                else if (arg.EndsWith(".xml"))
                {
                    fs.Position = 0;

                    try
                    {
                        XmlDocument document = new XmlDocument();
                        document.Load(fs);

                        var bxml = new BXML();
                        bxml.CreateFromXmlNode(document);

                        using var output = new FileStream(Path.ChangeExtension(arg, ".bxml"), FileMode.Create);
                        bxml.Serialize(output);

                        Console.WriteLine($"Converted XML '{arg}' -> BXML");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to convert '{arg}' to BXML - {e.Message}");
                    }
                }
            }
        }
    }
}