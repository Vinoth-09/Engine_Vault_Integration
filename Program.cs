using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace VaultWindowsService
{
    /// <summary>
    /// Main entry point for the Vault Windows Service
    /// Supports both service mode and console mode for debugging
    /// </summary>
    static class Program
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                // Check if running in console mode (for debugging)
                if (Environment.UserInteractive || (args.Length > 0 && args[0].Equals("--console", StringComparison.OrdinalIgnoreCase)))
                {
                    RunAsConsole(args);
                }
                else
                {
                    RunAsService();
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Fatal error in main program");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Runs the application as a Windows service
        /// </summary>
        private static void RunAsService()
        {
            try
            {
                Logger.Info("Starting VaultWindowsService in service mode");

                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new VaultService()
                };

                ServiceBase.Run(ServicesToRun);
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Fatal error running as Windows service");
                throw;
            }
        }

        /// <summary>
        /// Runs the application in console mode for debugging
        /// </summary>
        /// <param name="args">Command line arguments</param>
        private static void RunAsConsole(string[] args)
        {
            try
            {
                Console.WriteLine("VaultWindowsService - Console Mode");
                Console.WriteLine("==================================");
                Console.WriteLine("Press 'Q' to quit, 'R' to force refresh, 'S' to show status");
                Console.WriteLine();

                Logger.Info("Starting VaultWindowsService in console mode");

                var service = new VaultService();
                
                // Start the service
                service.GetType().GetMethod("OnStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(service, new object[] { args });

                // Wait for user input
                var cancellationTokenSource = new CancellationTokenSource();
                var inputTask = Task.Run(() => HandleConsoleInput(service, cancellationTokenSource.Token));

                // Wait for cancellation
                cancellationTokenSource.Token.WaitHandle.WaitOne();

                Console.WriteLine("Stopping service...");
                
                // Stop the service
                service.GetType().GetMethod("OnStop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(service, null);

                Logger.Info("VaultWindowsService stopped in console mode");
                Console.WriteLine("Service stopped. Press any key to exit.");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Fatal error running in console mode");
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Handles console input for interactive commands
        /// </summary>
        /// <param name="service">Service instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private static async Task HandleConsoleInput(VaultService service, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var key = Console.ReadKey(true);
                    
                    switch (char.ToUpper(key.KeyChar))
                    {
                        case 'Q':
                            Console.WriteLine("Quit requested...");
                            return;

                        case 'R':
                            Console.WriteLine("Forcing configuration refresh...");
                            try
                            {
                                var result = await service.ForceConfigurationRefreshAsync();
                                Console.WriteLine($"Refresh result: {(result ? "Success" : "Failed")}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Refresh error: {ex.Message}");
                            }
                            break;

                        case 'S':
                            Console.WriteLine("Service Status:");
                            try
                            {
                                var status = service.GetServiceStatus();
                                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(status, Newtonsoft.Json.Formatting.Indented));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Status error: {ex.Message}");
                            }
                            break;

                        case 'C':
                            Console.WriteLine("Configuration Values:");
                            try
                            {
                                var configs = await service.GetAllConfigurationValuesAsync();
                                if (configs.Count > 0)
                                {
                                    foreach (var kvp in configs)
                                    {
                                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("  No configuration values available");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Configuration error: {ex.Message}");
                            }
                            break;

                        case 'H':
                        case '?':
                            Console.WriteLine("Available commands:");
                            Console.WriteLine("  Q - Quit");
                            Console.WriteLine("  R - Force refresh configuration");
                            Console.WriteLine("  S - Show service status");
                            Console.WriteLine("  C - Show configuration values");
                            Console.WriteLine("  H/? - Show this help");
                            break;

                        default:
                            // Ignore other keys
                            break;
                    }

                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in console input handler");
                Console.WriteLine($"Input handler error: {ex.Message}");
            }
        }
    }
}
