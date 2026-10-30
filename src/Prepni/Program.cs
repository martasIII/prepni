namespace Prepni;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Dvě instance by přepisovaly stejné slovo dvakrát.
        using var single = new Mutex(true, @"Local\Prepni", out var first);
        if (!first)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
