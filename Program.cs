using System.Text;
using SiriusCleaner.Cleaning;
using SiriusCleaner.Core;
using SiriusCleaner.UI;

namespace SiriusCleaner;

internal static class Program
{
    private const string InstanceMutexName = @"Local\SiriusCleaner.Instance";

    [STAThread]
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        bool Has(string flag) => args.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

        if (Has("--help") || Has("/?")) return ShowHelp();
        if (Has("--clean") || Has("--analyze")) return RunHeadless(dryRun: Has("--analyze"));

        using var mutex = new Mutex(false, InstanceMutexName);
        if (!AcquireInstance(mutex, Has("--wait-instance")))
        {
            // Já existe uma janela aberta: pede para ela aparecer e sai.
            try
            {
                using var show = EventWaitHandle.OpenExisting(MainForm.ShowEventName);
                show.Set();
            }
            catch { }
            return 0;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => Logger.Error("Erro não tratado", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Error("Erro fatal", e.ExceptionObject as Exception);

            var settings = AppSettings.Load();
            Application.Run(new MainForm(settings, startHidden: Has("--tray")));
            return 0;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static bool AcquireInstance(Mutex mutex, bool wait)
    {
        try
        {
            // --wait-instance: reinício como administrador; aguarda a instância anterior fechar.
            return mutex.WaitOne(wait ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }

    /// <summary>
    /// Modo linha de comando (ideal para o Agendador de Tarefas):
    /// limpa as categorias marcadas na interface, registra no log e sai.
    /// </summary>
    private static int RunHeadless(bool dryRun)
    {
        bool hasConsole = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
        void Print(string text)
        {
            if (hasConsole) Console.WriteLine(text);
        }

        Logger.MessageLogged += Print;
        try
        {
            Print("");
            var settings = AppSettings.Load();
            var categories = CleanupCatalog.All
                .Where(c => c.ConfirmationMessage == null && settings.IsCategoryEnabled(c.Id, c.DefaultEnabled))
                .ToList();

            Logger.Info($"{(dryRun ? "Análise" : "Limpeza")} via linha de comando ({categories.Count} categorias)");
            var report = new JunkCleaner(settings).RunAsync(categories, dryRun, null, CancellationToken.None).GetAwaiter().GetResult();

            foreach (var result in report.Results)
                Logger.Info($"  {result.Category.Name,-45} {Format.Bytes(result.Bytes),10}  {result.StatusText(dryRun)}");
            Logger.Info(report.Summary());
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error("Falha na limpeza via linha de comando", ex);
            return 1;
        }
        finally
        {
            Logger.MessageLogged -= Print;
        }
    }

    private static int ShowHelp()
    {
        const string help =
            "Sirius Cleaner\n\n" +
            "  SiriusCleaner.exe             Abre a interface\n" +
            "  SiriusCleaner.exe --tray      Inicia minimizado na bandeja\n" +
            "  SiriusCleaner.exe --analyze   Mostra o que seria limpo, sem apagar nada\n" +
            "  SiriusCleaner.exe --clean     Limpa as categorias marcadas e sai\n";

        if (NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS))
            Console.WriteLine("\n" + help);
        else
            MessageBox.Show(help, AppInfo.Name);
        return 0;
    }
}
