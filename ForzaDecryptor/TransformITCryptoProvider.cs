using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

namespace TransformIT
{
    public class TransformITCryptoProvider
    {
        public static int[][][] idcx = new int[17][][];
        public static int[][] tbl = new int[84][];

        public readonly int IVSize = 0x10;

        public byte[] Key { get; set; }
        public byte[] BaseIV { get; set; } = new byte[0x10];
        public byte[] CurrentIV { get; set; } = new byte[0x10];

        public TransformITCryptoProvider(byte[] key)
        {
            Key = key;
        }

        public static bool Init(string keysDir, string name)
        {
            if (!File.Exists(Path.Combine(keysDir, $"{name}.aes_state")))
            {
                Console.WriteLine($"aes_state file for Game Name '{name}' is missing");
                return false;
            }

            using FileStream fs = new FileStream(Path.Combine(keysDir, $"{name}.aes_state"), FileMode.Open);
            using BinaryReader br = new BinaryReader(fs);

            for (var i = 0; i < 84; i++)
            {
                tbl[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    tbl[i][j] = br.ReadInt32();
                }
            }

            if (!File.Exists(Path.Combine(keysDir, $"{name}.indices")))
            {
                Console.WriteLine($"indices file for Game Name '{name}' is missing");
                return false;
            }

            using StreamReader reader = File.OpenText(Path.Combine(keysDir, $"{name}.indices"));
            for (var i = 0; i < 17; i++)
                idcx[i] = new int[4][];

            int i2 = 0;
            int j2 = 0;
            while (i2 < 17)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                    continue;

                var arr = line.Split(",").Select(e => int.Parse(e.Trim())).ToArray();
                idcx[i2][j2++] = arr;
                if (j2 > 3)
                {
                    j2 = 0;
                    i2++;
                }
            }

            return true;
        }

        public void TFIT_wbaes_cbc_decrypt(Span<byte> key, Span<byte> input, int size, Span<byte> iv, Span<byte> output)
        {
            const int blockSize = 0x10;

            int nBlocks = size / blockSize;

            Span<byte> inBlocks = new byte[32];
            Span<byte> currentIv = iv;

            if ((size % 0x10) == 0 && nBlocks != 0)
            {
                Span<int> decryptedBlock = new int[4];
                for (int i = 0; i < nBlocks; i++)
                {
                    // memcpy(&inBlock[16 * (i & 1)], &pInput[16 * i], 0x10ui64);
                    input.Slice(i * blockSize, blockSize).CopyTo(inBlocks.Slice(blockSize * (i & 1)));

                    // Decrypt current block using key
                    TFIT_op_iAES(MemoryMarshal.Cast<byte, int>(key),
                        MemoryMarshal.Cast<byte, int>(inBlocks.Slice(blockSize * (i & 1))),
                        decryptedBlock);

                    // Apply xor from custom key
                    var decryptedBlockAsBytes = MemoryMarshal.Cast<int, byte>(decryptedBlock);
                    block_xor(decryptedBlockAsBytes, currentIv, output.Slice(i * blockSize));

                    /* For bruteforcing, this has never worked
                    byte[] temp = new byte[0x10];
                    for (uint j = 0; j < uint.MaxValue; j++)
                    {
                        output.Slice(blockSize * (i & 1), 0x10).CopyTo(temp);

                        int v = 0;
                        ObfuscationStream.TransformBlock((int)j, temp, temp, 4, ref v); 

                        if (temp[0] == 0x53 && temp[1] == 0x51 && temp[2] == 0x4C && temp[3] == 0x69 && temp[4] == 0x74)
                        {
                            throw new Exception("got");
                        }
                    } */

                    currentIv = inBlocks.Slice(blockSize * (i & 1));
                }

                // Report current key to caller
                currentIv.CopyTo(iv);
            }
        }

        private void block_xor(Span<byte> inputBlock, Span<byte> xorKeyBytes, Span<byte> outputBlock)
        {
            Span<long> inputBlockLong = MemoryMarshal.Cast<byte, long>(inputBlock);
            Span<long> xorKeyBytesLong = MemoryMarshal.Cast<byte, long>(xorKeyBytes);
            Span<long> outputBlockLong = MemoryMarshal.Cast<byte, long>(outputBlock);

            outputBlockLong[0] = xorKeyBytesLong[0] ^ inputBlockLong[0];
            outputBlockLong[1] = xorKeyBytesLong[1] ^ inputBlockLong[1];
        }

