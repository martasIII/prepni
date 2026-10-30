using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Prepni;

/// <summary>Položka nabídky ikonky; <see cref="Text"/> <c>null</c> je oddělovač.</summary>
internal sealed record MenuItem(int Id, string? Text, bool Checked = false)
{
    public static MenuItem Separator { get; } = new(0, null);
}

/// <summary>
/// Neviditelné okno s frontou zpráv a ikonkou v oznamovací oblasti, místo WinForms. Ve stejném vlákně běží
/// háčky klávesnice; <see cref="Post"/> odloží práci za ně a <c>await</c> se sem vrací přes vlastní
/// SynchronizationContext.
/// </summary>
internal sealed unsafe class TrayWindow : IDisposable
{
    private const string ClassName = "Prepni.TrayWindow";
    private const uint TrayCallbackMessage = Native.WM_APP + 1;
    private const uint RunPostedMessage = Native.WM_APP + 2;

    private static TrayWindow? _instance;

    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly uint _taskbarCreatedMessage;
    private readonly nint _icon;
    private string _tip;

    public TrayWindow(string tip)
    {
        _instance = this;
        _tip = tip;

        var instance = Native.GetModuleHandleW(null);
        fixed (char* className = ClassName)
        {
            var windowClass = new Native.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(Native.WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                hInstance = instance,
                lpszClassName = className,
            };
            Native.RegisterClassExW(&windowClass);
        }

        // Obyčejné okno, jen se nikdy neukáže: nabídka ikonky potřebuje okno, které smí být v popředí.
        Handle = Native.CreateWindowExW(0, ClassName, "Přepni", 0, 0, 0, 0, 0, 0, 0, instance, 0);
        if (Handle == 0)
            throw new InvalidOperationException($"Okno se nepodařilo vytvořit (chyba {Marshal.GetLastPInvokeError()}).");

        // Když se Průzkumník restartuje, ikonky v oznamovací oblasti zmizí a je potřeba je přidat znovu.
        _taskbarCreatedMessage = Native.RegisterWindowMessageW("TaskbarCreated");
        _icon = LoadIcon(instance);
        AddIcon();

        SynchronizationContext.SetSynchronizationContext(new WindowContext(this));
    }

    public nint Handle { get; }

    /// <summary>Klepnutí na ikonku (levým nebo pravým tlačítkem).</summary>
    public event Action? Clicked;

    /// <summary>Spustí práci ve vlákně okna, až se háček vrátí; volat jde odkudkoli.</summary>
    public void Post(Action action)
    {
        _posted.Enqueue(action);
        Native.PostMessageW(Handle, RunPostedMessage, 0, 0);
    }

    /// <summary>Fronta zpráv; skončí po <see cref="Quit"/>.</summary>
    public static void Run()
    {
        Native.MSG message;
        while (Native.GetMessageW(&message, 0, 0, 0) > 0)
        {
            Native.TranslateMessage(&message);
            Native.DispatchMessageW(&message);
        }
    }

    public void Quit() => Native.PostQuitMessage(0);

    public void SetTip(string tip)
    {
        _tip = tip;
        var data = IconData(Native.NIF_TIP);
        Native.Shell_NotifyIconW(Native.NIM_MODIFY, &data);
    }

    public void ShowBalloon(string title, string text)
    {
        var data = IconData(Native.NIF_INFO);
        Native.CopyTo(title, data.szInfoTitle, 64);
        Native.CopyTo(text, data.szInfo, 256);
        data.dwInfoFlags = Native.NIIF_INFO;
        Native.Shell_NotifyIconW(Native.NIM_MODIFY, &data);
    }

    /// <summary>Ukáže nabídku u kurzoru a vrátí číslo zvolené položky (0, když uživatel nic nezvolil).</summary>
    public int ShowMenu(IEnumerable<MenuItem> items)
    {
        var menu = Native.CreatePopupMenu();
        try
        {
            foreach (var item in items)
            {
                if (item.Text is null)
                    Native.AppendMenuW(menu, Native.MF_SEPARATOR, 0, null);
                else
                    Native.AppendMenuW(menu, Native.MF_STRING | (item.Checked ? Native.MF_CHECKED : 0), (nuint)item.Id, item.Text);
            }

            // Bez okna v popředí by nabídka nezmizela po kliknutí jinam (známá vlastnost Windows).
            Native.GetCursorPos(out var cursor);
            Native.SetForegroundWindow(Handle);
            var chosen = Native.TrackPopupMenu(menu, Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON, cursor.x, cursor.y, 0, Handle, 0);
            Native.PostMessageW(Handle, Native.WM_NULL, 0, 0);
            return chosen;
        }
        finally
        {
            Native.DestroyMenu(menu);
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowProc(nint window, uint message, nint wParam, nint lParam)
    {
        var tray = _instance;
        try
        {
            if (tray is not null)
            {
                if (message == RunPostedMessage)
                {
                    tray.RunPosted();
                    return 0;
                }

                if (message == TrayCallbackMessage)
                {
                    if (lParam is Native.WM_LBUTTONUP or Native.WM_RBUTTONUP)
                        tray.Clicked?.Invoke();
                    return 0;
                }

                if (message == tray._taskbarCreatedMessage && tray._taskbarCreatedMessage != 0)
                {
                    tray.AddIcon();
                    return 0;
                }
            }
        }
        catch
        {
            // Výjimka ven z okna by shodila proces; jedna nepovedená akce nevadí.
        }

        return Native.DefWindowProcW(window, message, wParam, lParam);
    }

    private void RunPosted()
    {
        while (_posted.TryDequeue(out var action))
            action();
    }

    private void AddIcon()
    {
        var data = IconData(Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP);
        Native.Shell_NotifyIconW(Native.NIM_ADD, &data);
    }

    private Native.NOTIFYICONDATAW IconData(uint flags)
    {
        var data = new Native.NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(Native.NOTIFYICONDATAW),
            hWnd = Handle,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _icon,
        };
        Native.CopyTo(_tip, data.szTip, 128);
        return data;
    }

    /// <summary>Ikonka z exe ve velikosti pro oznamovací oblast (podle DPI), jinak obecná ikonka Windows.</summary>
    private static nint LoadIcon(nint instance)
    {
        var width = Native.GetSystemMetrics(Native.SM_CXSMICON);
        var height = Native.GetSystemMetrics(Native.SM_CYSMICON);
        var icon = Native.LoadImageW(instance, Native.ApplicationIconId, Native.IMAGE_ICON, width, height, 0);
        return icon != 0 ? icon : Native.LoadImageW(0, Native.ApplicationIconId, Native.IMAGE_ICON, width, height, 0x8000);
    }

    public void Dispose()
    {
        var data = IconData(0);
        Native.Shell_NotifyIconW(Native.NIM_DELETE, &data);
        Native.DestroyIcon(_icon);
        _instance = null;
    }

    /// <summary>Pokračování po <c>await</c> pošle zpět do vlákna okna, kde žije všechno ostatní.</summary>
    private sealed class WindowContext(TrayWindow window) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => window.Post(() => callback(state));

        public override SynchronizationContext CreateCopy() => this;
    }
}
