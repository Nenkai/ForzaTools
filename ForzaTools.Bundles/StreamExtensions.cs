using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Syroot.BinaryData;

namespace ForzaTools.Bundles
{
    public static class StreamExtensions
    {
        public static void WriteMatrix4x4(this Stream stream, Matrix4x4 matrix)
        {
            stream.WriteSingle(matrix.M11);
            stream.WriteSingle(matrix.M12);
            stream.WriteSingle(matrix.M13);
            stream.WriteSingle(matrix.M14);

            stream.WriteSingle(matrix.M21);
            stream.WriteSingle(matrix.M22);
            stream.WriteSingle(matrix.M23);
            stream.WriteSingle(matrix.M24);

            stream.WriteSingle(matrix.M31);
            stream.WriteSingle(matrix.M32);
            stream.WriteSingle(matrix.M33);
            stream.WriteSingle(matrix.M34);

            stream.WriteSingle(matrix.M41);
            stream.WriteSingle(matrix.M42);
            stream.WriteSingle(matrix.M43);
            stream.WriteSingle(matrix.M44);
        }

        public static void WriteVector4(this Stream stream, Vector4 vec)
        {
            stream.WriteSingle(vec.X);
            stream.WriteSingle(vec.Y);
            stream.WriteSingle(vec.Z);
            stream.WriteSingle(vec.W);
        }
    }
}
