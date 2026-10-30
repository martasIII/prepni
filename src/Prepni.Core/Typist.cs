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
        ['´'] = '́',
        ['`'] = '̀',
        ['ˇ'] = '̌',
        ['^'] = '̂',
        ['¨'] = '̈',
        ['°'] = '̊',
        ['˚'] = '̊',
        ['~'] = '̃',
        ['˘'] = '̆',
        ['˛'] = '̨',
        ['˙'] = '̇',
        ['˝'] = '̋',
        ['¸'] = '̧',
    };

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
