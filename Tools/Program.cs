using McMaster.Extensions.CommandLineUtils;
using Tools.Commands;

namespace Tools;

/// <summary>Точка входа CLI UltraDecompiler.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var app = new CommandLineApplication
        {
            Name = "udc",
            Description = "Инструменты UltraDecompiler: декомпиляция DOS EXE и разбор OMF .LIB",
        };

        app.HelpOption(inherited: true);

        DecompileCommand.Configure(app);
        DecompileMatchCommand.Configure(app);
        LibCommand.Configure(app);

        app.OnExecute(() =>
        {
            app.ShowHelp();
            return 1;
        });

        return app.Execute(args);
    }
}
