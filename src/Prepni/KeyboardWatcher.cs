using System.Runtime.InteropServices;
using Prepni.Core;

namespace Prepni;

/// <summary>
/// Sleduje klávesnici a myš přes nízkoúrovňové háčky Windows: skládá napsaný text (jen poslední slova, jen
/// v paměti) a pozná dvojí klepnutí na Shift.
/// <para>
/// Háčky mají vlastní vlákno, které nedělá nic jiného. Když háček neodpoví dost rychle, Windows ho potichu
/// vyřadí a aplikace pak klávesy nevidí (dvojí Shift nic nedělal); práce se schránkou nebo okny ve stejném
/// vlákně ho zdržovat nesmí. Pro jistotu se každých pár sekund kontroluje, jestli háček o vstupu ví, a když ne,
/// nainstaluje se znovu.
/// </para>
/// </summary>
internal sealed unsafe class KeyboardWatcher : IDisposable
{
    private const uint TapMilliseconds = 300;
    private const uint DoubleTapMilliseconds = 450;
    private const uint CheckMilliseconds = 5000;
    private const int SilentHookMilliseconds = 2000;

    /// <summary>
    /// Testovací režim (proměnná prostředí PREPNI_TEST_INPUT=1): sleduje i klávesy, které posílá jiný program,
    /// aby šlo vyzkoušet celou cestu psaní → dvojí Shift → oprava bez člověka u klávesnice. Vlastní stisky
    /// aplikace se nesledují nikdy.
    /// </summary>
    private static readonly bool AcceptInjected = Environment.GetEnvironmentVariable("PREPNI_TEST_INPUT") == "1";

    // Windows volají háček jako obyčejnou funkci bez objektu; instance je v procesu jen jedna.
    private static KeyboardWatcher? _instance;

    private readonly Layouts _layouts;
    private readonly TypedText _text = new();
    private readonly Thread _thread;
    private uint _threadId;
    private nint _keyboardHook;
    private nint _mouseHook;
    private int _lastHookTick = Environment.TickCount;
    private volatile bool _paused;
    private Exception? _startError;

    // Jen ve vlákně háčků.
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

