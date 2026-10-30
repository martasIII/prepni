using System.Text;

namespace Prepni.Core;

/// <summary>
/// „Napíše“ klávesy v daném rozložení, včetně mrtvých kláves: čárka nebo háček se složí s dalším písmenem
/// do jednoho znaku stejně, jako to dělají Windows („´“ + „e“ = „é“).
/// </summary>
public static class Typist
{
    // Mrtvá klávesa dává samostatný znak diakritiky; ke složení potřebujeme jeho kombinující podobu.
    private static readonly Dictionary<char, char> CombiningMarks = new()
    {
        ['´'] = '\u0301',
        ['`'] = '\u0300',
        ['ˇ'] = '\u030C',
        ['^'] = '\u0302',
        ['¨'] = '\u0308',
        ['°'] = '\u030A',
        ['˚'] = '\u030A',
        ['~'] = '\u0303',
        ['˘'] = '\u0306',
        ['˛'] = '\u0328',
        ['˙'] = '\u0307',
        ['˝'] = '\u030B',
        ['¸'] = '\u0327',
    };

    /// <summary>
    /// Převede hotový text, třeba označenou větu: zjistí, kterými klávesami se v <paramref name="from"/> napsal,
    /// a napíše je v <paramref name="to"/>. Znaky, které v <paramref name="from"/> napsat nejde (konce řádků,
    /// emoji), zůstanou, jak jsou.
    /// </summary>
    public static string Retype(string text, IKeyboardLayout from, IKeyboardLayout to)
    {
        var result = new StringBuilder();

        // Klávesy píšeme jako jeden souvislý sled: „=“ v US je v češtině mrtvá klávesa a složí se s dalším písmenem.
        var keys = new List<Keystroke>();
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text.Normalize(NormalizationForm.FormC));
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            if (element.Length == 1 && !char.IsControl(element[0]) && KeysFor(element[0], from) is { } found)
            {
                keys.AddRange(found);
                continue;
            }

            result.Append(Type(keys, to)).Append(element);
            keys.Clear();
        }

        return result.Append(Type(keys, to)).ToString();
    }

    /// <summary>Klávesy pro znak: přímo, nebo mrtvá klávesa a písmeno („ó“ = „´“ + „o“).</summary>
    private static Keystroke[]? KeysFor(char c, IKeyboardLayout layout)
    {
        if (layout.Find(c) is { } key)
            return [key];

        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
        if (decomposed.Length != 2)
            return null;

        var accent = CombiningMarks.FirstOrDefault(m => m.Value == decomposed[1]).Key;
        if (accent == default || layout.Find(accent) is not { } deadKey || layout.Find(decomposed[0]) is not { } letter)
            return null;

        return [deadKey, letter];
    }

    public static string Type(IEnumerable<Keystroke> keys, IKeyboardLayout layout)
    {
        var text = new StringBuilder();
        string? pendingAccent = null;

        foreach (var key in keys)
        {
            var output = layout.Type(key);
            if (output.IsDeadKey)
            {
                // Dvě mrtvé klávesy za sebou: první se napíše samostatně.
                text.Append(pendingAccent);
                pendingAccent = output.Text;
                continue;
            }

            text.Append(pendingAccent is null ? output.Text : Compose(pendingAccent, output.Text));
            pendingAccent = null;
        }

        text.Append(pendingAccent);
        return text.ToString();
    }

    private static string Compose(string accent, string text)
    {
        // Mezera po mrtvé klávese napíše samotnou diakritiku.
        if (text == " ")
            return accent;

        if (accent.Length == 1 && text.Length == 1 && CombiningMarks.TryGetValue(accent[0], out var mark))
        {
            var composed = (text + mark).Normalize(NormalizationForm.FormC);
            if (composed.Length == 1)
                return composed;
        }

        // Nejde složit (háček nad „x“): Windows napíšou obojí za sebou.
        return accent + text;
    }
}
