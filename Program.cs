using System;
using System.IO;
using System.ServiceProcess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Cert;
using VaultService.Models;
using VaultService.Services;

namespace VaultService
{
    public static class Program
    {
        private static IConfiguration _configuration;
        private static ServiceSettings _serviceSettings;
        private static VaultSettings _vaultSettings;
        private static LoggingSettings _loggingSettings;

        public static int Main(string[] args)
        {
            try
            {
                // Build configuration
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Bind settings
                _serviceSettings = new ServiceSettings();
                _configuration.GetSection("ServiceSettings").Bind(_serviceSettings);
                
                _vaultSettings = new VaultSettings();
                _configuration.GetSection("VaultSettings").Bind(_vaultSettings);
                
                _loggingSettings = new LoggingSettings();
                _configuration.GetSection("Logging").Bind(_loggingSettings);

                // Configure logging
                ConfigureLogging();

                Log.Information("Application starting up...");
                Log.Information("Service Name: {ServiceName}", _serviceSettings.ServiceName);
                Log.Information("Vault Address: {VaultAddress}", _vaultSettings.VaultAddress);

                // Setup DI
                var serviceProvider = ConfigureServices();

                // Run in console mode if debugger is attached or console mode is requested
                if (Environment.UserInteractive || Debugger.IsAttached)
                {
                    Log.Information("Running in console mode");
                    RunAsConsole(serviceProvider);
                }
                else
                {
                    Log.Information("Running as a Windows Service");
                    var servicesToRun = new ServiceBase[]
                    {
                        serviceProvider.GetRequiredService<VaultService>()
                    };
                    ServiceBase.Run(servicesToRun);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register configuration
            services.Configure<VaultSettings>(_configuration.GetSection("VaultSettings"));
            services.Configure<ServiceSettings>(_configuration.GetSection("ServiceSettings"));
            services.Configure<CacheSettings>(_configuration.GetSection("CacheSettings"));
            services.Configure<LoggingSettings>(_configuration.GetSection("Logging"));

            // Register Vault client
            services.AddSingleton<IVaultClient>(sp =>
            {
                var settings = new VaultClientSettings(_vaultSettings.VaultAddress, 
                    new CertAuthMethodInfo())
                {
                    // Configure additional Vault client settings here
                };

                if (!string.IsNullOrEmpty(_vaultSettings.VaultNamespace))
                {
                    settings.Namespace = _vaultSettings.VaultNamespace;
                }

                return new VaultClient(settings);
            });

            // Register services
            services.AddSingleton<VaultService>();
            services.AddSingleton<IConfiguration>(_configuration);
            services.AddSingleton(_serviceSettings);
            services.AddSingleton(_vaultSettings);

            // Add hosted services if needed
            // services.AddHostedService<SomeBackgroundService>();

            return services.BuildServiceProvider();
        }

        private static void ConfigureLogging()
        {
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: _loggingSettings.Console.OutputTemplate);

            if (_loggingSettings.Console.Enabled)
            {
                loggerConfiguration.WriteTo.Console(
                    outputTemplate: _loggingSettings.Console.OutputTemplate);
            }

            if (!string.IsNullOrEmpty(_loggingSettings.File.Path))
            {
                loggerConfiguration.WriteTo.File(
                    _loggingSettings.File.Path,
                    rollingInterval: Enum.Parse<RollingInterval>(_loggingSettings.File.RollingInterval),
                    retainedFileCountLimit: _loggingSettings.File.RetainedFileCountLimit,
                    fileSizeLimitBytes: _loggingSettings.File.FileSizeLimitBytes,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }

        private static void RunAsConsole(IServiceProvider serviceProvider)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Log.Information("Shutting down...");
                Environment.Exit(0);
            };

            var service = serviceProvider.GetRequiredService<VaultService>();
            service.OnStart(Array.Empty<string>());

            Console.WriteLine("Press Ctrl+C to stop the service");
            Thread.Sleep(Timeout.Infinite);
        }
    }
}
