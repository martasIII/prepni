namespace Prepni.Core;

/// <summary>
/// Jeden stisk fyzické klávesy. Pamatujeme si klávesu (scan code), ne znak: stejnou klávesu pak „zmáčkneme“
/// v jiném rozložení a dostaneme, co uživatel chtěl napsat. Na české klávesnici je na místě US „6“ písmeno „ž“.
/// </summary>
/// <param name="ScanCode">Fyzická klávesa, nezávislá na rozložení.</param>
/// <param name="AltGr">Pravý Alt (třetí úroveň, třeba „€“ nebo „@“ na české klávesnici).</param>
public readonly record struct Keystroke(ushort ScanCode, bool Shift = false, bool AltGr = false, bool CapsLock = false)
{
    public const ushort SpaceScanCode = 0x39;

    /// <summary>Mezera dělí slova a v každém rozložení píše totéž.</summary>
    public bool IsSpace => ScanCode == SpaceScanCode;
}

/// <summary>Co klávesa v daném rozložení napíše.</summary>
/// <param name="IsDeadKey">Mrtvá klávesa (čárka, háček): sama nic nenapíše, jen připraví diakritiku pro další písmeno.</param>
public readonly record struct KeyOutput(string Text, bool IsDeadKey = false)
{
    public static KeyOutput None => new("");
}

/// <summary>Rozložení klávesnice: na Windows nad ToUnicodeEx, v testech z tabulky.</summary>
public interface IKeyboardLayout
{
    /// <summary>Krátký název pro zobrazení, třeba „CS“ nebo „EN“.</summary>
    string Name { get; }

    KeyOutput Type(Keystroke key);

    /// <summary>Klávesa, která v tomto rozložení napíše daný znak (i mrtvou klávesu); <c>null</c>, když žádná.</summary>
    Keystroke? Find(char c);
}