        private void TFIT_op_iAES(Span<int> key, Span<int> blk, Span<int> output)
        {
            /* LOCATIONS WILL CHANGE WITH UPDATES! NEED TO BE MANUALLY ADJUSTED in INDICES files! */

            int[][] r = idcx[0];
            // TFIT_rInv

            int v1_1 = tbl[r[0][0]][(blk[0] >> 24) & 0xFF] ^ tbl[r[0][1]][(blk[0] >> 16) & 0xFF] ^ tbl[r[0][2]][(blk[0] >> 8) & 0xFF] ^ tbl[r[0][3]][blk[0] & 0xFF] ^ key[4];
            int v1_2 = tbl[r[1][0]][(blk[1] >> 24) & 0xFF] ^ tbl[r[1][1]][(blk[1] >> 16) & 0xFF] ^ tbl[r[1][2]][(blk[1] >> 8) & 0xFF] ^ tbl[r[1][3]][blk[1] & 0xFF] ^ key[5];
            int v1_3 = tbl[r[2][0]][(blk[2] >> 24) & 0xFF] ^ tbl[r[2][1]][(blk[2] >> 16) & 0xFF] ^ tbl[r[2][2]][(blk[2] >> 8) & 0xFF] ^ tbl[r[2][3]][blk[2] & 0xFF] ^ key[6];
            int v1_4 = tbl[r[3][0]][(blk[3] >> 24) & 0xFF] ^ tbl[r[3][1]][(blk[3] >> 16) & 0xFF] ^ tbl[r[3][2]][(blk[3] >> 8) & 0xFF] ^ tbl[r[3][3]][blk[3] & 0xFF] ^ key[7];

            // TFIT_r
            r = idcx[1];
            int v2_1 = tbl[r[0][0]][(v1_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v1_1 >> 16) & 0xFF] ^ tbl[r[0][2]][(v1_1 >> 8) & 0xFF] ^ tbl[r[0][3]][v1_1 & 0xFF] ^ key[8];
            int v2_2 = tbl[r[1][0]][(v1_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v1_2 >> 16) & 0xFF] ^ tbl[r[1][2]][(v1_2 >> 8) & 0xFF] ^ tbl[r[1][3]][v1_2 & 0xFF] ^ key[9];
            int v2_3 = tbl[r[2][0]][(v1_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v1_3 >> 16) & 0xFF] ^ tbl[r[2][2]][(v1_3 >> 8) & 0xFF] ^ tbl[r[2][3]][v1_3 & 0xFF] ^ key[10];
            int v2_4 = tbl[r[3][0]][(v1_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v1_4 >> 16) & 0xFF] ^ tbl[r[3][2]][(v1_4 >> 8) & 0xFF] ^ tbl[r[3][3]][v1_4 & 0xFF] ^ key[11];

            // Next rounds are different
            // TFIT_rij
            r = idcx[2];
            int v3_4 = tbl[r[0][0]][(v2_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v2_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v2_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v2_4 & 0xFF] ^ key[15];
            int v3_1 = tbl[r[1][0]][(v2_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v2_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v2_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v2_1 & 0xFF] ^ key[12];
            int v3_2 = tbl[r[2][0]][(v2_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v2_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v2_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v2_2 & 0xFF] ^ key[13];
            int v3_3 = tbl[r[3][0]][(v2_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v2_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v2_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v2_3 & 0xFF] ^ key[14];

            r = idcx[3];
            int v4_4 = tbl[r[0][0]][(v3_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v3_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v3_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v3_4 & 0xFF] ^ key[19];
            int v4_1 = tbl[r[1][0]][(v3_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v3_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v3_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v3_1 & 0xFF] ^ key[16];
            int v4_2 = tbl[r[2][0]][(v3_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v3_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v3_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v3_2 & 0xFF] ^ key[17];
            int v4_3 = tbl[r[3][0]][(v3_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v3_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v3_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v3_3 & 0xFF] ^ key[18];

            r = idcx[4];
            int v5_4 = tbl[r[0][0]][(v4_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v4_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v4_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v4_4 & 0xFF] ^ key[23];
            int v5_1 = tbl[r[1][0]][(v4_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v4_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v4_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v4_1 & 0xFF] ^ key[20];
            int v5_2 = tbl[r[2][0]][(v4_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v4_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v4_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v4_2 & 0xFF] ^ key[21];
            int v5_3 = tbl[r[3][0]][(v4_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v4_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v4_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v4_3 & 0xFF] ^ key[22];

            r = idcx[5];
            int v6_4 = tbl[r[0][0]][(v5_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v5_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v5_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v5_4 & 0xFF] ^ key[27];
            int v6_1 = tbl[r[1][0]][(v5_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v5_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v5_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v5_1 & 0xFF] ^ key[24];
            int v6_2 = tbl[r[2][0]][(v5_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v5_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v5_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v5_2 & 0xFF] ^ key[25];
            int v6_3 = tbl[r[3][0]][(v5_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v5_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v5_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v5_3 & 0xFF] ^ key[26];

            r = idcx[6];
            int v7_4 = tbl[r[0][0]][(v6_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v6_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v6_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v6_4 & 0xFF] ^ key[31];
            int v7_1 = tbl[r[1][0]][(v6_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v6_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v6_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v6_1 & 0xFF] ^ key[28];
            int v7_2 = tbl[r[2][0]][(v6_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v6_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v6_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v6_2 & 0xFF] ^ key[29];
            int v7_3 = tbl[r[3][0]][(v6_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v6_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v6_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v6_3 & 0xFF] ^ key[30];

            r = idcx[7];
            int v8_4 = tbl[r[0][0]][(v7_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v7_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v7_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v7_4 & 0xFF] ^ key[35];
            int v8_1 = tbl[r[1][0]][(v7_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v7_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v7_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v7_1 & 0xFF] ^ key[32];
            int v8_2 = tbl[r[2][0]][(v7_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v7_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v7_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v7_2 & 0xFF] ^ key[33];
            int v8_3 = tbl[r[3][0]][(v7_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v7_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v7_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v7_3 & 0xFF] ^ key[34];

            r = idcx[8];
            int v9_4 = tbl[r[0][0]][(v8_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v8_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v8_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v8_4 & 0xFF] ^ key[39];
            int v9_1 = tbl[r[1][0]][(v8_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v8_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v8_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v8_1 & 0xFF] ^ key[36];
            int v9_2 = tbl[r[2][0]][(v8_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v8_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v8_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v8_2 & 0xFF] ^ key[37];
            int v9_3 = tbl[r[3][0]][(v8_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v8_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v8_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v8_3 & 0xFF] ^ key[38];

            r = idcx[9];
            int v10_4 = tbl[r[0][0]][(v9_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v9_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v9_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v9_4 & 0xFF] ^ key[43];
            int v10_1 = tbl[r[1][0]][(v9_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v9_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v9_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v9_1 & 0xFF] ^ key[40];
            int v10_2 = tbl[r[2][0]][(v9_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v9_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v9_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v9_2 & 0xFF] ^ key[41];
            int v10_3 = tbl[r[3][0]][(v9_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v9_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v9_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v9_3 & 0xFF] ^ key[42];

            r = idcx[10];
            int v11_4 = tbl[r[0][0]][(v10_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v10_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v10_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v10_4 & 0xFF] ^ key[47];
            int v11_1 = tbl[r[1][0]][(v10_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v10_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v10_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v10_1 & 0xFF] ^ key[44];
            int v11_2 = tbl[r[2][0]][(v10_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v10_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v10_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v10_2 & 0xFF] ^ key[45];
            int v11_3 = tbl[r[3][0]][(v10_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v10_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v10_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v10_3 & 0xFF] ^ key[46];

            r = idcx[11];
            int v12_4 = tbl[r[0][0]][(v11_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v11_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v11_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v11_4 & 0xFF] ^ key[51];
            int v12_1 = tbl[r[1][0]][(v11_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v11_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v11_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v11_1 & 0xFF] ^ key[48];
            int v12_2 = tbl[r[2][0]][(v11_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v11_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v11_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v11_2 & 0xFF] ^ key[49];
            int v12_3 = tbl[r[3][0]][(v11_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v11_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v11_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v11_3 & 0xFF] ^ key[50];

            r = idcx[12];
            int v13_4 = tbl[r[0][0]][(v12_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v12_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v12_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v12_4 & 0xFF] ^ key[55];
            int v13_1 = tbl[r[1][0]][(v12_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v12_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v12_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v12_1 & 0xFF] ^ key[52];
            int v13_2 = tbl[r[2][0]][(v12_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v12_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v12_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v12_2 & 0xFF] ^ key[53];
            int v13_3 = tbl[r[3][0]][(v12_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v12_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v12_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v12_3 & 0xFF] ^ key[54];

            r = idcx[13];
            int v14_4 = tbl[r[0][0]][(v13_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v13_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v13_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v13_4 & 0xFF] ^ key[59];
            int v14_1 = tbl[r[1][0]][(v13_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v13_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v13_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v13_1 & 0xFF] ^ key[56];
            int v14_2 = tbl[r[2][0]][(v13_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v13_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v13_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v13_2 & 0xFF] ^ key[57];
            int v14_3 = tbl[r[3][0]][(v13_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v13_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v13_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v13_3 & 0xFF] ^ key[58];

            r = idcx[14];
            int v15_4 = tbl[r[0][0]][(v14_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v14_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v14_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v14_4 & 0xFF] ^ key[63];
            int v15_1 = tbl[r[1][0]][(v14_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v14_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v14_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v14_1 & 0xFF] ^ key[60];
            int v15_2 = tbl[r[2][0]][(v14_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v14_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v14_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v14_2 & 0xFF] ^ key[61];
            int v15_3 = tbl[r[3][0]][(v14_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v14_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v14_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v14_3 & 0xFF] ^ key[62];

            r = idcx[15];
            int v16_4 = tbl[r[0][0]][(v15_1 >> 24) & 0xFF] ^ tbl[r[0][1]][(v15_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v15_3 >> 8) & 0xFF] ^ tbl[r[0][3]][v15_4 & 0xFF] ^ key[67];
            int v16_1 = tbl[r[1][0]][(v15_2 >> 24) & 0xFF] ^ tbl[r[1][1]][(v15_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v15_4 >> 8) & 0xFF] ^ tbl[r[1][3]][v15_1 & 0xFF] ^ key[64];
            int v16_2 = tbl[r[2][0]][(v15_3 >> 24) & 0xFF] ^ tbl[r[2][1]][(v15_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v15_1 >> 8) & 0xFF] ^ tbl[r[2][3]][v15_2 & 0xFF] ^ key[65];
            int v16_3 = tbl[r[3][0]][(v15_4 >> 24) & 0xFF] ^ tbl[r[3][1]][(v15_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v15_2 >> 8) & 0xFF] ^ tbl[r[3][3]][v15_3 & 0xFF] ^ key[66];

            // Final block is back to normal
            r = idcx[16];
            int v17_2 = tbl[r[0][0]][(v16_2 >> 24) & 0xFF] ^ tbl[r[0][1]][(v16_2 >> 16) & 0xFF] ^ tbl[r[0][2]][(v16_2 >> 8) & 0xFF] ^ tbl[r[0][3]][v16_2 & 0xFF] ^ key[69];
            int v17_3 = tbl[r[1][0]][(v16_3 >> 24) & 0xFF] ^ tbl[r[1][1]][(v16_3 >> 16) & 0xFF] ^ tbl[r[1][2]][(v16_3 >> 8) & 0xFF] ^ tbl[r[1][3]][v16_3 & 0xFF] ^ key[70];
            int v17_4 = tbl[r[2][0]][(v16_4 >> 24) & 0xFF] ^ tbl[r[2][1]][(v16_4 >> 16) & 0xFF] ^ tbl[r[2][2]][(v16_4 >> 8) & 0xFF] ^ tbl[r[2][3]][v16_4 & 0xFF] ^ key[71];
            int v17_1 = tbl[r[3][0]][(v16_1 >> 24) & 0xFF] ^ tbl[r[3][1]][(v16_1 >> 16) & 0xFF] ^ tbl[r[3][2]][(v16_1 >> 8) & 0xFF] ^ tbl[r[3][3]][v16_1 & 0xFF] ^ key[68];

            output[0] = v17_1;
            output[1] = v17_2;
            output[2] = v17_3;
            output[3] = v17_4;
        }
    }
}
