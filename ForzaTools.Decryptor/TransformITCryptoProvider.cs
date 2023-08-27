using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

namespace ForzaTools.Decryptor
{
    public class TransformITCryptoProvider
    {
        public static int[][][] idcx = new int[17][][];     // Decryption - AES White Box States Indices
        public static int[][][] vidcx = new int[13][][];    // MAC - AES White Box States Indices
        public static int[][][] inv_idcx = new int[17][][]; // Encryption - AES White Box States Indices

        public static int[][] sbox = new int[84][];      // Decryption - AES White Box/SBOX
        public static int[][] inv_sbox = new int[84][];  // Encryption - AES White Box/SBOX
        public static int[][] vsbox = new int[84][];     // MAC - AES White Box/SBOX

        public readonly uint IVSize = 0x10;
        public const int BLOCK_LEN = 0x10;

        public byte[] Key { get; set; }
        public byte[] MacKey { get; set; }

        public byte[] BaseIV { get; set; } = new byte[0x10];
        public byte[] CurrentIV { get; set; } = new byte[0x10];

        public TransformITCryptoProvider(byte[] key, byte[] macKey)
        {
            Key = key;
            MacKey = macKey;
        }

        public static bool Init(string keysDir, string name)
        {
            ReadDecryptionData(keysDir, name);
            ReadEncryptionData(keysDir, name);
            ReadVerifData(keysDir, name);
            
            return true;
        }

        private static bool ReadDecryptionData(string keysDir, string name)
        {
            if (!File.Exists(Path.Combine(keysDir, name, "aes_sbox_decrypt")))
            {
                Console.WriteLine($"aes_sbox_decrypt file for Game Name '{name}' is missing");
                return false;
            }

            using FileStream fs = new FileStream(Path.Combine(keysDir, name, "aes_sbox_decrypt"), FileMode.Open);
            using BinaryReader br = new BinaryReader(fs);

            for (var i = 0; i < 84; i++)
            {
                inv_sbox[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    inv_sbox[i][j] = br.ReadInt32();
                }
            }

            if (!File.Exists(Path.Combine(keysDir, name, "aes_sbox_indices_dec")))
            {
                Console.WriteLine($"aes_sbox_indices_dec file for Game Name '{name}' is missing");
                return false;
            }

            using StreamReader reader = File.OpenText(Path.Combine(keysDir, name, "aes_sbox_indices_dec"));
            for (var i = 0; i < 17; i++)
                inv_idcx[i] = new int[4][];

            int i2 = 0;
            int j2 = 0;
            while (i2 < 17)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                    continue;

                var arr = line.Split(",").Select(e => int.Parse(e.Trim())).ToArray();
                inv_idcx[i2][j2++] = arr;
                if (j2 > 3)
                {
                    j2 = 0;
                    i2++;
                }
            }

            return true;
        }

        private static bool ReadEncryptionData(string keysDir, string name)
        {
            if (!File.Exists(Path.Combine(keysDir, name, "aes_sbox_encrypt")))
            {
                Console.WriteLine($"aes_sbox_encrypt file for Game Name '{name}' is missing");
                return false;
            }

            using var fs = new FileStream(Path.Combine(keysDir, name, "aes_sbox_encrypt"), FileMode.Open);
            using var br = new BinaryReader(fs);

            for (var i = 0; i < 84; i++)
            {
                sbox[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    sbox[i][j] = br.ReadInt32();
                }
            }

            if (!File.Exists(Path.Combine(keysDir, name, "aes_sbox_indices_enc")))
            {
                Console.WriteLine($"aes_sbox_indices_enc file for Game Name '{name}' is missing");
                return false;
            }

            using StreamReader reader = File.OpenText(Path.Combine(keysDir, name, "aes_sbox_indices_enc"));
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

        private static bool ReadVerifData(string keysDir, string name)
        {
            if (!File.Exists(Path.Combine(keysDir, name, "aes_sbox_verif")))
            {
                Console.WriteLine($"aes_sbox_encrypt file for Game Name '{name}' is missing");
                return false;
            }

            using var fs = new FileStream(Path.Combine(keysDir, name, "aes_sbox_verif"), FileMode.Open);
            using var br = new BinaryReader(fs);

            for (var i = 0; i < 84; i++)
            {
                vsbox[i] = new int[256];
                for (var j = 0; j < 256; j++)
                {
                    vsbox[i][j] = br.ReadInt32();
                }
            }

            if (!File.Exists(Path.Combine(keysDir, name, "aes_sbox_indices_verif")))
            {
                Console.WriteLine($"aes_sbox_indices_verif file for Game Name '{name}' is missing");
                return false;
            }

            using StreamReader reader = File.OpenText(Path.Combine(keysDir, name, "aes_sbox_indices_verif"));
            for (var i = 0; i < 13; i++)
                vidcx[i] = new int[4][];

            int i2 = 0;
            int j2 = 0;
            while (i2 < 13)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                    continue;

                var arr = line.Split(",").Select(e => int.Parse(e.Trim())).ToArray();
                vidcx[i2][j2++] = arr;
                if (j2 > 3)
                {
                    j2 = 0;
                    i2++;
                }
            }

            return true;
        }

        /// <summary>
        /// Verifies data from an expected MAC.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="inputLength"></param>
        /// <param name="expectedMac"></param>
        public bool Authenticate(Span<byte> input, uint inputLength, Span<byte> expectedMac)
        {
            Span<byte> outputMac = new byte[0x10];
            TFIT_wbaes_cmac_iCMAC(MacKey, input, inputLength, outputMac, 0x10);

            return outputMac.SequenceEqual(expectedMac.Slice(0, 0x10));
        }

        /****************************************
         * 
         * TFIT Libary
         * Everything is straight from dissasembly
         * This is all unrolled and mostly raw AES, mostly to refer more easily within IDA - order of operations preserved too.
         ****************************************/