        using var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = "Prepni háčky" };
        _thread.Start();
        started.Wait();
        if (_startError is not null)
            throw _startError;
    }

    /// <summary>Dvakrát rychle Shift, bez jiné klávesy mezi tím. Volá se z vlákna háčků.</summary>
    public event Action? DoubleShift;

    /// <summary>Uživatel něco napsal nebo kliknul: případný probíhající převod končí. Volá se z vlákna háčků.</summary>
    public event Action? Interrupted;

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            ClearText();
        }
    }

    /// <summary>Přístup k napsanému textu z jiného vlákna (okna aplikace).</summary>
    public T Read<T>(Func<TypedText, T> read)
    {
        lock (_text)
            return read(_text);
    }

    public void ClearText()
    {
        lock (_text)
            _text.Clear();
    }

    private void Run(ManualResetEventSlim started)
    {
        _threadId = Native.GetCurrentThreadId();
        try
        {
            InstallHooks();
        }
        catch (Exception ex)
        {
            _startError = ex;
            started.Set();
            return;
        }

        Native.SetTimer(0, 0, CheckMilliseconds, 0);
        started.Set();

        Native.MSG message;
        while (Native.GetMessageW(&message, 0, 0, 0) > 0)
        {
            if (message.message == Native.WM_TIMER)
                CheckHooks();
            else
                Native.DispatchMessageW(&message);
        }

        RemoveHooks();
    }

    private void InstallHooks()
    {
        var module = Native.GetModuleHandleW(null);
        _keyboardHook = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, &OnKeyboard, module, 0);
        _mouseHook = Native.SetWindowsHookExW(Native.WH_MOUSE_LL, &OnMouse, module, 0);
        if (_keyboardHook == 0 || _mouseHook == 0)
            throw new InvalidOperationException($"Háček klávesnice se nepodařilo nainstalovat (chyba {Marshal.GetLastPInvokeError()}).");
        _lastHookTick = Environment.TickCount;
    }

    private void RemoveHooks()
    {
        Native.UnhookWindowsHookEx(_keyboardHook);
        Native.UnhookWindowsHookEx(_mouseHook);
    }

    /// <summary>Windows zaznamenaly vstup, o kterém háček neví: vyřadily ho. Nainstalovat znovu.</summary>
    private void CheckHooks()
    {
        var input = new Native.LASTINPUTINFO { cbSize = (uint)sizeof(Native.LASTINPUTINFO) };
        if (!Native.GetLastInputInfo(&input))
            return;

        if ((int)(input.dwTime - (uint)_lastHookTick) > SilentHookMilliseconds)
        {
            RemoveHooks();
            InstallHooks();
            ClearText();
        }
    }

    [UnmanagedCallersOnly]
    private static nint OnKeyboard(int code, nint wParam, nint lParam)
    {
        var watcher = _instance;
        try
        {
            if (watcher is not null)
            {
                watcher._lastHookTick = Environment.TickCount;
                if (code >= 0 && !watcher._paused)
                {
                    var info = *(Native.KBDLLHOOKSTRUCT*)lParam;

                    // Vlastní přepis (a jiné programy, které píšou za uživatele) nesledujeme.
                    var injected = (info.flags & Native.LLKHF_INJECTED) != 0;
                    if (!injected || (AcceptInjected && info.dwExtraInfo != Retyper.Signature))
                        watcher.OnKey(info, down: wParam is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN);
                }
            }
        }
        catch
        {
            // Výjimka z háčku by shodila celý proces; jeden ztracený stisk nevadí.
            watcher?.ClearText();
        }

        return Native.CallNextHookEx(0, code, wParam, lParam);
    }

    [UnmanagedCallersOnly]
    private static nint OnMouse(int code, nint wParam, nint lParam)
    {
        var watcher = _instance;
        if (watcher is not null)
        {
            watcher._lastHookTick = Environment.TickCount;

            // Kliknutí přesune kurzor jinam; co jsme si pamatovali, už nemusí být před ním.
            if (code >= 0 && wParam is Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN or Native.WM_MBUTTONDOWN)
                watcher.Interrupt();
        }

        return Native.CallNextHookEx(0, code, wParam, lParam);
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

        var window = Native.FocusedWindow();
        if (window != _window)
        {
            ClearText();
            _window = window;
        }

        if (info.vkCode == Native.VK_BACK)
        {
            lock (_text)
                _text.Backspace();
            return;
        }

        // AltGr posílá i levý Ctrl; skutečná zkratka (Ctrl+V, Alt+Tab, Win+…) text změní nebo přesune kurzor.
        var altGr = Native.IsDown(Native.VK_RMENU);
        var shortcut = (!altGr && Native.IsDown(Native.VK_CONTROL)) || Native.IsDown(Native.VK_LMENU)
            || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN);

        // Rozšířené klávesy jsou šipky, Home, End, Delete, Enter na numerické části…
        if (shortcut || (info.flags & Native.LLKHF_EXTENDED) != 0 || info.scanCode > 0x7F)
        {
            ClearText();
            return;
        }

        var key = new Keystroke((ushort)info.scanCode, Native.IsDown(Native.VK_SHIFT), altGr, _capsLock);
        var layout = _layouts.Current(window);
        var output = layout.Type(key);

        // Enter, Tab, Esc a podobné dávají řídicí znaky: konec úseku, který umíme opravit.
        lock (_text)
        {
            if (key.IsSpace || output.IsDeadKey || (output.Text.Length > 0 && !output.Text.Any(char.IsControl)))
                _text.Type(key, layout);
            else
                _text.Clear();
        }
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
        ClearText();
        Interrupted?.Invoke();
    }

    public void Dispose()
    {
        Native.PostThreadMessageW(_threadId, Native.WM_QUIT, 0, 0);
        _thread.Join(1000);
        ClearText();
        _instance = null;
    }
}
