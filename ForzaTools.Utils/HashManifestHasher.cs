using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForzaTools.Utils
{
    internal class HashManifestHasher
    {
        static uint StringHashIter(string name, bool caseInsensitive, uint hash = 5381)
        {
            if (name is not null)
            {
                foreach (char c in name)
                {
                    char use_c = c;
                    if (caseInsensitive)
                        use_c = c.ToString().ToUpper()[0];
                    uint int_c = Convert.ToUInt32(c);
                    hash ^= int_c + (hash >> 2) + 32 * hash;
                }
            }
            return hash;
        }

        // Intended for HashManifest.xml
        // Input would be path to file, i.e media\Audio\ModularCars\VW_GolfGTI_83-Engine.xml
        public static ulong StringHash64(string name, bool caseInsensitive)
        {
            var hash = StringHashIter(name, caseInsensitive);
            return hash | Convert.ToUInt64(StringHashIter(name, caseInsensitive, hash)) << 32;
        }
    }
}
