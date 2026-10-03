using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NetSeal.Services.Ras;

[StructLayout(LayoutKind.Sequential)]
public struct Luid
{
    public uint LowPart;
    public int HighPart;

    public override string ToString()
    {
        // 拼成 64 位无符号数
        ulong v = ((ulong)(uint)HighPart << 32) | LowPart;
        return $"0x{v:X16}";
    }

    public bool Equals(Luid other)
        => LowPart == other.LowPart && HighPart == other.HighPart;

    public override bool Equals(object? obj)
        => obj is Luid l && Equals(l);

    public override int GetHashCode()
        => HashCode.Combine(LowPart, HighPart);
}