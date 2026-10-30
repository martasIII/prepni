using Prepni.Core;

namespace Prepni;

/// <summary>
/// Píše do aktivního okna. Opravu nepíše jako znaky Unicode, ale zmáčkne stejné fyzické klávesy jako uživatel,
/// jen v přepnutém rozložení, a to tempem rychlého písaře. Notepad ve Windows 11 zpracovává vstup opožděně
/// (po mezeře kontroluje pravopis) a čte přitom aktuální stav kláves: znaky Unicode („VK_PACKET“) poslané
/// najednou přečetl všechny jako ten poslední („převážně yyyyyyy“) a krátce držený Shift už neviděl
/// („pozítří“ místo „Pozítří“, „ń“ místo „ň“). Mačká se mimo vlákno s háčky, ať se klávesnice nezasekne.
/// </summary>
internal static class Retyper
{
    private const ushort LeftShiftScanCode = 0x2A;
    private const ushort RightAltScanCode = 0x38;

    private const int LayoutSwitchTimeoutMilliseconds = 400;

    /// <summary>Jak dlouho držet Shift nebo AltGr kolem klávesy a Ctrl kolem zkratky.</summary>
    private const int ModifierHoldMilliseconds = 15;

    /// <summary>Pauza mezi klávesami; po mezeře delší, to Notepad kontroluje pravopis.</summary>
    private const int KeyGapMilliseconds = 12;
    private const int AfterSpaceMilliseconds = 40;

    /// <summary>
    /// Přepne okno do <paramref name="target"/>, smaže <paramref name="deleteCount"/> znaků a zmáčkne
    /// <paramref name="keys"/>. Když okno rozložení nepřepne (nebo klávesy nejsou), smaže a vloží
    /// <paramref name="text"/> přes schránku.
    /// </summary>
    public static async Task ReplaceAsync(nint window, WindowsLayout target, int deleteCount,
        IReadOnlyList<Keystroke>? keys, string text, nint clipboardOwner)
    {
        SwitchLayout(window, target);
        var switched = await LayoutSwitchedAsync(window, target);

        await Task.Run(() =>
        {
            // I Backspace po jednom: v Notepadu se jinak jeden ztratil a zůstalo „p“ navíc.
            for (var i = 0; i < deleteCount; i++)
            {
                Tap(Native.VK_BACK);
                Thread.Sleep(KeyGapMilliseconds);
            }

            if (switched && keys is not null)
            {
                foreach (var key in keys)
                    PressSlowly(key);
            }
        });

        if (!switched || keys is null)
            await Selection.PasteAsync(clipboardOwner, text);
    }

    /// <summary>Ctrl + klávesa, třeba Ctrl+C; Ctrl drží chvíli, ať ho i pomalá aplikace vidí.</summary>
    public static Task ShortcutAsync(ushort virtualKey) => Task.Run(() =>
    {
        Send([Key(Native.VK_CONTROL, 0, 0)]);
        Thread.Sleep(ModifierHoldMilliseconds);
        Tap(virtualKey);
        Thread.Sleep(ModifierHoldMilliseconds);
        Send([Key(Native.VK_CONTROL, 0, Native.KEYEVENTF_KEYUP)]);
    });

    /// <summary>Požádá okno, ať dál píše v jiném rozložení (stejně jako Win+mezerník).</summary>
    public static void SwitchLayout(nint window, WindowsLayout layout) =>
        Native.PostMessageW(window, Native.WM_INPUTLANGCHANGEREQUEST, 0, layout.Handle);

    /// <summary>Počká, až okno rozložení opravdu přepne; žádost je jen zpráva, kterou si okno zpracuje samo.</summary>
    private static async Task<bool> LayoutSwitchedAsync(nint window, WindowsLayout target)
    {
        var thread = Native.GetWindowThreadProcessId(window, out _);
        for (var waited = 0; waited <= LayoutSwitchTimeoutMilliseconds; waited += 10)
        {
            if (Native.GetKeyboardLayout(thread) == target.Handle)
                return true;
            await Task.Delay(10);
        }

        return false;
    }

    /// <summary>Stisk fyzické klávesy (scan code) i se Shiftem a AltGr; znak z ní udělá rozložení okna.</summary>
    private static void PressSlowly(Keystroke key)
    {
        var modifiers = new List<Native.INPUT>();
        if (key.Shift)
            modifiers.Add(Key(Native.VK_LSHIFT, LeftShiftScanCode, 0));
        if (key.AltGr)
            modifiers.Add(Key(Native.VK_RMENU, RightAltScanCode, Native.KEYEVENTF_EXTENDEDKEY));

        if (modifiers.Count > 0)
        {
            Send(modifiers);
            Thread.Sleep(ModifierHoldMilliseconds);
        }

        Send([Key(0, key.ScanCode, Native.KEYEVENTF_SCANCODE), Key(0, key.ScanCode, Native.KEYEVENTF_SCANCODE | Native.KEYEVENTF_KEYUP)]);

        if (modifiers.Count > 0)
        {
            Thread.Sleep(ModifierHoldMilliseconds);
            Send(modifiers.Select(m => m with { u = m.u with { ki = m.u.ki with { dwFlags = m.u.ki.dwFlags | Native.KEYEVENTF_KEYUP } } }).Reverse().ToList());
        }

        Thread.Sleep(key.IsSpace ? AfterSpaceMilliseconds : KeyGapMilliseconds);
    }

    private static void Tap(ushort virtualKey) =>
        Send([Key(virtualKey, 0, 0), Key(virtualKey, 0, Native.KEYEVENTF_KEYUP)]);

    private static unsafe void Send(List<Native.INPUT> inputs)
    {
        if (inputs.Count == 0)
            return;

        var array = inputs.ToArray();
        fixed (Native.INPUT* pointer = array)
            Native.SendInput((uint)array.Length, pointer, sizeof(Native.INPUT));
    }

    /// <summary>Podpis vlastních stisků, aby je háček poznal a nesledoval (ani v testovacím režimu).</summary>
    public const nint Signature = 0x50524550;

    private static Native.INPUT Key(ushort virtualKey, ushort scan, uint flags) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = virtualKey, wScan = scan, dwFlags = flags, dwExtraInfo = Signature } },
    };
}
