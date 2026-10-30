namespace Prepni;

/// <summary>
/// Označený text přes schránku: pošleme Ctrl+C, přečteme ho a schránku vrátíme, jak byla. Když se schránka
/// nezmění, nic označené nebylo. Čekání je asynchronní: háček klávesnice běží v tomto vlákně a musí dostat
/// slovo i během něj, jinak by Windows zdržely každý stisk.
/// </summary>
internal static class Selection
{
    public static async Task<string?> CopyAsync(nint owner)
    {
        var saved = ClipboardSnapshot.Take(owner);
        var before = Native.GetClipboardSequenceNumber();

        await Retyper.ShortcutAsync(Native.VK_C);
        if (!await ChangedSinceAsync(before))
            return null;

        string? text = null;
        if (Clipboard.Open(owner))
        {
            try
            {
                if (!IsWholeLineCopy())
                    text = Clipboard.ReadText();
            }
            finally
            {
                Native.CloseClipboard();
            }
        }

        saved.Restore(owner);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Vloží text přes schránku (víc řádků; psaný Enter by v chatu odeslal zprávu) a pak ji vrátí.</summary>
    public static async Task PasteAsync(nint owner, string text)
    {
        var saved = ClipboardSnapshot.Take(owner);
        Clipboard.WriteText(owner, text);
        await Retyper.ShortcutAsync(Native.VK_V);

        // Aplikace si text ze schránky vezme až za chvíli; vrátit ji hned by vložilo původní obsah.
        await Task.Delay(400);
        saved.Restore(owner);
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
    private static bool IsWholeLineCopy()
    {
        if (Clipboard.Has("VisualStudioEditorOperationsLineCutCopyClipboardTag") || Clipboard.Has("MSDEVLineSelect"))
            return true;

        var json = Clipboard.ReadNamed("vscode-editor-data");
        return json?.Replace(" ", "").Contains(@"""isFromEmptySelection"":true") == true;
    }
}
