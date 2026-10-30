using Prepni.Core;

namespace Prepni.Core.Tests;

public class TypistTests
{
    private static readonly TableLayout Us = TableLayout.Us;
    private static readonly TableLayout Cs = TableLayout.Cs;

    [Theory]
    [InlineData("slo6it8", "složitá")]
    [InlineData("[pln8", "úplná")]
    [InlineData("m9t", "mít")]
    [InlineData("Slo6it8", "Složitá")]
    [InlineData("ydrav9m", "zdravím")] // QWERTY → QWERTZ: prohozené Y a Z
    public void Text_typed_on_US_keyboard_is_retyped_as_Czech(string typed, string meant)
    {
        Assert.Equal(meant, Typist.Type(Us.KeysFor(typed), Cs));
    }

    [Theory]
    [InlineData("+ěšč", "1234")]
    [InlineData("zes", "yes")]
    [InlineData("ůhat", ";hat")]
    public void Text_typed_on_Czech_keyboard_is_retyped_as_US(string typed, string meant)
    {
        Assert.Equal(meant, Typist.Type(Cs.KeysFor(typed), Us));
    }

    [Fact]
    public void Dead_key_composes_with_the_next_letter()
    {
        // Na české klávesnici „´“ a pak „e“ napíše jedno „é“.
        Keystroke[] keys = [new(0x0D), new(0x12)];

        Assert.Equal("é", Typist.Type(keys, Cs));
        Assert.Equal("=e", Typist.Type(keys, Us));
    }

    [Fact]
    public void US_equals_sign_followed_by_a_letter_becomes_an_accented_letter_in_Czech()
    {
        Assert.Equal("dobré ráno", Typist.Type(Us.KeysFor("dobr=e r=ano"), Cs));
    }

    [Fact]
    public void Hacek_composes_where_a_precomposed_letter_exists()
    {
        // Shift + „´“ je na české klávesnici háček: „ˇ“ + „d“ = „ď“.
        Keystroke[] keys = [new(0x0D, Shift: true), new(0x20)];

        Assert.Equal("ď", Typist.Type(keys, Cs));
    }

    [Fact]
    public void Dead_key_before_a_letter_without_such_accent_writes_both()
    {
        Keystroke[] keys = [new(0x0D, Shift: true), new(0x2D)];

        Assert.Equal("ˇx", Typist.Type(keys, Cs));
    }

    [Theory]
    [InlineData("ta 2 bude dost slo6it8", "ta ě bude dost složitá")]
    [InlineData("Slo6it8 a [pln8", "Složitá a úplná")]
    public void Selected_US_text_is_retyped_as_Czech(string selected, string meant)
    {
        Assert.Equal(meant, Typist.Retype(selected, Us, Cs));
    }

    [Theory]
    [InlineData("total sum is +ěšč", "total sum is 1234")]
    [InlineData("dobré ráno", "dobr0 r8no")]
    public void Selected_Czech_text_is_retyped_as_US(string selected, string meant)
    {
        Assert.Equal(meant, Typist.Retype(selected, Cs, Us));
    }

    [Fact]
    public void Letter_typed_with_a_dead_key_is_found_through_its_accent()
    {
        // „ó“ nemá na české klávesnici vlastní klávesu: píše se „´“ a „o“, v US z toho bude „=o“.
        Assert.Equal("k=od", Typist.Retype("kód", Cs, Us));
        Assert.Equal("kód", Typist.Retype("k=od", Us, Cs));
    }

    [Fact]
    public void Line_breaks_and_unknown_characters_stay()
    {
        Assert.Equal("ž\r\ná 😀", Typist.Retype("6\r\n8 😀", Us, Cs));
    }

    [Fact]
    public void Keys_for_selected_text_type_it_converted_in_the_other_layout()
    {
        var keys = Typist.KeysFor("k=od a [pln8", Us);

        Assert.NotNull(keys);
        Assert.Equal("kód a úplná", Typist.Type(keys, Cs));
    }

    [Fact]
    public void Text_with_a_line_break_has_no_keys()
    {
        Assert.Null(Typist.KeysFor("a" + (char)10 + "b", Us));
    }
}
