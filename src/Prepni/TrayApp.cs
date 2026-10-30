using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Prepni;

/// <summary>
/// Aplikace v oznamovací oblasti. Dvakrát Shift přepíše poslední slovo tak, jako by bylo napsané
/// v druhém rozložení, a rozložení přepne; každé další dvojí klepnutí přibere další slovo.
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string Hint = "Přepni: 2× Shift přepíše poslední slovo";

    private readonly Layouts _layouts = new();
    private readonly KeyboardWatcher _watcher;
    private readonly NotifyIcon _icon;
    private readonly StickyKeysShortcut _stickyKeys = new();

    // Přepis se nesmí dělat uvnitř háčku klávesnice; přes skryté okno ho odložíme až za něj.
    private readonly Control _invoker = new();

    private int _words;
    private WindowsLayout? _target;

    public TrayApp()
    {
        _ = _invoker.Handle;
        _watcher = new KeyboardWatcher(_layouts);
        _watcher.DoubleShift += () => _invoker.BeginInvoke(Retype);
        _watcher.Interrupted += () => _words = 0;

        var pause = new ToolStripMenuItem("Pozastavit") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) =>
        {
            _watcher.Paused = pause.Checked;
            _watcher.Text.Clear();
            _icon!.Text = pause.Checked ? "Přepni: pozastaveno" : Hint;
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(pause);
        menu.Items.Add("Konec", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = Hint,
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    private void Retype()
    {
        var window = Native.GetForegroundWindow();

        // Při prvním klepnutí zvolíme cíl a držíme ho; po přepnutí by „druhé“ rozložení bylo zase to původní.
        if (_words == 0)
        {
            _target = _layouts.Other(_layouts.Current(window));
            if (_target is null)
            {
                _icon.ShowBalloonTip(3000, "Přepni", "Ve Windows je jen jedno rozložení klávesnice, není kam přepnout.", ToolTipIcon.Info);
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

        var change = $"{retyping.Original.Trim()} → {retyping.Text.Trim()}";
        _icon.Text = change.Length <= 120 ? change : change[..119] + "…";
    }

    protected override void ExitThreadCore()
    {
        _watcher.Dispose();
        _stickyKeys.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _invoker.Dispose();
        base.ExitThreadCore();
    }

    /// <summary>Ikonka: dvě šipky přes sebe na modrém čtverečku.</summary>
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var background = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var path = RoundedRectangle(new Rectangle(1, 1, 30, 30), 7);
            g.FillPath(background, path);
            using var font = new Font("Segoe UI Symbol", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("⇄", font, Brushes.White, new RectangleF(0, 0, 32, 32), format);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
