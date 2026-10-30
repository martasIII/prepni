using System.Runtime.InteropServices;
using System.Text;

namespace Prepni;

/// <summary>Schránka přes Win32: text, kopie všech formátů a jejich vrácení.</summary>
internal static unsafe class Clipboard
{
    // Formáty, které nejsou data v paměti, ale ukazatele na objekty Windows (bitmapa, metasoubor…); kopírovat je
    // neumíme. Obrázek se přesto vrátí: aplikace ho dávají i jako CF_DIB, a ten je obyčejná data.
    private static readonly HashSet<uint> HandleFormats = [2, 3, 9, 14, 0x80, 0x82, 0x83, 0x8E];
    private const uint PrivateFormatsFirst = 0x200;
    private const uint PrivateFormatsLast = 0x3FF;
    private const int MaxFormatBytes = 64 * 1024 * 1024;

    /// <summary>Otevře schránku; když ji zrovna drží jiná aplikace, chvíli to zkouší.</summary>
    public static bool Open(nint owner)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (Native.OpenClipboard(owner))
                return true;
            Thread.Sleep(10);
        }

        return false;
    }

    /// <summary>Text ze schránky, nebo <c>null</c>. Schránka musí být otevřená.</summary>
    public static string? ReadText()
    {
        var handle = Native.GetClipboardData(Native.CF_UNICODETEXT);
        if (handle == 0)
            return null;

        var pointer = Native.GlobalLock(handle);
        if (pointer is null)
            return null;

        try
        {
            return Marshal.PtrToStringUni((nint)pointer);
        }
        finally
        {
            Native.GlobalUnlock(handle);
        }
    }

    /// <summary>Data pojmenovaného formátu jako UTF-8 text, nebo <c>null</c>. Schránka musí být otevřená.</summary>
    public static string? ReadNamed(string formatName)
    {
        var format = Native.RegisterClipboardFormatW(formatName);
        return Native.IsClipboardFormatAvailable(format) && ReadBytes(format) is { } bytes
            ? Encoding.UTF8.GetString(bytes).TrimEnd((char)0)
            : null;
    }

    public static bool Has(string formatName) =>
        Native.IsClipboardFormatAvailable(Native.RegisterClipboardFormatW(formatName));

    /// <summary>Vloží do schránky jen text.</summary>
    public static void WriteText(nint owner, string text)
    {
        if (!Open(owner))
            return;

        try
        {
            Native.EmptyClipboard();
            var bytes = new byte[(text.Length + 1) * 2];
            Encoding.Unicode.GetBytes(text, bytes);
            Write(Native.CF_UNICODETEXT, bytes);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public static byte[]? ReadBytes(uint format)
    {
        var handle = Native.GetClipboardData(format);
        if (handle == 0)
            return null;

        var size = Native.GlobalSize(handle);
        if (size == 0 || size > MaxFormatBytes)
            return null;

        var pointer = Native.GlobalLock(handle);
        if (pointer is null)
            return null;

        try
        {
            return new ReadOnlySpan<byte>(pointer, (int)size).ToArray();
        }
        finally
        {
            Native.GlobalUnlock(handle);
        }
    }

    public static void Write(uint format, byte[] data)
    {
        var handle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (nuint)data.Length);
        if (handle == 0)
            return;

        var pointer = Native.GlobalLock(handle);
        data.CopyTo(new Span<byte>(pointer, data.Length));
        Native.GlobalUnlock(handle);

        // Po úspěchu patří paměť schránce; jinak ji musíme uvolnit sami.
        if (Native.SetClipboardData(format, handle) == 0)
            Native.GlobalFree(handle);
    }

    public static bool CanCopy(uint format) =>
        !HandleFormats.Contains(format) && format is not (>= PrivateFormatsFirst and <= PrivateFormatsLast);
}

/// <summary>Kopie schránky, aby se dala po půjčení vrátit.</summary>
internal sealed class ClipboardSnapshot
{
    private readonly List<(uint Format, byte[] Data)>? _formats;

    private ClipboardSnapshot(List<(uint Format, byte[] Data)>? formats) => _formats = formats;

    public static ClipboardSnapshot Take(nint owner)
    {
        if (!Clipboard.Open(owner))
            return new ClipboardSnapshot(null);

        try
        {
            var formats = new List<(uint, byte[])>();
            for (var format = Native.EnumClipboardFormats(0); format != 0; format = Native.EnumClipboardFormats(format))
            {
                if (Clipboard.CanCopy(format) && Clipboard.ReadBytes(format) is { } data)
                    formats.Add((format, data));
            }

            return new ClipboardSnapshot(formats);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    public void Restore(nint owner)
    {
        // Schránku jsme nepřečetli (držela ji jiná aplikace); radši na ni nesahat.
        if (_formats is null || !Clipboard.Open(owner))
            return;

        try
        {
            Native.EmptyClipboard();
            foreach (var (format, data) in _formats)
                Clipboard.Write(format, data);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }
}
