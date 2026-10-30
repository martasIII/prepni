using Prepni.Core;

namespace Prepni.Core.Tests;

/// <summary>
/// Zjednodušené rozložení z tabulky (scan code → znak), aby testy nezávisely na tom, co je ve Windows
/// nainstalované. Obsahuje písmena, číselnou řadu, interpunkci a mezeru; na zbytku testy nestojí.
/// </summary>
internal sealed class TableLayout : IKeyboardLayout
{
    // Pořadí kláves v tabulkách níž: číselná řada, horní, prostřední a dolní řada písmen. Musí být před
    // rozloženími, statická pole se inicializují v pořadí, jak jsou napsaná.
    private static readonly ushort[] ScanCodes =
    [
        0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D,
        0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B,
        0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29,
        0x2B, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35,
    ];

    private readonly Dictionary<(ushort Scan, bool Shift), KeyOutput> _keys = [];

    private TableLayout(string name) => Name = name;

    public string Name { get; }

    public KeyOutput Type(Keystroke key)
    {
        if (_keys.TryGetValue((key.ScanCode, key.Shift), out var output))
            return output;
        return KeyOutput.None;
    }

    /// <summary>Klávesy, které v tomto rozložení napíšou daný text (každý znak jednou klávesou).</summary>
    public IEnumerable<Keystroke> KeysFor(string text) => text.Select(c =>
    {
        var match = _keys.First(k => !k.Value.IsDeadKey && k.Value.Text == c.ToString());
        return new Keystroke(match.Key.Scan, match.Key.Shift);
    });

    public static TableLayout Us { get; } = Create("EN",
        "1234567890-=" + "qwertyuiop[]" + "asdfghjkl;'`" + "\\zxcvbnm,./",
        "!@#$%^&*()_+" + "QWERTYUIOP{}" + "ASDFGHJKL:\"~" + "|ZXCVBNM<>?");

    /// <summary>České QWERTZ: diakritika na číselné řadě, „´“ a „ˇ“ jako mrtvé klávesy, prohozené Y a Z.</summary>
    public static TableLayout Cs { get; } = Create("CS",
        "+ěščřžýáíé=´" + "qwertzuiopú)" + "asdfghjklů§;" + "¨yxcvbnm,.-",
        "1234567890%ˇ" + "QWERTZUIOP/(" + "ASDFGHJKL\"!°" + "'YXCVBNM?:_",
        deadKeys: "´ˇ¨");

    private static TableLayout Create(string name, string plain, string shifted, string deadKeys = "")
    {
        var layout = new TableLayout(name);
        for (var i = 0; i < ScanCodes.Length; i++)
        {
            layout._keys[(ScanCodes[i], false)] = new KeyOutput(plain[i].ToString(), deadKeys.Contains(plain[i]));
            layout._keys[(ScanCodes[i], true)] = new KeyOutput(shifted[i].ToString(), deadKeys.Contains(shifted[i]));
        }

        layout._keys[(Keystroke.SpaceScanCode, false)] = new KeyOutput(" ");
        layout._keys[(Keystroke.SpaceScanCode, true)] = new KeyOutput(" ");
        return layout;
    }
}
