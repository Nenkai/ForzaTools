namespace BXML
{
    internal class Program
    {
        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                if (File.Exists(arg))
                {
                    var bxml = BXML.ReadFile(arg);
                    Console.WriteLine($"Processing '{arg}'");
                    bxml.SerializeToTextXML(arg);
                }
            }
        }
    }
}