
using System.Runtime.InteropServices;
using System;
using System.IO.Compression;
using System.Buffers.Binary;

namespace ForzaTools.Decryptor;

public class Program
{
    public static byte[] keywrapper_gamedb_decryptionkey;
    public static byte[] keywrapper_gamedb_mackey;

    public static byte[] keywrapper_sfs_decryptionkey;
    public static byte[] keywrapper_sfs_mackey;

    public static byte[] keywrapper_file_decryptionkey;
    public static byte[] keywrapper_file_mackey;

    public static byte[] keywrapper_profile_decryptionkey;
    public static byte[] keywrapper_profile_encryptionkey;
    public static byte[] keywrapper_profile_mackey;

    public static byte[] keywrapper_photo_decryptionkey;
    public static byte[] keywrapper_photo_encryptionkey;
    public static byte[] keywrapper_photo_mackey;

    public static byte[] keywrapper_dynamic_decryptionkey;
    public static byte[] keywrapper_dynamic_encryptionkey;
    public static byte[] keywrapper_dynamic_mackey;

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

        ReadKeys(exeDir, name);

        Console.WriteLine($"Processing '{args[0]}'");

        // photo key = 0x200, used for profile files (photos)
        // dynamic = 0x200, also used for some profile files seemingly

        if (args[0].Contains(".slt"))
        {
            Console.WriteLine("Detected input: GameDB, using gamedb keys");
            var provider = new TransformITCryptoProvider(keywrapper_gamedb_decryptionkey, keywrapper_gamedb_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var stream = new TransformITAesCryptoStream(fs, provider, 0x20000);
            var obfsStream = new ObfuscationStream(stream, MemoryMarshal.Cast<byte, int>(ObfuscationStream.Key)[0]);
            DecryptFile(obfsStream, (int)stream.Length, args[0]);
        }
        else if (args[0].Contains("sfsdata"))
        {
            Console.WriteLine("Detected input: Secure File System, using sfs keys");
            var provider = new TransformITCryptoProvider(keywrapper_sfs_decryptionkey, keywrapper_sfs_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var cryptoStream = new TransformITAesCryptoStream(fs, provider, 0x20000);
            DecryptFile(cryptoStream, (int)cryptoStream.Length, args[0]);
        }
        else if (args[0].Contains("ProfileData") || args[0].Contains("ProfileBackup") || args[0].Contains("VersionFlags") || args[0].Contains("UserPurchasesTelemetry"))
        {
            Console.WriteLine("Detected input: ProfileData file, using profile keys");
            var provider = new TransformITCryptoProvider(keywrapper_profile_decryptionkey, keywrapper_profile_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var cryptoStream = new TransformITAesCryptoStream(fs, provider, 0x200);
            DecryptFile(cryptoStream, (int)cryptoStream.Length, args[0]);
        }
        else if (args[0].Contains("RouteData"))
        {
            Console.WriteLine("Detected input: RouteData file, using dynamic keys");
            var provider = new TransformITCryptoProvider(keywrapper_dynamic_decryptionkey, keywrapper_dynamic_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var cryptoStream = new TransformITAesCryptoStream(fs, provider, 0x200);
            DecryptFile(cryptoStream, (int)cryptoStream.Length, args[0]);
        }
        else if (args[0].EndsWith(".zip"))
        {
            Console.WriteLine("Detected input: Zip file, using file keys");
            var zipFile = ZipFile.Open(args[0]);
            zipFile.ExtractAll();
        }
        else
        {
            Console.WriteLine("Input: Generic file, using file keys");
            var provider = new TransformITCryptoProvider(keywrapper_file_decryptionkey, keywrapper_file_mackey);

            using var fs = new FileStream(args[0], FileMode.Open);
            var stream = new TransformITAesCryptoStream(fs, provider, 0x200);
        }

    }

    private static void ReadKeys(string exeDir, string name)
    {
        // Gamedb
        if (File.Exists(Path.Combine(exeDir, "Keys", name, "gamedb_decryptionkey")))
            keywrapper_gamedb_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "gamedb_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "gamedb_mackey")))
            keywrapper_gamedb_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "gamedb_mackey"));

        // sfsdata
        if (File.Exists(Path.Combine(exeDir, "Keys", name, "sfs_decryptionkey")))
            keywrapper_sfs_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "sfs_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "sfs_mackey")))
            keywrapper_sfs_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "sfs_mackey"));

        // File
        if (File.Exists(Path.Combine(exeDir, "Keys", name, "file_decryptionkey")))
            keywrapper_file_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "file_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "file_mackey")))
            keywrapper_file_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "file_mackey"));

        // Profile
        if (File.Exists(Path.Combine(exeDir, "Keys", name, "profile_decryptionkey")))
            keywrapper_profile_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "profile_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "profile_encryptionkey")))
            keywrapper_profile_encryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "profile_encryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "profile_mackey")))
            keywrapper_profile_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "profile_mackey"));

        // Photo
        if (File.Exists(Path.Combine(exeDir, "Keys", name, "photo_decryptionkey")))
            keywrapper_photo_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "photo_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "photo_encryptionkey")))
            keywrapper_photo_encryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "photo_encryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "photo_mackey")))
            keywrapper_photo_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "photo_mackey"));

        // dynamic
        if (File.Exists(Path.Combine(exeDir, "Keys", name, "dynamic_decryptionkey")))
            keywrapper_dynamic_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "dynamic_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "dynamic_encryptionkey")))
            keywrapper_dynamic_encryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "dynamic_encryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", name, "dynamic_mackey")))
            keywrapper_dynamic_mackey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", name, "dynamic_mackey"));
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

        input.Close();
        input.Dispose();

        File.Move(inputFileName + ".temp", inputFileName, overwrite: true);
    }
}


