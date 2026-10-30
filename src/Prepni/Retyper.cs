using System.Runtime.InteropServices;
using Prepni.Core;

namespace Prepni;

/// <summary>Provede opravu v aktivním okně: smaže původní text Backspacem a napíše nový jako znaky Unicode.</summary>
internal static class Retyper
{
    public static void Apply(Retyping retyping)
    {
        var inputs = new List<Native.INPUT>();
        for (var i = 0; i < retyping.DeleteCount; i++)
        {
            inputs.Add(Key(Native.VK_BACK, 0, 0));
            inputs.Add(Key(Native.VK_BACK, 0, Native.KEYEVENTF_KEYUP));
        }

        // Znaky Unicode se napíšou stejně v jakémkoli rozložení, nemusíme čekat na jeho přepnutí.
        foreach (var c in retyping.Text)
        {
            inputs.Add(Key(0, c, Native.KEYEVENTF_UNICODE));
            inputs.Add(Key(0, c, Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP));
        }

        Native.SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<Native.INPUT>());
    }

    /// <summary>Požádá okno, ať dál píše v jiném rozložení (stejně jako Win+mezerník).</summary>
    public static void SwitchLayout(nint window, WindowsLayout layout) =>
        Native.PostMessage(window, Native.WM_INPUTLANGCHANGEREQUEST, 0, layout.Handle);

    private static Native.INPUT Key(ushort virtualKey, ushort scan, uint flags) => new()
    {
        type = Native.INPUT_KEYBOARD,
        u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = virtualKey, wScan = scan, dwFlags = flags } },
    };
}
