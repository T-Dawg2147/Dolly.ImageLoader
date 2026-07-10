using System.IO;
using System.Windows;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Infrastructure.Configuration;
using Dolly.ImageLoader.Infrastructure.Services;
using Dolly.ImageLoader.Wpf.Shared.ViewModels;
using Dolly.ImageLoader.Wpf.ViewModels;
using Dolly.ImageLoader.Wpf.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddDebug();
                });

                services.AddInfrastructure(context.Configuration);

                services.AddSingleton<IImageSlotViewModelFactory, ImageSlotViewModelFactory>();
                services.AddSingleton<IImageNormalizationService, ImageNormalizationService>();
                services.AddSingleton<IWorkingDirectoryCleanupService, WorkingDirectoryCleanupService>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application starting.");

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Application shutting down.");

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}