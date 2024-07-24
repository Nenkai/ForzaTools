using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DurangoTypes;

using System.Runtime.InteropServices;

namespace ForzaTools.TexConv;

public partial class XGImports
{
    [LibraryImport("xg.dll", EntryPoint = "XGCreateTexture2DComputer")]
    public unsafe static partial int XGCreateTexture2DComputer(XG_TEXTURE2D_DESC* desc, XGTextureAddressComputer** computer);
}