namespace Prepni;

/// <summary>
/// Windows po pěti Shiftech za sebou nabídnou Jedno stisknutí (Sticky Keys); opakované dvojí klepnutí na Shift
/// na to narazí. Dokud aplikace běží, tuhle zkratku vypneme a při ukončení vrátíme. Jde o postup, který Microsoft
/// doporučuje hrám: změna se neukládá, po odhlášení platí zase původní nastavení. Kdo Jedno stisknutí používá,
/// tomu ho nevypínáme.
/// </summary>
internal sealed unsafe class StickyKeysShortcut : IDisposable
{
    private readonly Native.STICKYKEYS _original;
    private readonly bool _changed;

    public StickyKeysShortcut()
    {
        var original = new Native.STICKYKEYS { cbSize = (uint)sizeof(Native.STICKYKEYS) };
        if (!Native.SystemParametersInfoW(Native.SPI_GETSTICKYKEYS, original.cbSize, &original, 0))
            return;

        _original = original;
        if ((original.dwFlags & Native.SKF_STICKYKEYSON) != 0)
            return;

        var withoutShortcut = original;
        withoutShortcut.dwFlags &= ~(Native.SKF_HOTKEYACTIVE | Native.SKF_CONFIRMHOTKEY);
        _changed = Native.SystemParametersInfoW(Native.SPI_SETSTICKYKEYS, withoutShortcut.cbSize, &withoutShortcut, 0);

        // I při pádu aplikace vrátit, co jsme změnili.
        if (_changed)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
    }

    public void Dispose()
    {
        if (!_changed)
            return;

        var original = _original;
        Native.SystemParametersInfoW(Native.SPI_SETSTICKYKEYS, original.cbSize, &original, 0);
    }
}
