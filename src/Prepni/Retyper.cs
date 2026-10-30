using System.Runtime.InteropServices;
using Prepni.Core;

namespace Prepni;

/// <summary>Píše do aktivního okna: maže Backspacem, píše znaky Unicode a posílá zkratky jako Ctrl+C.</summary>
internal static class Retyper
{
    private const char Tab = (char)9;

    /// <summary>Smaže původní text a napíše opravený.</summary>
    public static void Apply(Retyping retyping)
    {
        var inputs = new List<Native.INPUT>();
        for (var i = 0; i < retyping.DeleteCount; i++)
            Press(inputs, Native.VK_BACK);

        AddText(inputs, retyping.Text);
        Send(inputs);
    }

    /// <summary>Napíše text na místo kurzoru, případně přes označený text.</summary>
    public static void Type(string text)
    {
        var inputs = new List<Native.INPUT>();
        AddText(inputs, text);
        Send(inputs);
    }

    /// <summary>Ctrl + klávesa, třeba Ctrl+C.</summary>
    public static void Shortcut(ushort virtualKey)
    {
        var inputs = new List<Native.INPUT>
        {
            Key(Native.VK_CONTROL, 0, 0),
            Key(virtualKey, 0, 0),
            Key(virtualKey, 0, Native.KEYEVENTF_KEYUP),
            Key(Native.VK_CONTROL, 0, Native.KEYEVENTF_KEYUP),
        };
        Send(inputs);
    }

    /// <summary>Požádá okno, ať dál píše v jiném rozložení (stejně jako Win+mezerník).</summary>
    public static void SwitchLayout(nint window, WindowsLayout layout) =>
        Native.PostMessage(window, Native.WM_INPUTLANGCHANGEREQUEST, 0, layout.Handle);

    // Znaky Unicode se napíšou stejně v jakémkoli rozložení, nemusíme čekat na jeho přepnutí.
    private static void AddText(List<Native.INPUT> inputs, string text)
    {
        foreach (var c in text)
        {
            if (c == Tab)
            {
                Press(inputs, Native.VK_TAB);
                continue;
            }

            inputs.Add(Key(0, c, Native.KEYEVENTF_UNICODE));
            inputs.Add(Key(0, c, Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP));
        }
    }

    private static void Press(List<Native.INPUT> inputs, ushort virtualKey)
    {
        inputs.Add(Key(virtualKey, 0, 0));
        inputs.Add(Key(virtualKey, 0, Native.KEYEVENTF_KEYUP));
    }

    private static void Send(List<Native.INPUT> inputs) =>
        Native.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<Native.INPUT>());

    private static Native.INPUT Key(ushort virtualKey, ushort scan, uint flags) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = virtualKey, wScan = scan, dwFlags = flags } },
    };
}
