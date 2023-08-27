using System.Diagnostics;

namespace ForzaTools.PlaygroundMiniZip
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Playground Minizip (PGZP) Extractor");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("- https://twitter.com/Nenkaai");
            Console.WriteLine("-----------------------------");

            if (args.Length == 0)
            {
                Console.WriteLine("Usage: <minizip file> [optional output directory]");
                return;
            }

            try
            {
                using MiniZip minizip = new MiniZip(args[0]);

                string outputDir = args.Length > 1 ? args[1] : Path.GetDirectoryName(args[0]);
                if (File.Exists(outputDir))
                {
                    Console.WriteLine("ERR: Output directory is a file");
                    return;
                }

                for (var i = 0; i < minizip.NumDirEntries; i++)
                    minizip.ExtractFile(i, outputDir, log: true);

                Console.WriteLine("Done.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}