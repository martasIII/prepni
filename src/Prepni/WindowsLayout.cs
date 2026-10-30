using System.Globalization;
using Prepni.Core;

namespace Prepni;

/// <summary>Rozložení klávesnice nainstalované ve Windows; znaky dává ToUnicodeEx, stejně jako při psaní.</summary>
internal sealed unsafe class WindowsLayout(nint handle) : IKeyboardLayout
{
    public nint Handle { get; } = handle;

    /// <summary>Jazyk z dolního slova HKL, třeba „CS“ nebo „EN“.</summary>
    public string Name { get; } = LanguageOf(handle);

    public KeyOutput Type(Keystroke key)
    {
        var virtualKey = Native.MapVirtualKeyExW(key.ScanCode, Native.MAPVK_VSC_TO_VK_EX, Handle);
        if (virtualKey == 0)
            return KeyOutput.None;

        var state = stackalloc byte[256];
        new Span<byte>(state, 256).Clear();
        if (key.Shift)
            state[Native.VK_SHIFT] = 0x80;
        if (key.AltGr)
        {
            state[Native.VK_CONTROL] = 0x80;
            state[Native.VK_MENU] = 0x80;
        }
        if (key.CapsLock)
            state[Native.VK_CAPITAL] = 0x01;

        var buffer = stackalloc char[8];
        var count = Native.ToUnicodeEx(virtualKey, key.ScanCode, state, buffer, 8, Native.ToUnicodeDontChangeState, Handle);

        return count switch
        {
            < 0 => new KeyOutput(buffer[0].ToString(), IsDeadKey: true),
            0 => KeyOutput.None,
            _ => new KeyOutput(new string(buffer, 0, count)),
        };
    }

    public Keystroke? Find(char c)
    {
        var result = Native.VkKeyScanExW(c, Handle);
        if (result == -1)
            return null;

        // Horní bajt: 1 Shift, 2 Ctrl, 4 Alt. Ctrl+Alt je AltGr; samotný Ctrl nebo Alt píše řídicí znaky.
        var modifiers = (result >> 8) & 0xFF;
        var altGr = (modifiers & 6) == 6;
        if (!altGr && (modifiers & 6) != 0)
            return null;

        var scanCode = Native.MapVirtualKeyExW((uint)(result & 0xFF), Native.MAPVK_VK_TO_VSC, Handle);
        return scanCode == 0 ? null : new Keystroke((ushort)scanCode, (modifiers & 1) != 0, altGr);
    }

    private static string LanguageOf(nint handle)
    {
        try
        {
            return new CultureInfo((int)(handle & 0xFFFF)).TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch (CultureNotFoundException)
        {
            return $"{handle & 0xFFFF:X4}";
        }
    }
}

/// <summary>Nainstalovaná rozložení; pro stejné HKL vrací vždy stejný objekt.</summary>
internal sealed unsafe class Layouts
{
    private readonly Dictionary<nint, WindowsLayout> _cache = [];

    /// <summary>Rozložení, ve kterém se právě píše do okna.</summary>
    public WindowsLayout Current(nint window) =>
        Get(Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(window, out _)));

    /// <summary>Další nainstalované rozložení po <paramref name="current"/>; s jediným rozložením <c>null</c>.</summary>
    public WindowsLayout? Other(WindowsLayout current)
    {
        var count = Native.GetKeyboardLayoutList(0, null);
        if (count < 2)
            return null;

        var list = new nint[count];
        fixed (nint* pointer = list)
            Native.GetKeyboardLayoutList(count, pointer);

        var index = Array.IndexOf(list, current.Handle);
        return Get(list[(index + 1) % count]);
    }

    private WindowsLayout Get(nint handle)
    {
        if (!_cache.TryGetValue(handle, out var layout))
            _cache[handle] = layout = new WindowsLayout(handle);
        return layout;
    }
}
