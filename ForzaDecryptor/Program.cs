
using System.Runtime.InteropServices;
using System;
using System.IO.Compression;

namespace TransformIT;

public class Program
{
    public static byte[] keywrapper_gamedb_decryptionkey;
    public static byte[] keywrapper_sfs_decryptionkey;
    public static byte[] keywrapper_file_decryptionkey;

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


        Console.WriteLine(exeDir);

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

        if (File.Exists(Path.Combine(exeDir, "Keys", $"{name}.gamedb_decryptionkey")))
            keywrapper_gamedb_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", $"{name}.gamedb_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", $"{name}.sfs_decryptionkey")))
            keywrapper_sfs_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", $"{name}.sfs_decryptionkey"));

        if (File.Exists(Path.Combine(exeDir, "Keys", $"{name}.file_decryptionkey")))
            keywrapper_file_decryptionkey = File.ReadAllBytes(Path.Combine(exeDir, "Keys", $"{name}.file_decryptionkey"));

        Console.WriteLine($"Processing '{args[0]}'");

        if (args[0].Contains(".slt"))
        {
            DecryptFile(args[0], keywrapper_gamedb_decryptionkey, 0x20000, isSourceObfuscated: true);
            
        }
        else if (args[0].Contains("sfsdata"))
        {
            DecryptFile(args[0], keywrapper_sfs_decryptionkey, 0x20000);
        }
        else if (args[0].EndsWith(".zip"))
        {
            var zipFile = ZipFile.Open(args[0]);
            zipFile.ExtractAll();
        }
        else
        {
            DecryptFile(args[0], keywrapper_file_decryptionkey, 0x200);
        }
    }

    public static void DecryptFile(string inputFileName, byte[] key, int chunkSize, bool isSourceObfuscated = false)
    {
        var provider = new TransformITCryptoProvider(key);

        using (FileStream input = new FileStream(inputFileName, FileMode.Open))
        using (FileStream output = new FileStream(inputFileName + ".temp", FileMode.Create))
        {
            ProcessFile(provider, input, output, chunkSize, isSourceObfuscated);
        }

        File.Move(inputFileName + ".temp", inputFileName, overwrite: true);
    }

    private static void ProcessFile(TransformITCryptoProvider provider, Stream input, Stream output, int chunkSize, bool isSourceObfuscated = false)
    {
        BinaryReader br = new BinaryReader(input);

        // Header - Base IV
        br.Read(provider.BaseIV);
        provider.BaseIV.CopyTo(provider.CurrentIV.AsSpan());

        int lastChunkPad = br.ReadInt32();

        int blockSize = (chunkSize + provider.IVSize);
        long fileSizeNoHeader = input.Length - (0x10 + 0x04 + 0x10); // IV + Pad Size + HMac
        long blockCount = (fileSizeNoHeader / blockSize);

        // Begin decryption in chunks
        byte[] inputBuffer = new byte[chunkSize];

        int pos = 0;
        int bufferLen = chunkSize;

        byte[] hmac = new byte[provider.IVSize];
        br.Read(hmac);

        for (var i = 0; i < blockCount; i++)
        {
            if (i == blockCount - 1)
                bufferLen = chunkSize - lastChunkPad;

            // Read
            br.Read(inputBuffer, 0, chunkSize);

            // Decrypt
            provider.TFIT_wbaes_cbc_decrypt(provider.Key, inputBuffer, chunkSize, provider.CurrentIV, inputBuffer);

            if (isSourceObfuscated)
            {
                // Decrypt 2 (database)
                ObfuscationStream.TransformBlock(
                    MemoryMarshal.Cast<byte, int>(ObfuscationStream.Key)[0],
                    inputBuffer,
                    inputBuffer,
                    bufferLen,
                    ref pos);
            }

            // Read next IV
            br.Read(provider.CurrentIV);

            // TODO: verify hmac for each block

            // Done, copy
            output.Write(inputBuffer, 0, bufferLen);
        }
    }
}


