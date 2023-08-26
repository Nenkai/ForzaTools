namespace ForzaTools.Bundle;

internal class Program
{
    static void Main(string[] args)
    {
        using var fs = new FileStream(args[0], FileMode.Open);
        var bundle = new Bundle();
        bundle.Load(fs);

        using var output = new FileStream(args[1], FileMode.Create);
        bundle.Serialize(output);
    }
}