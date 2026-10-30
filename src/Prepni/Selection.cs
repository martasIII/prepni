using System.Runtime.InteropServices;
using System.Text;

namespace Prepni;

/// <summary>
/// Označený text přes schránku: pošleme Ctrl+C, přečteme ho a schránku vrátíme, jak byla. Když se schránka
/// nezmění, nic označené nebylo. Čekání je asynchronní: háček klávesnice běží v tomto vlákně a musí dostat
/// slovo i během něj, jinak by Windows zdržely každý stisk.
/// </summary>
internal static class Selection
{
    public static async Task<string?> CopyAsync()
    {
        var saved = ClipboardSnapshot.Take();
        var before = Native.GetClipboardSequenceNumber();

        Retyper.Shortcut(Native.VK_C);
        if (!await ChangedSinceAsync(before))
            return null;

        string? text = null;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is not null && !IsWholeLineCopy(data) && data.GetDataPresent(DataFormats.UnicodeText))
                text = data.GetData(DataFormats.UnicodeText) as string;
        }
        catch (ExternalException)
        {
            // Schránku zrovna drží jiná aplikace; bez textu nic neopravíme.
        }

        saved.Restore();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Vloží text přes schránku (víc řádků; psaný Enter by v chatu odeslal zprávu) a pak ji vrátí.</summary>
    public static async Task PasteAsync(string text)
    {
        var saved = ClipboardSnapshot.Take();
        Clipboard.SetDataObject(text, copy: true, retryTimes: 5, retryDelay: 50);
        Retyper.Shortcut(Native.VK_V);

        // Aplikace si text ze schránky vezme až za chvíli; vrátit ji hned by vložilo původní obsah.
        await Task.Delay(400);
        saved.Restore();
    }

    private static async Task<bool> ChangedSinceAsync(uint sequence)
    {
        for (var i = 0; i < 25; i++)
        {
            await Task.Delay(20);
            if (Native.GetClipboardSequenceNumber() != sequence)
            {
                // Aplikace mohla zapsat jen první formát; ostatní dopíše hned za ním.
                await Task.Delay(30);
                return true;
            }
        }

        return false;
    }

    /// <summary>Visual Studio a VS Code bez označení kopírují celý řádek; ten bychom napsali podruhé.</summary>
    private static bool IsWholeLineCopy(IDataObject data)
    {
        if (data.GetDataPresent("VisualStudioEditorOperationsLineCutCopyClipboardTag") || data.GetDataPresent("MSDEVLineSelect"))
            return true;

        if (!data.GetDataPresent("vscode-editor-data"))
            return false;

        var json = data.GetData("vscode-editor-data") switch
        {
            string s => s,
            MemoryStream stream => Encoding.UTF8.GetString(stream.ToArray()),
            _ => null,
        };
        return json?.Replace(" ", "").Contains(@"""isFromEmptySelection"":true") == true;
    }
}

/// <summary>Kopie schránky, aby se dala po půjčení vrátit.</summary>
internal sealed class ClipboardSnapshot
{
    private readonly DataObject? _data;

    private ClipboardSnapshot(DataObject? data) => _data = data;

    public static ClipboardSnapshot Take()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null)
                return new ClipboardSnapshot(null);

            var copy = new DataObject();
            foreach (var format in data.GetFormats(autoConvert: false))
            {
                try
                {
                    if (data.GetData(format, autoConvert: false) is { } value)
                        copy.SetData(format, autoConvert: false, value);
                }
                catch (Exception ex) when (ex is ExternalException or NotSupportedException or InvalidOperationException)
                {
                    // Některé formáty (vlastní objekty jiných aplikací) přečíst nejde; vrátíme aspoň ostatní.
                }
            }

            return new ClipboardSnapshot(copy);
        }
        catch (ExternalException)
        {
            return new ClipboardSnapshot(null);
        }
    }

    public void Restore()
    {
        try
        {
            if (_data is null || _data.GetFormats().Length == 0)
                Clipboard.Clear();
            else
                Clipboard.SetDataObject(_data, copy: true, retryTimes: 5, retryDelay: 50);
        }
        catch (ExternalException)
        {
            // Schránku drží jiná aplikace; necháme ji být.
        }
    }
}