        /// <summary>
        /// Decrypts data (White-Box AES CBC)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="size"></param>
        /// <param name="iv"></param>
        /// <param name="output"></param>
        public void TFIT_wbaes_cbc_decrypt(Span<byte> key, Span<byte> input, uint size, Span<byte> iv, Span<byte> output)
        {
            uint nBlocks = size / BLOCK_LEN;

            Span<byte> inBlocks = new byte[32];
            Span<byte> currentIv = iv;

            if ((size % 0x10) == 0 && nBlocks != 0)
            {
                Span<int> decryptedBlock = new int[4];
                for (int i = 0; i < nBlocks; i++)
                {
                    // memcpy(&inBlock[16 * (i & 1)], &pInput[16 * i], 0x10ui64);
                    input.Slice(i * BLOCK_LEN, BLOCK_LEN).CopyTo(inBlocks.Slice(BLOCK_LEN * (i & 1)));

                    // Decrypt current block using key
                    TFIT_op_iAES3(MemoryMarshal.Cast<byte, int>(key),
                        MemoryMarshal.Cast<byte, int>(inBlocks.Slice(BLOCK_LEN * (i & 1))),
                        decryptedBlock);

                    // Apply xor from custom key
                    var decryptedBlockAsBytes = MemoryMarshal.Cast<int, byte>(decryptedBlock);
                    block_xor(decryptedBlockAsBytes, currentIv, output.Slice(i * BLOCK_LEN));

                    currentIv = inBlocks.Slice(BLOCK_LEN * (i & 1), BLOCK_LEN);
                }

                // Report current key to caller
                currentIv.CopyTo(iv);
            }
        }

        /// <summary>
        /// Encrypts data (White-Box AES CBC)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="size"></param>
        /// <param name="iv"></param>
        /// <param name="output"></param>
        public void TFIT_wbaes_cbc_encrypt(Span<byte> key, Span<byte> input, uint size, Span<byte> iv, Span<byte> output)
        {
            const int blockSize = 0x10;

            uint nBlocks = size / blockSize;

            Span<byte> currentBlock = new byte[16];
            Span<byte> currentIv = iv;

            if ((size % 0x10) == 0 && nBlocks != 0)
            {
                for (int i = 0; i < nBlocks; i++)
                {
                    block_xor(input.Slice(i * 0x10), currentIv, currentBlock);

                    // Decrypt current block using key
                    TFIT_op_iAES4(MemoryMarshal.Cast<byte, int>(key),
                        MemoryMarshal.Cast<byte, int>(currentBlock),
                        MemoryMarshal.Cast<byte, int>(output.Slice(i * 0x10)));

                    // Apply xor from custom key
                    currentIv = output.Slice(i * 0x10);
                }
            }
        }

