using System.Collections;
using System.Runtime.InteropServices;

namespace Dolly.ImageLoader.Wpf.Comparers;

public sealed class NaturalStringComparer : IComparer<string>, IComparer
{
    public static NaturalStringComparer Instance { get; } = new();
    
    private NaturalStringComparer()
    {}

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string x, string y);

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x is null)
            return -1;

        if (y is null)
            return 1;

        return StrCmpLogicalW(x, y);
    }

    int IComparer.Compare(object? x, object? y)
    {
        return Compare(x as string, y as string);
    }
}