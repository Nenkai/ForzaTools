using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.Utils
{
    public class StringDecrypt
    {
        public static string Decrypt0x26(byte[] data)
        {
            string s = "";
            for (var i = 0; i < data.Length; i++)
            {
                s += (char)(data[i] ^ (i + 0x26));
            }

            return s;
        }

        public static byte[] Encrypt0x26(string str)
        {
            byte[] output = new byte[str.Length];

            for (var i = 0; i < str.Length; i++)
            {
                output[i] = (byte)(str[i] ^ (i + 0x26));
            }

            return output;
        }

        public static string Decrypt0x75(byte[] data)
        {
            string s = "";
            for (var i = 0; i < data.Length; i++)
            {
                s += (char)(data[i] ^ (i + 0x75));
            }

            return s;
        }

        public static byte[] Encrypt0x75(string str)
        {
            byte[] output = new byte[str.Length];

            for (var i = 0; i < str.Length; i++)
            {
                output[i] = (byte)(str[i] ^ (i + 0x75));
            }

            return output;
        }

        public static byte[] EncryptedGameDbPath = new byte[]
        {
              0x41, 0x46, 0x45, 0x4C, 0x10, 0x77, 0x41, 0x48, 0x4A, 0x46, 0x51, 0x6D, 0x56, 0x51, 0x68, 0x52
        };
    }
}
