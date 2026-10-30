using System.Globalization;

namespace Prepni.Core;

/// <summary>
/// Co uživatel napsal od posledního přerušení (Enter, kliknutí, jiné okno): klávesy i s rozložením, ve kterém
/// je text právě vidět. Jen v paměti a jen pár desítek kláves; nic se neukládá ani neposílá.
/// </summary>
public sealed class TypedText
{
    /// <summary>Víc si nepamatujeme; na opravu pár slov to stačí a v paměti nezůstává delší text.</summary>
    public const int MaxKeys = 200;

    private readonly List<TypedUnit> _units = [];
    private Keystroke? _pendingDeadKey;

    public bool IsEmpty => _units.Count == 0;

    /// <summary>Klávesa napsaná v daném rozložení.</summary>
    public void Type(Keystroke key, IKeyboardLayout layout)
    {
        // Mrtvá klávesa se nezobrazí hned; s dalším písmenem tvoří jeden znak, který smaže jeden Backspace.
        if (_pendingDeadKey is { } deadKey)
        {
            _units.Add(new TypedUnit([deadKey, key], layout));
            _pendingDeadKey = null;
        }
        else if (layout.Type(key).IsDeadKey)
        {
            _pendingDeadKey = key;
            return;
        }
        else
        {
            _units.Add(new TypedUnit([key], layout));
        }

        if (_units.Count > MaxKeys)
            _units.RemoveRange(0, _units.Count - MaxKeys);
    }

    public void Backspace()
    {
        if (_pendingDeadKey is not null)
            _pendingDeadKey = null;
        else if (_units.Count > 0)
            _units.RemoveAt(_units.Count - 1);
    }

    public void Clear()
    {
        _units.Clear();
        _pendingDeadKey = null;
    }

    /// <summary>
    /// Přepíše posledních <paramref name="words"/> slov tak, jako by je uživatel napsal v rozložení
    /// <paramref name="target"/>. Mezery na konci zůstanou. Slov může být méně, pak se vezme všechno.
    /// </summary>
    /// <returns>Co smazat a co napsat místo toho; <c>null</c>, když není co přepsat.</returns>
    public Retyping? Retype(int words, IKeyboardLayout target)
    {
        var start = StartOfLastWords(words);
        if (start is not { } from)
            return null;

        var region = _units.Skip(from).ToList();
        var shown = TypeShown(region);

        // Celý úsek jako jeden sled kláves: „+“ v US je v češtině mrtvá klávesa a složí se s dalším písmenem („+n“ = „ň“).
        var retyped = Typist.Type(region.SelectMany(u => u.Keys), target);

        foreach (var unit in region)
            unit.Shown = target;

        return new Retyping(new StringInfo(shown).LengthInTextElements, retyped, shown);
    }

    /// <summary>Klávesy posledních <paramref name="words"/> slov (stejný úsek jako <see cref="Retype"/>), i s mezerami na konci.</summary>
    public IReadOnlyList<Keystroke> KeysOfLastWords(int words) =>
        StartOfLastWords(words) is { } from ? _units.Skip(from).SelectMany(u => u.Keys).ToList() : [];

    /// <summary>Co je z úseku vidět: souvislé kusy v jednom rozložení se píšou vcelku, aby seděly mrtvé klávesy.</summary>
    private static string TypeShown(List<TypedUnit> units)
    {
        var text = new System.Text.StringBuilder();
        for (var start = 0; start < units.Count;)
        {
            var layout = units[start].Shown;
            var end = start;
            while (end < units.Count && units[end].Shown == layout)
                end++;

            text.Append(Typist.Type(units.Skip(start).Take(end - start).SelectMany(u => u.Keys), layout));
            start = end;
        }

        return text.ToString();
    }

    private int? StartOfLastWords(int words)
    {
        var index = _units.Count;
        while (index > 0 && _units[index - 1].IsSpace)
            index--;
        if (index == 0)
            return null;

        for (var found = 1; ; found++)
        {
            while (index > 0 && !_units[index - 1].IsSpace)
                index--;

            var beforeSpaces = index;
            while (beforeSpaces > 0 && _units[beforeSpaces - 1].IsSpace)
                beforeSpaces--;

            if (found == words || beforeSpaces == 0)
                return index;

            index = beforeSpaces;
        }
    }

    /// <summary>Jeden viditelný znak: klávesa, případně s mrtvou klávesou před ní.</summary>
    private sealed class TypedUnit(Keystroke[] keys, IKeyboardLayout shown)
    {
        public Keystroke[] Keys { get; } = keys;

        /// <summary>Rozložení, ve kterém je znak právě na obrazovce; po přepsání to cílové.</summary>
        public IKeyboardLayout Shown { get; set; } = shown;

        public bool IsSpace => Keys is [{ IsSpace: true }];
    }
}

/// <summary>Oprava na obrazovce: smazat <paramref name="DeleteCount"/> znaků a napsat <paramref name="Text"/>.</summary>
/// <param name="Original">Co bylo vidět před opravou, pro zobrazení „slo6it8 → složitá“.</param>
public sealed record Retyping(int DeleteCount, string Text, string Original);
