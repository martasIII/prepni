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

    /// <summary>
    /// Kterým směrem převést hotový text (označenou větu). Rozložení klávesnice může být už přepnuté, takže
    /// rozhoduje text: kde jsou mezi písmeny číslice a znaky („poz9t59“), psalo se ve špatném rozložení. Vyhraje
    /// směr s čistším výsledkem; nerozhodně (třeba „+ěšč“ → „1234“) z aktuálního do druhého.
    /// </summary>
    /// <returns><c>null</c>, když text už vypadá správně líp než oba převody („Ahoj, jak se máš? (dobře)“).</returns>
    public static (IKeyboardLayout From, IKeyboardLayout To)? Direction(string text, IKeyboardLayout current, IKeyboardLayout other)
    {
        var forward = Suspicion(Retype(text, current, other));
        var backward = Suspicion(Retype(text, other, current));
        if (Suspicion(text) < Math.Min(forward, backward))
            return null;

        return backward < forward ? (other, current) : (current, other);
    }

    // Běžná interpunkce na kraji slova není podezřelá: „(ahoj“, „oblačno,“, „konec.“, „»tak«“.
    private const string LeadingMarks = "([{\"'«„“‚‘¿¡";
    private const string TrailingMarks = ".,;:!?…)]}\"'»“”’";

    /// <summary>
    /// Kolik znaků vypadá jako napsaných ve špatném rozložení: cokoli jiného než písmeno uvnitř slova („poz9t59“,
    /// „m;7re“), kromě interpunkce na kraji a spojovníku nebo apostrofu mezi písmeny („don't“, „aujourd'hui“).
    /// Počítá se i u rozložení, kde se prohazují písmena (AZERTY): tam se správná část věty pokazí oběma směry
    /// a rozhodnout musí jen ty znaky.
    /// </summary>
    private static int Suspicion(string text)
    {
        var score = 0;
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!word.Any(char.IsLetter))
                continue;

            var start = 0;
            var end = word.Length;
            while (start < end && LeadingMarks.Contains(word[start]))
                start++;
            while (end > start && TrailingMarks.Contains(word[end - 1]))
                end--;

            for (var i = start; i < end; i++)
            {
                var c = word[i];
                if (char.IsLetter(c))
                    continue;

                var joiner = c is '-' or '\'' or '’' && i > start && i < end - 1
                    && char.IsLetter(word[i - 1]) && char.IsLetter(word[i + 1]);
                if (!joiner)
                    score++;
            }
        }

        return score;
    }

    /// <summary>
    /// Klávesy, kterými se text v daném rozložení napíše; <c>null</c>, když některý znak napsat nejde (konec řádku,
    /// emoji). Stejné klávesy v jiném rozložení pak napíšou převedený text.
    /// </summary>
    public static IReadOnlyList<Keystroke>? KeysFor(string text, IKeyboardLayout layout)
    {
        var keys = new List<Keystroke>();
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text.Normalize(NormalizationForm.FormC));
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            if (element.Length != 1 || char.IsControl(element[0]) || KeysFor(element[0], layout) is not { } found)
                return null;
            keys.AddRange(found);
        }

        return keys;
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
