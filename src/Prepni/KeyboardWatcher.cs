using System.Runtime.InteropServices;
using Prepni.Core;

namespace Prepni;

/// <summary>
/// Sleduje klávesnici a myš přes nízkoúrovňové háčky Windows: skládá napsaný text (jen poslední slova, jen
/// v paměti) a pozná dvojí klepnutí na Shift. Háček běží ve vlákně s frontou zpráv, které ho nainstalovalo,
/// a musí se vrátit rychle; vlastní přepis proto dělá volající až po návratu.
/// </summary>
internal sealed unsafe class KeyboardWatcher : IDisposable
{
    private const uint TapMilliseconds = 300;
    private const uint DoubleTapMilliseconds = 450;

    // Windows volají háček jako obyčejnou funkci bez objektu; instance je v procesu jen jedna.
    private static KeyboardWatcher? _instance;

    private readonly Layouts _layouts;
    private readonly nint _keyboardHook;
    private readonly nint _mouseHook;

    private nint _window;
    private bool _capsLock = (Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0;

    private bool _shiftDown;
    private bool _shiftAlone;
    private uint _shiftDownAt;
    private uint _lastTapAt;

    public KeyboardWatcher(Layouts layouts)
    {
        _layouts = layouts;
        _instance = this;

        var module = Native.GetModuleHandleW(null);
        _keyboardHook = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, &OnKeyboard, module, 0);
        _mouseHook = Native.SetWindowsHookExW(Native.WH_MOUSE_LL, &OnMouse, module, 0);
        if (_keyboardHook == 0 || _mouseHook == 0)
            throw new InvalidOperationException($"Háček klávesnice se nepodařilo nainstalovat (chyba {Marshal.GetLastPInvokeError()}).");
    }

    public TypedText Text { get; } = new();

    public bool Paused { get; set; }

    /// <summary>Dvakrát rychle Shift, bez jiné klávesy mezi tím.</summary>
    public event Action? DoubleShift;

    /// <summary>Uživatel něco napsal nebo kliknul: případný probíhající převod končí.</summary>
    public event Action? Interrupted;

    [UnmanagedCallersOnly]
    private static nint OnKeyboard(int code, nint wParam, nint lParam)
    {
        var watcher = _instance;
        try
        {
            if (code >= 0 && watcher is { Paused: false })
            {
                var info = *(Native.KBDLLHOOKSTRUCT*)lParam;

                // Vlastní přepis (a jiné programy, které píšou za uživatele) nesledujeme.
                if ((info.flags & Native.LLKHF_INJECTED) == 0)
                    watcher.OnKey(info, down: wParam is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN);
            }
        }
        catch
        {
            // Výjimka z háčku by shodila celý proces; jeden ztracený stisk nevadí.
            watcher?.Text.Clear();
        }

        return Native.CallNextHookEx(watcher?._keyboardHook ?? 0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static nint OnMouse(int code, nint wParam, nint lParam)
    {
        var watcher = _instance;

        // Kliknutí přesune kurzor jinam; co jsme si pamatovali, už nemusí být před ním.
        if (code >= 0 && wParam is Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN or Native.WM_MBUTTONDOWN)
            watcher?.Interrupt();

        return Native.CallNextHookEx(watcher?._mouseHook ?? 0, code, wParam, lParam);
    }

    private void OnKey(Native.KBDLLHOOKSTRUCT info, bool down)
    {
        if (info.vkCode is Native.VK_LSHIFT or Native.VK_RSHIFT or Native.VK_SHIFT)
        {
            OnShift(down, info.time);
            return;
        }

        if (!down)
            return;

        _shiftAlone = false;
        _lastTapAt = 0;

        switch (info.vkCode)
        {
            case Native.VK_LCONTROL or Native.VK_RCONTROL or Native.VK_LMENU or Native.VK_RMENU
                or Native.VK_LWIN or Native.VK_RWIN:
                return;

            case Native.VK_CAPITAL:
                _capsLock = !_capsLock;
                return;
        }

        Interrupted?.Invoke();

        var window = Native.GetForegroundWindow();
        if (window != _window)
        {
            Text.Clear();
            _window = window;
        }

        if (info.vkCode == Native.VK_BACK)
        {
            Text.Backspace();
            return;
        }

        // AltGr posílá i levý Ctrl; skutečná zkratka (Ctrl+V, Alt+Tab, Win+…) text změní nebo přesune kurzor.
        var altGr = Native.IsDown(Native.VK_RMENU);
        var shortcut = (!altGr && Native.IsDown(Native.VK_CONTROL)) || Native.IsDown(Native.VK_LMENU)
            || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN);

        // Rozšířené klávesy jsou šipky, Home, End, Delete, Enter na numerické části…
        if (shortcut || (info.flags & Native.LLKHF_EXTENDED) != 0 || info.scanCode > 0x7F)
        {
            Text.Clear();
            return;
        }

        var key = new Keystroke((ushort)info.scanCode, Native.IsDown(Native.VK_SHIFT), altGr, _capsLock);
        var layout = _layouts.Current(window);
        var output = layout.Type(key);

        // Enter, Tab, Esc a podobné dávají řídicí znaky: konec úseku, který umíme opravit.
        if (key.IsSpace || output.IsDeadKey || (output.Text.Length > 0 && !output.Text.Any(char.IsControl)))
            Text.Type(key, layout);
        else
            Text.Clear();
    }

    private void OnShift(bool down, uint time)
    {
        if (down)
        {
            // Držený Shift posílá opakované stisky; počítá se jen první.
            if (!_shiftDown)
            {
                _shiftDown = true;
                _shiftAlone = true;
                _shiftDownAt = time;
            }
            return;
        }

        _shiftDown = false;

        // Ctrl+Shift a Alt+Shift přepínají rozložení ve Windows; to není klepnutí.
        var withModifier = Native.IsDown(Native.VK_CONTROL) || Native.IsDown(Native.VK_MENU)
            || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN);
        if (!_shiftAlone || withModifier || time - _shiftDownAt > TapMilliseconds)
        {
            _lastTapAt = 0;
            return;
        }

        if (_lastTapAt != 0 && time - _lastTapAt <= DoubleTapMilliseconds)
        {
            _lastTapAt = 0;
            DoubleShift?.Invoke();
        }
        else
        {
            _lastTapAt = time;
        }
    }

    private void Interrupt()
    {
        Text.Clear();
        Interrupted?.Invoke();
    }

    public void Dispose()
    {
        Native.UnhookWindowsHookEx(_keyboardHook);
        Native.UnhookWindowsHookEx(_mouseHook);
        Text.Clear();
        _instance = null;
    }
}