        /// <summary>
        /// Performs block decryption
        /// </summary>
        /// <param name="key"></param>
        /// <param name="blk"></param>
        /// <param name="output"></param>
        private void TFIT_op_iAES3(Span<int> key, Span<int> blk, Span<int> output)
        {
            /* LOCATIONS WILL CHANGE WITH UPDATES! NEED TO BE MANUALLY ADJUSTED in INDICES files! */

            int[][] r = inv_idcx[0];
            // TFIT_rInv

            int v1_1 = inv_sbox[r[0][0]][(blk[0] >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(blk[0] >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(blk[0] >> 8) & 0xFF] ^ inv_sbox[r[0][3]][blk[0] & 0xFF] ^ key[4];
            int v1_2 = inv_sbox[r[1][0]][(blk[1] >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(blk[1] >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(blk[1] >> 8) & 0xFF] ^ inv_sbox[r[1][3]][blk[1] & 0xFF] ^ key[5];
            int v1_3 = inv_sbox[r[2][0]][(blk[2] >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(blk[2] >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(blk[2] >> 8) & 0xFF] ^ inv_sbox[r[2][3]][blk[2] & 0xFF] ^ key[6];
            int v1_4 = inv_sbox[r[3][0]][(blk[3] >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(blk[3] >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(blk[3] >> 8) & 0xFF] ^ inv_sbox[r[3][3]][blk[3] & 0xFF] ^ key[7];

            // TFIT_r
            r = inv_idcx[1];
            int v2_1 = inv_sbox[r[0][0]][(v1_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v1_1 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v1_1 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v1_1 & 0xFF] ^ key[8];
            int v2_2 = inv_sbox[r[1][0]][(v1_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v1_2 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v1_2 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v1_2 & 0xFF] ^ key[9];
            int v2_3 = inv_sbox[r[2][0]][(v1_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v1_3 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v1_3 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v1_3 & 0xFF] ^ key[10];
            int v2_4 = inv_sbox[r[3][0]][(v1_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v1_4 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v1_4 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v1_4 & 0xFF] ^ key[11];

            // Next rounds are different
            // TFIT_rij
            r = inv_idcx[2];
            int v3_4 = inv_sbox[r[0][0]][(v2_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v2_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v2_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v2_4 & 0xFF] ^ key[15];
            int v3_1 = inv_sbox[r[1][0]][(v2_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v2_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v2_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v2_1 & 0xFF] ^ key[12];
            int v3_2 = inv_sbox[r[2][0]][(v2_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v2_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v2_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v2_2 & 0xFF] ^ key[13];
            int v3_3 = inv_sbox[r[3][0]][(v2_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v2_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v2_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v2_3 & 0xFF] ^ key[14];

            r = inv_idcx[3];
            int v4_4 = inv_sbox[r[0][0]][(v3_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v3_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v3_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v3_4 & 0xFF] ^ key[19];
            int v4_1 = inv_sbox[r[1][0]][(v3_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v3_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v3_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v3_1 & 0xFF] ^ key[16];
            int v4_2 = inv_sbox[r[2][0]][(v3_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v3_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v3_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v3_2 & 0xFF] ^ key[17];
            int v4_3 = inv_sbox[r[3][0]][(v3_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v3_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v3_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v3_3 & 0xFF] ^ key[18];

            r = inv_idcx[4];
            int v5_4 = inv_sbox[r[0][0]][(v4_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v4_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v4_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v4_4 & 0xFF] ^ key[23];
            int v5_1 = inv_sbox[r[1][0]][(v4_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v4_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v4_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v4_1 & 0xFF] ^ key[20];
            int v5_2 = inv_sbox[r[2][0]][(v4_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v4_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v4_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v4_2 & 0xFF] ^ key[21];
            int v5_3 = inv_sbox[r[3][0]][(v4_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v4_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v4_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v4_3 & 0xFF] ^ key[22];

            r = inv_idcx[5];
            int v6_4 = inv_sbox[r[0][0]][(v5_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v5_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v5_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v5_4 & 0xFF] ^ key[27];
            int v6_1 = inv_sbox[r[1][0]][(v5_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v5_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v5_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v5_1 & 0xFF] ^ key[24];
            int v6_2 = inv_sbox[r[2][0]][(v5_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v5_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v5_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v5_2 & 0xFF] ^ key[25];
            int v6_3 = inv_sbox[r[3][0]][(v5_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v5_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v5_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v5_3 & 0xFF] ^ key[26];

            r = inv_idcx[6];
            int v7_4 = inv_sbox[r[0][0]][(v6_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v6_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v6_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v6_4 & 0xFF] ^ key[31];
            int v7_1 = inv_sbox[r[1][0]][(v6_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v6_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v6_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v6_1 & 0xFF] ^ key[28];
            int v7_2 = inv_sbox[r[2][0]][(v6_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v6_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v6_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v6_2 & 0xFF] ^ key[29];
            int v7_3 = inv_sbox[r[3][0]][(v6_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v6_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v6_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v6_3 & 0xFF] ^ key[30];

            r = inv_idcx[7];
            int v8_4 = inv_sbox[r[0][0]][(v7_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v7_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v7_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v7_4 & 0xFF] ^ key[35];
            int v8_1 = inv_sbox[r[1][0]][(v7_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v7_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v7_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v7_1 & 0xFF] ^ key[32];
            int v8_2 = inv_sbox[r[2][0]][(v7_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v7_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v7_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v7_2 & 0xFF] ^ key[33];
            int v8_3 = inv_sbox[r[3][0]][(v7_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v7_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v7_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v7_3 & 0xFF] ^ key[34];

            r = inv_idcx[8];
            int v9_4 = inv_sbox[r[0][0]][(v8_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v8_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v8_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v8_4 & 0xFF] ^ key[39];
            int v9_1 = inv_sbox[r[1][0]][(v8_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v8_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v8_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v8_1 & 0xFF] ^ key[36];
            int v9_2 = inv_sbox[r[2][0]][(v8_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v8_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v8_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v8_2 & 0xFF] ^ key[37];
            int v9_3 = inv_sbox[r[3][0]][(v8_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v8_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v8_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v8_3 & 0xFF] ^ key[38];

            r = inv_idcx[9];
            int v10_4 = inv_sbox[r[0][0]][(v9_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v9_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v9_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v9_4 & 0xFF] ^ key[43];
            int v10_1 = inv_sbox[r[1][0]][(v9_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v9_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v9_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v9_1 & 0xFF] ^ key[40];
            int v10_2 = inv_sbox[r[2][0]][(v9_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v9_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v9_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v9_2 & 0xFF] ^ key[41];
            int v10_3 = inv_sbox[r[3][0]][(v9_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v9_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v9_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v9_3 & 0xFF] ^ key[42];

            r = inv_idcx[10];
            int v11_4 = inv_sbox[r[0][0]][(v10_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v10_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v10_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v10_4 & 0xFF] ^ key[47];
            int v11_1 = inv_sbox[r[1][0]][(v10_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v10_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v10_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v10_1 & 0xFF] ^ key[44];
            int v11_2 = inv_sbox[r[2][0]][(v10_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v10_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v10_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v10_2 & 0xFF] ^ key[45];
            int v11_3 = inv_sbox[r[3][0]][(v10_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v10_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v10_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v10_3 & 0xFF] ^ key[46];

            r = inv_idcx[11];
            int v12_4 = inv_sbox[r[0][0]][(v11_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v11_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v11_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v11_4 & 0xFF] ^ key[51];
            int v12_1 = inv_sbox[r[1][0]][(v11_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v11_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v11_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v11_1 & 0xFF] ^ key[48];
            int v12_2 = inv_sbox[r[2][0]][(v11_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v11_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v11_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v11_2 & 0xFF] ^ key[49];
            int v12_3 = inv_sbox[r[3][0]][(v11_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v11_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v11_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v11_3 & 0xFF] ^ key[50];

            r = inv_idcx[12];
            int v13_4 = inv_sbox[r[0][0]][(v12_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v12_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v12_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v12_4 & 0xFF] ^ key[55];
            int v13_1 = inv_sbox[r[1][0]][(v12_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v12_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v12_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v12_1 & 0xFF] ^ key[52];
            int v13_2 = inv_sbox[r[2][0]][(v12_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v12_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v12_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v12_2 & 0xFF] ^ key[53];
            int v13_3 = inv_sbox[r[3][0]][(v12_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v12_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v12_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v12_3 & 0xFF] ^ key[54];

            r = inv_idcx[13];
            int v14_4 = inv_sbox[r[0][0]][(v13_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v13_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v13_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v13_4 & 0xFF] ^ key[59];
            int v14_1 = inv_sbox[r[1][0]][(v13_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v13_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v13_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v13_1 & 0xFF] ^ key[56];
            int v14_2 = inv_sbox[r[2][0]][(v13_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v13_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v13_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v13_2 & 0xFF] ^ key[57];
            int v14_3 = inv_sbox[r[3][0]][(v13_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v13_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v13_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v13_3 & 0xFF] ^ key[58];

            r = inv_idcx[14];
            int v15_4 = inv_sbox[r[0][0]][(v14_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v14_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v14_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v14_4 & 0xFF] ^ key[63];
            int v15_1 = inv_sbox[r[1][0]][(v14_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v14_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v14_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v14_1 & 0xFF] ^ key[60];
            int v15_2 = inv_sbox[r[2][0]][(v14_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v14_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v14_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v14_2 & 0xFF] ^ key[61];
            int v15_3 = inv_sbox[r[3][0]][(v14_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v14_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v14_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v14_3 & 0xFF] ^ key[62];

            r = inv_idcx[15];
            int v16_4 = inv_sbox[r[0][0]][(v15_1 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v15_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v15_3 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v15_4 & 0xFF] ^ key[67];
            int v16_1 = inv_sbox[r[1][0]][(v15_2 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v15_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v15_4 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v15_1 & 0xFF] ^ key[64];
            int v16_2 = inv_sbox[r[2][0]][(v15_3 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v15_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v15_1 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v15_2 & 0xFF] ^ key[65];
            int v16_3 = inv_sbox[r[3][0]][(v15_4 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v15_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v15_2 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v15_3 & 0xFF] ^ key[66];

            // Final block is back to normal
            r = inv_idcx[16];
            int v17_2 = inv_sbox[r[0][0]][(v16_2 >> 24) & 0xFF] ^ inv_sbox[r[0][1]][(v16_2 >> 16) & 0xFF] ^ inv_sbox[r[0][2]][(v16_2 >> 8) & 0xFF] ^ inv_sbox[r[0][3]][v16_2 & 0xFF] ^ key[69];
            int v17_3 = inv_sbox[r[1][0]][(v16_3 >> 24) & 0xFF] ^ inv_sbox[r[1][1]][(v16_3 >> 16) & 0xFF] ^ inv_sbox[r[1][2]][(v16_3 >> 8) & 0xFF] ^ inv_sbox[r[1][3]][v16_3 & 0xFF] ^ key[70];
            int v17_4 = inv_sbox[r[2][0]][(v16_4 >> 24) & 0xFF] ^ inv_sbox[r[2][1]][(v16_4 >> 16) & 0xFF] ^ inv_sbox[r[2][2]][(v16_4 >> 8) & 0xFF] ^ inv_sbox[r[2][3]][v16_4 & 0xFF] ^ key[71];
            int v17_1 = inv_sbox[r[3][0]][(v16_1 >> 24) & 0xFF] ^ inv_sbox[r[3][1]][(v16_1 >> 16) & 0xFF] ^ inv_sbox[r[3][2]][(v16_1 >> 8) & 0xFF] ^ inv_sbox[r[3][3]][v16_1 & 0xFF] ^ key[68];

            output[0] = v17_1;
            output[1] = v17_2;
            output[2] = v17_3;
            output[3] = v17_4;
        }

        /// <summary>
        /// Performs block encryption (Unimplemented)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="blk"></param>
        /// <param name="output"></param>
        private void TFIT_op_iAES4(Span<int> key, Span<int> blk, Span<int> output)
        {
            /* LOCATIONS WILL CHANGE WITH UPDATES! NEED TO BE MANUALLY ADJUSTED in INDICES files! */

            int[][] r = idcx[0];
            // TFIT_rInv

            int v1_1 = sbox[r[0][0]][(blk[0] >> 24) & 0xFF] ^ sbox[r[0][1]][(blk[0] >> 16) & 0xFF] ^ sbox[r[0][2]][(blk[0] >> 8) & 0xFF] ^ sbox[r[0][3]][blk[0] & 0xFF] ^ key[4];
            int v1_2 = sbox[r[1][0]][(blk[1] >> 24) & 0xFF] ^ sbox[r[1][1]][(blk[1] >> 16) & 0xFF] ^ sbox[r[1][2]][(blk[1] >> 8) & 0xFF] ^ sbox[r[1][3]][blk[1] & 0xFF] ^ key[5];
            int v1_3 = sbox[r[2][0]][(blk[2] >> 24) & 0xFF] ^ sbox[r[2][1]][(blk[2] >> 16) & 0xFF] ^ sbox[r[2][2]][(blk[2] >> 8) & 0xFF] ^ sbox[r[2][3]][blk[2] & 0xFF] ^ key[6];
            int v1_4 = sbox[r[3][0]][(blk[3] >> 24) & 0xFF] ^ sbox[r[3][1]][(blk[3] >> 16) & 0xFF] ^ sbox[r[3][2]][(blk[3] >> 8) & 0xFF] ^ sbox[r[3][3]][blk[3] & 0xFF] ^ key[7];

            // TFIT_r
            r = idcx[1];
            int v2_1 = sbox[r[0][0]][(v1_1 >> 24) & 0xFF] ^ sbox[r[0][1]][(v1_1 >> 16) & 0xFF] ^ sbox[r[0][2]][(v1_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v1_1 & 0xFF] ^ key[8];
            int v2_2 = sbox[r[1][0]][(v1_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v1_2 >> 16) & 0xFF] ^ sbox[r[1][2]][(v1_2 >> 8) & 0xFF] ^ sbox[r[1][3]][v1_2 & 0xFF] ^ key[9];
            int v2_3 = sbox[r[2][0]][(v1_3 >> 24) & 0xFF] ^ sbox[r[2][1]][(v1_3 >> 16) & 0xFF] ^ sbox[r[2][2]][(v1_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v1_3 & 0xFF] ^ key[10];
            int v2_4 = sbox[r[3][0]][(v1_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v1_4 >> 16) & 0xFF] ^ sbox[r[3][2]][(v1_4 >> 8) & 0xFF] ^ sbox[r[3][3]][v1_4 & 0xFF] ^ key[11];

            // Next rounds are different
            // TFIT_rij
            r = idcx[2];
            int v3_4 = sbox[r[0][0]][(v2_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v2_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v2_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v2_4 & 0xFF] ^ key[15];
            int v3_3 = sbox[r[1][0]][(v2_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v2_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v2_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v2_3 & 0xFF] ^ key[14];
            int v3_2 = sbox[r[2][0]][(v2_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v2_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v2_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v2_2 & 0xFF] ^ key[13];
            int v3_1 = sbox[r[3][0]][(v2_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v2_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v2_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v2_1 & 0xFF] ^ key[12];

            r = idcx[3];
            int v4_4 = sbox[r[0][0]][(v3_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v3_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v3_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v3_4 & 0xFF] ^ key[19];
            int v4_3 = sbox[r[1][0]][(v3_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v3_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v3_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v3_3 & 0xFF] ^ key[18];
            int v4_2 = sbox[r[2][0]][(v3_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v3_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v3_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v3_2 & 0xFF] ^ key[17];
            int v4_1 = sbox[r[3][0]][(v3_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v3_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v3_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v3_1 & 0xFF] ^ key[16];

            r = idcx[4];
            int v5_4 = sbox[r[0][0]][(v4_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v4_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v4_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v4_4 & 0xFF] ^ key[23];
            int v5_3 = sbox[r[1][0]][(v4_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v4_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v4_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v4_3 & 0xFF] ^ key[22];
            int v5_2 = sbox[r[2][0]][(v4_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v4_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v4_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v4_2 & 0xFF] ^ key[21];
            int v5_1 = sbox[r[3][0]][(v4_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v4_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v4_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v4_1 & 0xFF] ^ key[20];

            r = idcx[5];
            int v6_4 = sbox[r[0][0]][(v5_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v5_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v5_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v5_4 & 0xFF] ^ key[27];
            int v6_3 = sbox[r[1][0]][(v5_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v5_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v5_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v5_3 & 0xFF] ^ key[26];
            int v6_2 = sbox[r[2][0]][(v5_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v5_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v5_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v5_2 & 0xFF] ^ key[25];
            int v6_1 = sbox[r[3][0]][(v5_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v5_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v5_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v5_1 & 0xFF] ^ key[24];

            r = idcx[6];
            int v7_4 = sbox[r[0][0]][(v6_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v6_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v6_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v6_4 & 0xFF] ^ key[31];
            int v7_3 = sbox[r[1][0]][(v6_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v6_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v6_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v6_3 & 0xFF] ^ key[30];
            int v7_2 = sbox[r[2][0]][(v6_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v6_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v6_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v6_2 & 0xFF] ^ key[29];
            int v7_1 = sbox[r[3][0]][(v6_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v6_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v6_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v6_1 & 0xFF] ^ key[28];

            r = idcx[7];
            int v8_4 = sbox[r[0][0]][(v7_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v7_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v7_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v7_4 & 0xFF] ^ key[35];
            int v8_3 = sbox[r[1][0]][(v7_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v7_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v7_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v7_3 & 0xFF] ^ key[34];
            int v8_2 = sbox[r[2][0]][(v7_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v7_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v7_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v7_2 & 0xFF] ^ key[33];
            int v8_1 = sbox[r[3][0]][(v7_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v7_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v7_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v7_1 & 0xFF] ^ key[32];

            r = idcx[8];
            int v9_4 = sbox[r[0][0]][(v8_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v8_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v8_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v8_4 & 0xFF] ^ key[39];
            int v9_3 = sbox[r[1][0]][(v8_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v8_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v8_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v8_3 & 0xFF] ^ key[38];
            int v9_2 = sbox[r[2][0]][(v8_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v8_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v8_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v8_2 & 0xFF] ^ key[37];
            int v9_1 = sbox[r[3][0]][(v8_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v8_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v8_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v8_1 & 0xFF] ^ key[36];

            r = idcx[9];
            int v10_4 = sbox[r[0][0]][(v9_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v9_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v9_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v9_4 & 0xFF] ^ key[43];
            int v10_3 = sbox[r[1][0]][(v9_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v9_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v9_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v9_3 & 0xFF] ^ key[42];
            int v10_2 = sbox[r[2][0]][(v9_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v9_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v9_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v9_2 & 0xFF] ^ key[41];
            int v10_1 = sbox[r[3][0]][(v9_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v9_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v9_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v9_1 & 0xFF] ^ key[40];

            r = idcx[10];
            int v11_4 = sbox[r[0][0]][(v10_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v10_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v10_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v10_4 & 0xFF] ^ key[47];
            int v11_3 = sbox[r[1][0]][(v10_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v10_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v10_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v10_3 & 0xFF] ^ key[46];
            int v11_2 = sbox[r[2][0]][(v10_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v10_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v10_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v10_2 & 0xFF] ^ key[45];
            int v11_1 = sbox[r[3][0]][(v10_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v10_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v10_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v10_1 & 0xFF] ^ key[44];

            r = idcx[11];
            int v12_4 = sbox[r[0][0]][(v11_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v11_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v11_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v11_4 & 0xFF] ^ key[51];
            int v12_3 = sbox[r[1][0]][(v11_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v11_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v11_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v11_3 & 0xFF] ^ key[50];
            int v12_2 = sbox[r[2][0]][(v11_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v11_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v11_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v11_2 & 0xFF] ^ key[49];
            int v12_1 = sbox[r[3][0]][(v11_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v11_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v11_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v11_1 & 0xFF] ^ key[48];

            r = idcx[12];
            int v13_4 = sbox[r[0][0]][(v12_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v12_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v12_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v12_4 & 0xFF] ^ key[55];
            int v13_3 = sbox[r[1][0]][(v12_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v12_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v12_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v12_3 & 0xFF] ^ key[54];
            int v13_2 = sbox[r[2][0]][(v12_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v12_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v12_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v12_2 & 0xFF] ^ key[53];
            int v13_1 = sbox[r[3][0]][(v12_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v12_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v12_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v12_1 & 0xFF] ^ key[52];

            r = idcx[13];
            int v14_4 = sbox[r[0][0]][(v13_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v13_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v13_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v13_4 & 0xFF] ^ key[59];
            int v14_3 = sbox[r[1][0]][(v13_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v13_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v13_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v13_3 & 0xFF] ^ key[58];
            int v14_2 = sbox[r[2][0]][(v13_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v13_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v13_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v13_2 & 0xFF] ^ key[57];
            int v14_1 = sbox[r[3][0]][(v13_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v13_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v13_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v13_1 & 0xFF] ^ key[56];

            r = idcx[14];
            int v15_4 = sbox[r[0][0]][(v14_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v14_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v14_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v14_4 & 0xFF] ^ key[63];
            int v15_3 = sbox[r[1][0]][(v14_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v14_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v14_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v14_3 & 0xFF] ^ key[62];
            int v15_2 = sbox[r[2][0]][(v14_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v14_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v14_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v14_2 & 0xFF] ^ key[61];
            int v15_1 = sbox[r[3][0]][(v14_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v14_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v14_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v14_1 & 0xFF] ^ key[60];

            r = idcx[15];
            int v16_4 = sbox[r[0][0]][(v15_3 >> 24) & 0xFF] ^ sbox[r[0][1]][(v15_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v15_1 >> 8) & 0xFF] ^ sbox[r[0][3]][v15_4 & 0xFF] ^ key[67];
            int v16_3 = sbox[r[1][0]][(v15_2 >> 24) & 0xFF] ^ sbox[r[1][1]][(v15_1 >> 16) & 0xFF] ^ sbox[r[1][2]][(v15_4 >> 8) & 0xFF] ^ sbox[r[1][3]][v15_3 & 0xFF] ^ key[66];
            int v16_2 = sbox[r[2][0]][(v15_1 >> 24) & 0xFF] ^ sbox[r[2][1]][(v15_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v15_3 >> 8) & 0xFF] ^ sbox[r[2][3]][v15_2 & 0xFF] ^ key[65];
            int v16_1 = sbox[r[3][0]][(v15_4 >> 24) & 0xFF] ^ sbox[r[3][1]][(v15_3 >> 16) & 0xFF] ^ sbox[r[3][2]][(v15_2 >> 8) & 0xFF] ^ sbox[r[3][3]][v15_1 & 0xFF] ^ key[64];

            // Final block is back to normal
            r = idcx[16];
            int v17_2 = sbox[r[0][0]][(v16_2 >> 24) & 0xFF] ^ sbox[r[0][1]][(v16_2 >> 16) & 0xFF] ^ sbox[r[0][2]][(v16_2 >> 8) & 0xFF] ^ sbox[r[0][3]][v16_2 & 0xFF] ^ key[69];
            int v17_3 = sbox[r[1][0]][(v16_3 >> 24) & 0xFF] ^ sbox[r[1][1]][(v16_3 >> 16) & 0xFF] ^ sbox[r[1][2]][(v16_3 >> 8) & 0xFF] ^ sbox[r[1][3]][v16_3 & 0xFF] ^ key[70];
            int v17_4 = sbox[r[2][0]][(v16_4 >> 24) & 0xFF] ^ sbox[r[2][1]][(v16_4 >> 16) & 0xFF] ^ sbox[r[2][2]][(v16_4 >> 8) & 0xFF] ^ sbox[r[2][3]][v16_4 & 0xFF] ^ key[71];
            int v17_1 = sbox[r[3][0]][(v16_1 >> 24) & 0xFF] ^ sbox[r[3][1]][(v16_1 >> 16) & 0xFF] ^ sbox[r[3][2]][(v16_1 >> 8) & 0xFF] ^ sbox[r[3][3]][v16_1 & 0xFF] ^ key[68];

            output[0] = v17_1;
            output[1] = v17_2;
            output[2] = v17_3;
            output[3] = v17_4;
        }

        /* Content Integrity (MAC Code subroutines) */
        private static int[] _empty_block = new int[4];

        /// <summary>
        /// Performs mac computation
        /// </summary>
        /// <param name="key"></param>
        /// <param name="input"></param>
        /// <param name="size"></param>
        /// <param name="outputHash"></param>
        /// <param name="blockSize">Must be 0x10</param>
        private void TFIT_wbaes_cmac_iCMAC(Span<byte> key, Span<byte> input, uint size, Span<byte> outputHash, int blockSize)
        {
            uint blockCount = size / BLOCK_LEN;
            uint blockRem = size % BLOCK_LEN;

            bool isMultipleOfblockSize = size >= BLOCK_LEN && size % BLOCK_LEN == 0;
            uint actualBlockCount = blockCount;
            if (!isMultipleOfblockSize)
                actualBlockCount++; // extra one

            byte[] tmpInput = new byte[BLOCK_LEN];
            byte[] currentHash = new byte[BLOCK_LEN];

            if (!key.IsEmpty && (!input.IsEmpty || size != 0) && !outputHash.IsEmpty)
            {
                if (blockSize != 0 && blockSize <= BLOCK_LEN)
                {
                    Span<byte> tmp = stackalloc byte[BLOCK_LEN];
                    TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                        _empty_block,
                        MemoryMarshal.Cast<byte, int>(tmp));

                    Span<byte> shifted = stackalloc byte[BLOCK_LEN];
                    block_lshift(tmp, shifted);

                    if (blockCount > 0)
                    {
                        if (actualBlockCount == 1)
                        {
                            block_xor(input, shifted, tmpInput);
                            TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                                MemoryMarshal.Cast<byte, int>(tmpInput),
                                MemoryMarshal.Cast<byte, int>(currentHash));
                        }
                        else
                        {
                            TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                                MemoryMarshal.Cast<byte, int>(input),
                                MemoryMarshal.Cast<byte, int>(currentHash));
                        }
                    }
                    else
                    {
                        Memcpy(tmpInput, input, (int)blockRem);
                        tmpInput[blockRem] = 0x80;
                        if (blockRem < BLOCK_LEN - 1)
                            tmpInput.AsSpan((int)blockRem + 1).Fill(0);

                        Span<byte> r = stackalloc byte[BLOCK_LEN];
                        block_lshift(shifted, r);
                        block_xor(tmpInput, r, tmpInput);
                        TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                                MemoryMarshal.Cast<byte, int>(tmpInput),
                                MemoryMarshal.Cast<byte, int>(currentHash));
                    }

                    Span<byte> tmpBlk = stackalloc byte[BLOCK_LEN];
                    for (var i = 1; i < actualBlockCount; i++)
                    {
                        if (i >= actualBlockCount - 1)
                        {
                            if (blockRem != 0)
                            {
                                for (var j = 0; j < blockRem; j++)
                                    tmpInput[j] = (byte)(currentHash[j] ^ input[BLOCK_LEN * i + j]);
                                tmpInput[blockRem] = (byte)(currentHash[blockRem] ^ 0x80);

                                if (blockRem < 0x0F)
                                {
                                    // memcpy(tmpInput + (blockRem + 1), currentHash + (blockRem + 1), 16 - blockRem - 1);
                                    int cpySize = 0x10 - (int)blockRem - 1;
                                    Memcpy(tmpInput.AsSpan((int)blockRem + 1), currentHash.AsSpan((int)blockRem + 1), cpySize);
                                }

                                tmpBlk.Clear();
                                block_lshift(shifted, tmpBlk);
                                block_xor(tmpInput, tmpBlk, tmpInput);
                                TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                                    MemoryMarshal.Cast<byte, int>(tmpInput),
                                    MemoryMarshal.Cast<byte, int>(currentHash));
                            }
                            else
                            {
                                block_xor(input.Slice(i * BLOCK_LEN), currentHash, tmpInput);
                                block_xor(tmpInput, shifted, tmpInput);
                                TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                                    MemoryMarshal.Cast<byte, int>(tmpInput),
                                    MemoryMarshal.Cast<byte, int>(currentHash));
                            }
                        }
                        else
                        {
                            block_xor(input.Slice(i * BLOCK_LEN), currentHash, tmpInput);
                            TFIT_op_iCMAC(MemoryMarshal.Cast<byte, int>(key),
                                MemoryMarshal.Cast<byte, int>(tmpInput),
                                MemoryMarshal.Cast<byte, int>(currentHash));
                        }
                    }

                    Memcpy(outputHash, currentHash, blockSize);
                }
            }

        }

        private void block_lshift(Span<byte> input, Span<byte> output)
        {
            int bits = 0;
            if ((input[0] >> 7) != 0)
                bits = 0x87;

            output[15] = (byte)(bits ^ (input[15] << 1));
            for (int i = 14; i >= 0; --i)
                output[i] = (byte)((input[i + 1] >> 7) | (input[i] << 1));
        }

        private void block_xor(Span<byte> inputBlock, Span<byte> xorKeyBytes, Span<byte> outputBlock)
        {
            Span<long> inputBlockLong = MemoryMarshal.Cast<byte, long>(inputBlock);
            Span<long> xorKeyBytesLong = MemoryMarshal.Cast<byte, long>(xorKeyBytes);
            Span<long> outputBlockLong = MemoryMarshal.Cast<byte, long>(outputBlock);

            outputBlockLong[0] = xorKeyBytesLong[0] ^ inputBlockLong[0];
            outputBlockLong[1] = xorKeyBytesLong[1] ^ inputBlockLong[1];
        }

        private void TFIT_op_iCMAC(Span<int> key, Span<int> blk, Span<int> output)
        {
            int[][] r = vidcx[0];
            // TFIT_rInv

            int v1_1 = vsbox[r[0][0]][(blk[0] >> 24) & 0xFF] ^ vsbox[r[0][1]][(blk[0] >> 16) & 0xFF] ^ vsbox[r[0][2]][(blk[0] >> 8) & 0xFF] ^ vsbox[r[0][3]][blk[0] & 0xFF] ^ key[4];
            int v1_2 = vsbox[r[1][0]][(blk[1] >> 24) & 0xFF] ^ vsbox[r[1][1]][(blk[1] >> 16) & 0xFF] ^ vsbox[r[1][2]][(blk[1] >> 8) & 0xFF] ^ vsbox[r[1][3]][blk[1] & 0xFF] ^ key[5];
            int v1_3 = vsbox[r[2][0]][(blk[2] >> 24) & 0xFF] ^ vsbox[r[2][1]][(blk[2] >> 16) & 0xFF] ^ vsbox[r[2][2]][(blk[2] >> 8) & 0xFF] ^ vsbox[r[2][3]][blk[2] & 0xFF] ^ key[6];
            int v1_4 = vsbox[r[3][0]][(blk[3] >> 24) & 0xFF] ^ vsbox[r[3][1]][(blk[3] >> 16) & 0xFF] ^ vsbox[r[3][2]][(blk[3] >> 8) & 0xFF] ^ vsbox[r[3][3]][blk[3] & 0xFF] ^ key[7];

            // TFIT_r
            r = vidcx[1];
            int v2_1 = vsbox[r[0][0]][(v1_1 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v1_1 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v1_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v1_1 & 0xFF] ^ key[8];
            int v2_2 = vsbox[r[1][0]][(v1_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v1_2 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v1_2 >> 8) & 0xFF] ^ vsbox[r[1][3]][v1_2 & 0xFF] ^ key[9];
            int v2_3 = vsbox[r[2][0]][(v1_3 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v1_3 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v1_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v1_3 & 0xFF] ^ key[10];
            int v2_4 = vsbox[r[3][0]][(v1_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v1_4 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v1_4 >> 8) & 0xFF] ^ vsbox[r[3][3]][v1_4 & 0xFF] ^ key[11];

            // Next rounds are different
            // TFIT_rij
            r = vidcx[2];
            int v3_4 = vsbox[r[0][0]][(v2_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v2_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v2_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v2_4 & 0xFF] ^ key[15];
            int v3_3 = vsbox[r[1][0]][(v2_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v2_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v2_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v2_3 & 0xFF] ^ key[14];
            int v3_2 = vsbox[r[2][0]][(v2_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v2_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v2_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v2_2 & 0xFF] ^ key[13];
            int v3_1 = vsbox[r[3][0]][(v2_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v2_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v2_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v2_1 & 0xFF] ^ key[12];

            r = vidcx[3];
            int v4_4 = vsbox[r[0][0]][(v3_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v3_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v3_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v3_4 & 0xFF] ^ key[19];
            int v4_3 = vsbox[r[1][0]][(v3_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v3_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v3_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v3_3 & 0xFF] ^ key[18];
            int v4_2 = vsbox[r[2][0]][(v3_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v3_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v3_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v3_2 & 0xFF] ^ key[17];
            int v4_1 = vsbox[r[3][0]][(v3_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v3_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v3_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v3_1 & 0xFF] ^ key[16];

            r = vidcx[4];
            int v5_4 = vsbox[r[0][0]][(v4_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v4_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v4_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v4_4 & 0xFF] ^ key[23];
            int v5_3 = vsbox[r[1][0]][(v4_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v4_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v4_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v4_3 & 0xFF] ^ key[22];
            int v5_2 = vsbox[r[2][0]][(v4_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v4_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v4_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v4_2 & 0xFF] ^ key[21];
            int v5_1 = vsbox[r[3][0]][(v4_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v4_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v4_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v4_1 & 0xFF] ^ key[20];

            r = vidcx[5];
            int v6_4 = vsbox[r[0][0]][(v5_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v5_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v5_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v5_4 & 0xFF] ^ key[27];
            int v6_3 = vsbox[r[1][0]][(v5_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v5_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v5_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v5_3 & 0xFF] ^ key[26];
            int v6_2 = vsbox[r[2][0]][(v5_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v5_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v5_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v5_2 & 0xFF] ^ key[25];
            int v6_1 = vsbox[r[3][0]][(v5_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v5_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v5_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v5_1 & 0xFF] ^ key[24];

            r = vidcx[6];
            int v7_4 = vsbox[r[0][0]][(v6_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v6_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v6_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v6_4 & 0xFF] ^ key[31];
            int v7_3 = vsbox[r[1][0]][(v6_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v6_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v6_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v6_3 & 0xFF] ^ key[30];
            int v7_2 = vsbox[r[2][0]][(v6_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v6_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v6_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v6_2 & 0xFF] ^ key[29];
            int v7_1 = vsbox[r[3][0]][(v6_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v6_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v6_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v6_1 & 0xFF] ^ key[28];

            r = vidcx[7];
            int v8_4 = vsbox[r[0][0]][(v7_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v7_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v7_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v7_4 & 0xFF] ^ key[35];
            int v8_3 = vsbox[r[1][0]][(v7_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v7_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v7_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v7_3 & 0xFF] ^ key[34];
            int v8_2 = vsbox[r[2][0]][(v7_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v7_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v7_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v7_2 & 0xFF] ^ key[33];
            int v8_1 = vsbox[r[3][0]][(v7_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v7_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v7_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v7_1 & 0xFF] ^ key[32];

            r = vidcx[8];
            int v9_4 = vsbox[r[0][0]][(v8_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v8_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v8_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v8_4 & 0xFF] ^ key[39];
            int v9_3 = vsbox[r[1][0]][(v8_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v8_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v8_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v8_3 & 0xFF] ^ key[38];
            int v9_2 = vsbox[r[2][0]][(v8_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v8_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v8_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v8_2 & 0xFF] ^ key[37];
            int v9_1 = vsbox[r[3][0]][(v8_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v8_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v8_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v8_1 & 0xFF] ^ key[36];

            r = vidcx[9];
            int v10_4 = vsbox[r[0][0]][(v9_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v9_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v9_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v9_4 & 0xFF] ^ key[43];
            int v10_3 = vsbox[r[1][0]][(v9_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v9_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v9_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v9_3 & 0xFF] ^ key[42];
            int v10_2 = vsbox[r[2][0]][(v9_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v9_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v9_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v9_2 & 0xFF] ^ key[41];
            int v10_1 = vsbox[r[3][0]][(v9_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v9_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v9_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v9_1 & 0xFF] ^ key[40];

            r = vidcx[10];
            int v11_4 = vsbox[r[0][0]][(v10_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v10_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v10_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v10_4 & 0xFF] ^ key[47];
            int v11_3 = vsbox[r[1][0]][(v10_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v10_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v10_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v10_3 & 0xFF] ^ key[46];
            int v11_2 = vsbox[r[2][0]][(v10_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v10_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v10_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v10_2 & 0xFF] ^ key[45];
            int v11_1 = vsbox[r[3][0]][(v10_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v10_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v10_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v10_1 & 0xFF] ^ key[44];

            r = vidcx[11];
            int v12_4 = vsbox[r[0][0]][(v11_3 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v11_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v11_1 >> 8) & 0xFF] ^ vsbox[r[0][3]][v11_4 & 0xFF] ^ key[51];
            int v12_3 = vsbox[r[1][0]][(v11_2 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v11_1 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v11_4 >> 8) & 0xFF] ^ vsbox[r[1][3]][v11_3 & 0xFF] ^ key[50];
            int v12_2 = vsbox[r[2][0]][(v11_1 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v11_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v11_3 >> 8) & 0xFF] ^ vsbox[r[2][3]][v11_2 & 0xFF] ^ key[49];
            int v12_1 = vsbox[r[3][0]][(v11_4 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v11_3 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v11_2 >> 8) & 0xFF] ^ vsbox[r[3][3]][v11_1 & 0xFF] ^ key[48];

            r = vidcx[12];
            int v13_2 = vsbox[r[0][0]][(v12_2 >> 24) & 0xFF] ^ vsbox[r[0][1]][(v12_2 >> 16) & 0xFF] ^ vsbox[r[0][2]][(v12_2 >> 8) & 0xFF] ^ vsbox[r[0][3]][v12_2 & 0xFF] ^ key[53];
            int v13_3 = vsbox[r[1][0]][(v12_3 >> 24) & 0xFF] ^ vsbox[r[1][1]][(v12_3 >> 16) & 0xFF] ^ vsbox[r[1][2]][(v12_3 >> 8) & 0xFF] ^ vsbox[r[1][3]][v12_3 & 0xFF] ^ key[54];
            int v13_4 = vsbox[r[2][0]][(v12_4 >> 24) & 0xFF] ^ vsbox[r[2][1]][(v12_4 >> 16) & 0xFF] ^ vsbox[r[2][2]][(v12_4 >> 8) & 0xFF] ^ vsbox[r[2][3]][v12_4 & 0xFF] ^ key[55];
            int v13_1 = vsbox[r[3][0]][(v12_1 >> 24) & 0xFF] ^ vsbox[r[3][1]][(v12_1 >> 16) & 0xFF] ^ vsbox[r[3][2]][(v12_1 >> 8) & 0xFF] ^ vsbox[r[3][3]][v12_1 & 0xFF] ^ key[52];

            output[0] = v13_1;
            output[1] = v13_2;
            output[2] = v13_3;
            output[3] = v13_4;
        }

        private void Memcpy(Span<byte> output, Span<byte> input, int len)
        {
            input[..len].CopyTo(output[..len]);
        }
    }
}
