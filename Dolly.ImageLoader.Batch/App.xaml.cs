using System.IO;
using System.Windows;
using Dolly.ImageLoader.Batch.ViewModels;
using Dolly.ImageLoader.Batch.Views;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Infrastructure.Configuration;
using Dolly.ImageLoader.Infrastructure.Services;
using Dolly.ImageLoader.Wpf.Shared.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Batch;

public partial class App : Application
{
    public const int ExitNoChanges = 0;
    public const int ExitCancelledOrError = 1;
    public const int ExitChangesSaved = 2;

    private IHost? _host;
    private BatchLoaderViewModel? _viewModel;
    private int _exitCode = ExitNoChanges;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var groupId = ParseGroupId(e.Args, out var argsValid);
        if (!argsValid)
        {
            MessageBox.Show("Missing value for --group argument.", "Dolly Batch Loader",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(ExitCancelledOrError);
            return;
        }

        try
        {
            await StartAsync(groupId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start Batch Loader: {ex.Message}", "Dolly Batch Loader",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(ExitCancelledOrError);
        }
    }

    private static string? ParseGroupId(string[] args, out bool valid)
    {
        valid = true;
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--group", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
            {
                valid = false;
                return null;
            }

            return args[i + 1].Trim();
        }

        return null;
    }

    private async Task StartAsync(string? groupId)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
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

                services.AddTransient<BatchLoaderViewModel>();
                services.AddTransient<BatchLoaderWindow>();
            })
            .Build();

        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Dolly Batch Loader starting.");

        var batchWindow = _host.Services.GetRequiredService<BatchLoaderWindow>();
        _viewModel = (BatchLoaderViewModel)batchWindow.DataContext;
        MainWindow = batchWindow;
        batchWindow.Closed += (_, _) => _exitCode = DetermineExitCode();
        batchWindow.Show();

        if (groupId is not null)
        {
            await _viewModel.InitializeForGroupAsync(groupId);
            if (_viewModel.GroupLoadFailed)
            {
                MessageBox.Show(_viewModel.StatusMessage, "Dolly Batch Loader",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private int DetermineExitCode()
    {
        if (_viewModel is null)
        {
            return ExitCancelledOrError;
        }

        if (_viewModel.ChangesSavedThisSession)
        {
            return ExitChangesSaved;
        }

        return _viewModel.ClosedWithDiscardedChanges || _viewModel.GroupLoadFailed
            ? ExitCancelledOrError
            : ExitNoChanges;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Must be set before the first await so the runtime sees it.
        if (e.ApplicationExitCode == 0)
        {
            e.ApplicationExitCode = _exitCode;
        }

        if (_host is not null)
        {
            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Dolly Batch Loader shutting down.");

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
