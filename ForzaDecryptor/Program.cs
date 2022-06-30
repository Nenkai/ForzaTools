
using System.Runtime.InteropServices;
using System;
using System.IO.Compression;
using System.Buffers.Binary;

namespace ForzaDecryptor;

public class Program
{
    public static byte[] keywrapper_gamedb_decryptionkey;
    public static byte[] keywrapper_gamedb_mackey;

    public static byte[] keywrapper_sfs_decryptionkey;
    public static byte[] keywrapper_sfs_mackey;

    public static byte[] keywrapper_file_decryptionkey;
    public static byte[] keywrapper_file_mackey;

    public static byte[] keywrapper_profile_decryptionkey;
    public static byte[] keywrapper_profile_mackey;

    public static void Main(string[] args)
    {
        Console.WriteLine("ForzaDecryptor - Nenkai#9075");

        string exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
        string exeDir = Path.GetDirectoryName(exePath);

        if (!File.Exists(Path.Combine(exeDir, "config.ini")))
        {
            Console.WriteLine("ERROR: config.ini is missing");
            return;
        }

        if (args.Length == 0)
        {
            Console.WriteLine("Input: FH5Decryptor <input_file>");
            Console.WriteLine("Input file is sfsdata, gamedb.slt, any custom encrypted file, or a zip.");
            Console.WriteLine("Make backups. This will overwrite files.");
            Console.WriteLine();

            return;
        }

        string name = string.Empty;

        foreach (var line in File.ReadAllLines(Path.Combine(exeDir, "config.ini")))
        {
            if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                continue;

            string[] spl = line.Split("|");
            if (spl.Length < 2)
                continue;

            string key = spl[0];
            switch (key)
            {
                case "GameName":
                    name = spl[1];
                    break;
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("ERROR: config.init is missing GameName property");
            return;
        }

        string file = args[0];
        if (!TransformITCryptoProvider.Init(Path.Combine(exeDir, "Keys"), name))
            return;

        if (!File.Exists(file))
        {
            Console.WriteLine("ERROR: Input file does not exist.");
            return;
        }

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "gamedb_decryptionkey")))
            keywrapper_gamedb_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "gamedb_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "gamedb_mackey")))
            keywrapper_gamedb_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "gamedb_mackey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "sfs_decryptionkey")))
            keywrapper_sfs_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "sfs_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "sfs_mackey")))
            keywrapper_sfs_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "sfs_mackey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "file_decryptionkey")))
            keywrapper_file_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "file_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "file_mackey")))
            keywrapper_file_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "file_mackey"));

        Console.WriteLine($"Processing '{args[0]}'");

        
        if (args[0].Contains(".slt"))
        {
            var provider = new TransformITCryptoProvider(keywrapper_gamedb_decryptionkey, keywrapper_gamedb_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var stream = new TransformITAesCryptoStream(fs, provider, 0x20000);
            var obfsStream = new ObfuscationStream(stream, MemoryMarshal.Cast<byte, int>(ObfuscationStream.Key)[0]);
            DecryptFile(obfsStream, (int)stream.Length, args[0]);
        }
        else if (args[0].Contains("sfsdata"))
        {
            var provider = new TransformITCryptoProvider(keywrapper_sfs_decryptionkey, keywrapper_sfs_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var cryptoStream = new TransformITAesCryptoStream(fs, provider, 0x20000);
            DecryptFile(cryptoStream, (int)cryptoStream.Length, args[0]);
        }
        else if (args[0].EndsWith(".ProfileData") || args[0].EndsWith(".ProfileBackup") || args[0].EndsWith(".VersionFlags") || args[0].EndsWith(".UserPurchasesTelemetry"))
        {
            var provider = new TransformITCryptoProvider(keywrapper_profile_decryptionkey, keywrapper_profile_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var cryptoStream = new TransformITAesCryptoStream(fs, provider, 0x200);
            DecryptFile(cryptoStream, (int)cryptoStream.Length, args[0]);
        }
        else if (args[0].EndsWith(".zip"))
        {
            var zipFile = ZipFile.Open(args[0]);
            zipFile.ExtractAll();
        }
        else
        {
            var provider = new TransformITCryptoProvider(keywrapper_file_decryptionkey, keywrapper_file_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var stream = new TransformITAesCryptoStream(fs, provider, 0x200);
        }
        
    }

    public static void DecryptFile(Stream input, int fileSize, string inputFileName)
    {
        using (FileStream output = new FileStream(inputFileName + ".temp", FileMode.Create))
        {
            byte[] buffer = new byte[0x20000];
            while (fileSize > 0)
            {
                int read = input.Read(buffer);
                output.Write(buffer, 0, read);

                fileSize -= read;
            }
        }

        File.Move(inputFileName + ".temp", inputFileName, overwrite: true);
    }
}


