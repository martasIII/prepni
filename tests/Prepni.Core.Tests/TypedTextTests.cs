using Prepni.Core;

namespace Prepni.Core.Tests;

public class TypedTextTests
{
    private static readonly TableLayout Us = TableLayout.Us;
    private static readonly TableLayout Cs = TableLayout.Cs;

    private static TypedText TypedOnUs(string text)
    {
        var typed = new TypedText();
        foreach (var key in Us.KeysFor(text))
            typed.Type(key, Us);
        return typed;
    }

    [Fact]
    public void First_retyping_takes_the_last_word()
    {
        var typed = TypedOnUs("ta 2 bude dost slo6it8");

        var retyping = typed.Retype(1, Cs);

        Assert.Equal(new Retyping(7, "složitá", "slo6it8"), retyping);
    }

    [Fact]
    public void Each_further_retyping_takes_one_more_word()
    {
        // Z Martinovy zprávy: „ta 2“ byla opravdu dvojka, takže převádět všechno najednou by ji pokazilo.
        var typed = TypedOnUs("ta 2 bude dost slo6it8");

        typed.Retype(1, Cs);
        var second = typed.Retype(2, Cs);

        // Na obrazovce je teď „dost složitá“; smaže se celé a napíše znovu, „dost“ se nezmění.
        Assert.Equal(new Retyping(12, "dost složitá", "dost složitá"), second);
    }

    [Fact]
    public void Retyping_more_words_than_typed_takes_everything()
    {
        var typed = TypedOnUs("m9t slo6it8");

        var retyping = typed.Retype(5, Cs);

        Assert.Equal("mít složitá", retyping?.Text);
    }

    [Fact]
    public void Trailing_space_stays_after_the_retyped_word()
    {
        var typed = TypedOnUs("slo6it8 ");

        var retyping = typed.Retype(1, Cs);

        Assert.Equal(new Retyping(8, "složitá ", "slo6it8 "), retyping);
    }

    [Fact]
    public void Backspace_removes_the_last_key()
    {
        var typed = TypedOnUs("slo6it88");
        typed.Backspace();

        Assert.Equal("složitá", typed.Retype(1, Cs)?.Text);
    }

    [Fact]
    public void Dead_key_and_letter_count_as_one_character_on_screen()
    {
        var typed = new TypedText();
        foreach (var key in Cs.KeysFor("dobr"))
            typed.Type(key, Cs);
        typed.Type(new Keystroke(0x0D), Cs); // „´“
        typed.Type(new Keystroke(0x12), Cs); // „e“ → „é“

        var retyping = typed.Retype(1, Us);

        // Na obrazovce je „dobré“ (5 znaků), v US z toho bude „dobr=e“.
        Assert.Equal(new Retyping(5, "dobr=e", "dobré"), retyping);
    }

    [Fact]
    public void Nothing_to_retype_after_clear()
    {
        var typed = TypedOnUs("slo6it8");
        typed.Clear();

        Assert.Null(typed.Retype(1, Cs));
    }

    [Fact]
    public void Only_spaces_give_nothing_to_retype()
    {
        Assert.Null(TypedOnUs("   ").Retype(1, Cs));
    }

    [Fact]
    public void Keeps_only_the_most_recent_keys()
    {
        var typed = TypedOnUs(new string('a', TypedText.MaxKeys) + " slo6it8");

        Assert.Equal("a složitá", typed.Retype(2, Cs)?.Text[^9..]);
        Assert.Equal(TypedText.MaxKeys, typed.Retype(2, Cs)!.DeleteCount);
    }

    [Fact]
    public void Dead_key_from_the_other_layout_composes_with_the_next_letter()
    {
        // Z Martinova testu („p5eh8+nky“): „+“ na US je v češtině háček, „+n“ má být „ň“, ne „ˇn“.
        var typed = TypedOnUs("m9sto pl8+n");

        Assert.Equal(new Retyping(5, "pláň", "pl8+n"), typed.Retype(1, Cs));

        // Na obrazovce je teď „m9sto pláň“ (10 znaků): háček s „n“ je jeden znak.
        Assert.Equal(new Retyping(10, "místo pláň", "m9sto pláň"), typed.Retype(2, Cs));
    }

    [Fact]
    public void Keys_of_the_last_words_type_the_retyped_text_in_the_target_layout()
    {
        // Aplikace přepne okno do cílového rozložení a zmáčkne stejné klávesy; vyjít musí totéž co Retype.
        var typed = TypedOnUs("m9sto pl8+n ");

        var retyping = typed.Retype(2, Cs);
        var keys = typed.KeysOfLastWords(2);

        Assert.Equal(retyping!.Text, Typist.Type(keys, Cs));
        Assert.Equal("místo pláň ", Typist.Type(keys, Cs));
    }
}
