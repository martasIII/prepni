using System.Runtime.InteropServices;

namespace Prepni;

/// <summary>
/// Windows po pěti Shiftech za sebou nabídnou Jedno stisknutí (Sticky Keys); opakované dvojí klepnutí na Shift
/// na to narazí. Dokud aplikace běží, tuhle zkratku vypneme a při ukončení vrátíme. Jde o postup, který Microsoft
/// doporučuje hrám: změna se neukládá, po odhlášení platí zase původní nastavení. Kdo Jedno stisknutí používá,
/// tomu ho nevypínáme.
/// </summary>
internal sealed class StickyKeysShortcut : IDisposable
{
    private readonly Native.STICKYKEYS _original;
    private readonly bool _changed;

    public StickyKeysShortcut()
    {
        _original = new Native.STICKYKEYS { cbSize = (uint)Marshal.SizeOf<Native.STICKYKEYS>() };
        if (!Native.SystemParametersInfo(Native.SPI_GETSTICKYKEYS, _original.cbSize, ref _original, 0))
            return;

        if ((_original.dwFlags & Native.SKF_STICKYKEYSON) != 0)
            return;

        var withoutShortcut = _original;
        withoutShortcut.dwFlags &= ~(Native.SKF_HOTKEYACTIVE | Native.SKF_CONFIRMHOTKEY);
        _changed = Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, withoutShortcut.cbSize, ref withoutShortcut, 0);

        // I při pádu aplikace vrátit, co jsme změnili.
        if (_changed)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (!_changed)
            return;

        var original = _original;
        Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, original.cbSize, ref original, 0);
    }
}
