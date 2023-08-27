namespace ForzaTools.Utils
{
    /// <summary>
    /// From Reverse engineered code, used in .str files
    /// Also used in unstripped databases for string columns
    /// Normally two hashes appended, example:
    /// - Data_Car = 0x434455F2 (Table)
    /// - IDS_DisplayName_247 = 0x71A34BB8 (Localize Name)
    /// - 0x434455F271A34BB8 -> _&4847093598734470072 (converted decimal into string)
    /// 
    /// Usage example is: HashString("IDS_Mode1String_2990", 32, true);
    /// Seems to be always 32
    /// </summary>
    public class LocaleStringTableStringHasher
    {
        static long HashString(string str, int bitSize, bool caseInsensitive)
        {
            long v = (((long)1) << bitSize) - 1;
            long res = v;
            byte v7 = (byte)(bitSize - 7);

            for (var i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (!caseInsensitive && char.IsLetter(c) && char.IsUpper(c))
                    c = char.ToLower(c);

                res = v & (((c ^ res) << 7) | ((c ^ res) >> v7));
            }

            return res;
        }
    }
}