using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FoundationR
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ARGB
    {
        public byte a;
        public byte r;
        public byte g;
        public byte b;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct BGRA
    {
        public byte b;
        public byte g;
        public byte r;
        public byte a;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct RGB
    {
        public byte r;
        public byte g;
        public byte b;
    }
}
