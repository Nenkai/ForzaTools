using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Syroot.BinaryData;

using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace TransformIT
{
    internal class ZipFile
    {
        private FileStream inputStream;

        public List<ZipEntry> Entries { get; set; } = new();

        public string ZipName { get; set; }
        public string FullPath { get; set; }

        public static ZipFile Open(string fileName)
        {
            var zip = new ZipFile();
            zip.ZipName = Path.GetFileNameWithoutExtension(fileName);
            zip.FullPath = Path.GetDirectoryName(fileName);

            var fs = new FileStream(fileName, FileMode.Open);
            zip.inputStream = fs;

            var br = new BinaryStream(zip.inputStream);

            while (fs.Position != fs.Length)
            {
                var entry = new ZipEntry();
                uint signature = br.ReadUInt32();

                if (signature == 0x04034b50)
                {
                    entry.Version = br.ReadUInt16();
                    entry.Flags = br.ReadUInt16();
                    entry.Compression = br.ReadUInt16();
                    entry.ModTime = br.ReadUInt16();
                    entry.ModeDate = br.ReadUInt16();
                    entry.CRC32 = br.ReadUInt32();
                    entry.CompressedSize = br.ReadUInt32();
                    entry.UncompressedSize = br.ReadUInt32();

                    int fileNameLen = br.ReadInt16();
                    int extraFieldLen = br.ReadInt16();

                    entry.FileName = br.ReadString(fileNameLen);
                    br.Position += extraFieldLen;

                    entry.DataOffset = br.Position;
                    br.Position += entry.CompressedSize;

                    zip.Entries.Add(entry);
                }
                else
                    break;

                
            }

            return zip;
        }

        public long roundUp(long numToRound, int multiple)
        {
            if (multiple == 0)
                return numToRound;

            long remainder = numToRound % multiple;
            if (remainder == 0)
                return numToRound;

            return numToRound + multiple - remainder;
        }

        public void ExtractAll()
        {
            foreach (var file in Entries)
            {
                Console.WriteLine($"Zip Extracting: {file.FileName}");
                Extract(file, Path.Combine(FullPath, ZipName, file.FileName));
            }
        }

        public void Extract(ZipEntry entry, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var output = File.Create(path);
            Extract(entry, output);
        }

        public void Extract(ZipEntry entry, Stream output)
        {
            inputStream.Position = entry.DataOffset;

            if (entry.Compression == 22)
            {
                const int chunkSize = 0x200;

                var provider = new TransformITCryptoProvider(Program.keywrapper_file_decryptionkey);
                inputStream.Read(provider.BaseIV);

                provider.BaseIV.CopyTo(provider.CurrentIV.AsSpan());

                int lastChunkPad = inputStream.ReadInt32();

                int blockSize = (chunkSize + provider.IVSize);
                long fileSizeNoHeader = entry.CompressedSize - (0x10 + 0x04 + 0x10); // IV + Pad Size + HMac

                long roundedSize = roundUp(fileSizeNoHeader, blockSize);
                long blockCount = (roundedSize / blockSize);

                // Begin decryption in chunks
                byte[] inputBuffer = new byte[chunkSize];

                int bufferLen = chunkSize;

                byte[] hmac = new byte[provider.IVSize];
                inputStream.Read(hmac);

                // Setup compression
                Inflater inflater = new Inflater(true);
                byte[] deflateBuffer = new byte[0x4000];

                for (var i = 0; i < blockCount; i++)
                {
                    if (i == blockCount - 1)
                        bufferLen = chunkSize - lastChunkPad;

                    // Read
                    inputStream.Read(inputBuffer, 0, chunkSize);

                    // Decrypt
                    provider.TFIT_wbaes_cbc_decrypt(provider.Key, inputBuffer, chunkSize, provider.CurrentIV, inputBuffer); // Always decrypt the whole chunk 

                    // Done, copy
                    inflater.SetInput(inputBuffer, 0, bufferLen);

                    while (!inflater.IsNeedingInput)
                    {
                        int deflated = inflater.Inflate(deflateBuffer);
                        output.Write(deflateBuffer, 0, deflated);
                    }

                    // Read next IV
                    inputStream.Read(provider.CurrentIV);


                    // TODO: verify hmac for each block
                }


            }
        }
    }
}
