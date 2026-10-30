using Prepni.Core;

namespace Prepni;

/// <summary>
/// Aplikace v oznamovací oblasti. Dvakrát Shift přepíše poslední slovo tak, jako by bylo napsané
/// v druhém rozložení, a rozložení přepne; každé další dvojí klepnutí přibere další slovo. Když je místo
/// psaní označený text, převede se celý.
/// </summary>
internal sealed class App : IDisposable
{
    private const string Hint = "Přepni: 2× Shift přepíše poslední slovo nebo označený text";

    private const int PauseItem = 1;
    private const int AutostartItem = 2;
    private const int ExitItem = 3;

    private readonly Layouts _layouts = new();
    private readonly TrayWindow _tray;
    private readonly StickyKeysShortcut _stickyKeys = new();
    private readonly KeyboardWatcher _watcher;

    private int _words;
    private WindowsLayout? _target;
    private bool _busy;

    public App()
    {
        _tray = new TrayWindow(Hint);
        _tray.Clicked += ShowMenu;

        _watcher = new KeyboardWatcher(_layouts);

        // Přepis se nesmí dělat uvnitř háčku klávesnice; odložíme ho až za něj.
        _watcher.DoubleShift += () => _tray.Post(Retype);
        _watcher.Interrupted += () => _words = 0;
    }

    public void Run() => TrayWindow.Run();

    private void ShowMenu()
    {
        var chosen = _tray.ShowMenu(
        [
            new MenuItem(PauseItem, "Pozastavit", _watcher.Paused),
            new MenuItem(AutostartItem, "Spouštět s Windows", Autostart.IsEnabled),
            MenuItem.Separator,
            new MenuItem(ExitItem, "Konec"),
        ]);

        switch (chosen)
        {
            case PauseItem:
                _watcher.Paused = !_watcher.Paused;
                _watcher.Text.Clear();
                _tray.SetTip(_watcher.Paused ? "Přepni: pozastaveno" : Hint);
                break;

            case AutostartItem:
                Autostart.Set(!Autostart.IsEnabled);
                break;

            case ExitItem:
                _tray.Quit();
                break;
        }
    }

    private async void Retype()
    {
        // Převod označeného textu chvíli čeká na schránku; další klepnutí mezitím by ho zdvojilo.
        if (_busy)
            return;

        _busy = true;
        try
        {
            var window = Native.GetForegroundWindow();
            var current = _layouts.Current(window);

            // Při prvním klepnutí zvolíme cíl a držíme ho; po přepnutí by „druhé“ rozložení bylo zase to původní.
            if (_words == 0)
            {
                _target = _layouts.Other(current);
                if (_target is null)
                {
                    _tray.ShowBalloon("Přepni", "Ve Windows je jen jedno rozložení klávesnice, není kam přepnout.");
                    return;
                }

                // Nic nenapsáno od kliknutí nebo šipek: možná je označený text.
                if (_watcher.Text.IsEmpty)
                {
                    await RetypeSelectionAsync(window, current, _target);
                    return;
                }
            }

            var retyping = _watcher.Text.Retype(_words + 1, _target!);
            if (retyping is null)
                return;

            _words++;
            Retyper.Apply(retyping);
            if (_words == 1)
                Retyper.SwitchLayout(window, _target!);

            ShowChange(retyping.Original, retyping.Text);
        }
        catch
        {
            // async void: výjimka by shodila proces. Nepovedená oprava se nestane, text zůstane, jak byl.
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RetypeSelectionAsync(nint window, WindowsLayout source, WindowsLayout target)
    {
        var selected = await Selection.CopyAsync(_tray.Handle);
        if (selected is null)
            return;

        var retyped = Typist.Retype(selected, source, target);
        if (retyped.Contains('\n') || retyped.Contains('\r'))
            await Selection.PasteAsync(_tray.Handle, retyped);
        else
            Retyper.Type(retyped);

        Retyper.SwitchLayout(window, target);
        ShowChange(selected, retyped);
    }

    private void ShowChange(string original, string retyped)
    {
        var change = $"{original.Trim()} → {retyped.Trim()}".ReplaceLineEndings(" ");
        _tray.SetTip(change.Length <= 120 ? change : change[..119] + "…");
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _stickyKeys.Dispose();
        _tray.Dispose();
    }
}
