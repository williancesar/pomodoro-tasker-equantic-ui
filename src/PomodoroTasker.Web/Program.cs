using eQuantic.UI.Primitives;
using eQuantic.UI.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUI(options =>
{
    options.ScanAssembly(typeof(Program).Assembly)
           .UseTheme(new PomodoroTasker.Web.Theme.TaskerTheme())
           .ConfigureHtmlShell(shell => shell.SetTitle("pomodoro-tasker"));
});

var app = builder.Build();

// The interface is PT-BR only; the culture also sets how dates and numbers are written.
app.UseRequestLocalization(options =>
{
    options.SetDefaultCulture("pt-BR");
    options.AddSupportedUICultures("pt-BR");
    options.AddSupportedCultures("pt-BR");
});

app.UseStaticFiles();
app.UseServerActions();
app.MapUI();

app.Run();
