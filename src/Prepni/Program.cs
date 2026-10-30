namespace Prepni;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Dvě instance by přepisovaly stejné slovo dvakrát. Jméno bez předpony platí pro přihlášeného uživatele.
        using var single = new Mutex(true, "Prepni", out var first);
        if (!first)
            return;

        using var app = new App();
        app.Run();
    }
}
