using System;
using System.Configuration;
using System.ServiceProcess;
using System.Threading.Tasks;
using NLog;
using VaultWindowsService.Interfaces;
using VaultWindowsService.Models;
using VaultWindowsService.Services;

namespace VaultWindowsService
{
    /// <summary>
    /// Windows service implementation using Command pattern
    /// Manages Vault configuration retrieval and caching
    /// </summary>
    public partial class VaultService : ServiceBase
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        
        private IConfigurationProvider _configurationProvider;
        private VaultConfiguration _vaultConfiguration;
        private bool _isRunning;

        public VaultService()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Get service configuration from App.config
            this.ServiceName = ConfigurationManager.AppSettings["ServiceName"] ?? "VaultWindowsService";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
        }

        /// <summary>
        /// Service start command
        /// </summary>
        /// <param name="args">Service arguments</param>
        protected override void OnStart(string[] args)
        {
            try
            {
                Logger.Info("VaultService starting...");

                // Load configuration
                _vaultConfiguration = VaultConfiguration.FromAppConfig();
                
                if (!_vaultConfiguration.IsValid())
                {
                    throw new InvalidOperationException("Invalid Vault configuration");
                }

                // Initialize configuration provider
                _configurationProvider = new ConfigurationProvider(_vaultConfiguration);

                // Subscribe to events
                _configurationProvider.ConfigurationUpdated += OnConfigurationUpdated;
                _configurationProvider.ConfigurationRefreshFailed += OnConfigurationRefreshFailed;

                // Initialize asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        var initResult = await _configurationProvider.InitializeAsync();
                        if (initResult)
                        {
                            _isRunning = true;
                            Logger.Info("VaultService started successfully");
                            
                            // Log initial configuration metadata
                            var metadata = _configurationProvider.GetConfigurationMetadata();
                            Logger.Info($"Service initialized with configuration: {Newtonsoft.Json.JsonConvert.SerializeObject(metadata, Newtonsoft.Json.Formatting.Indented)}");
                        }
                        else
                        {
                            Logger.Error("Failed to initialize VaultService");
                            Stop();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error during VaultService initialization");
                        Stop();
                    }
                });

                Logger.Info("VaultService start command completed");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start VaultService");
                throw;
            }
        }

        /// <summary>
        /// Service stop command
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                Logger.Info("VaultService stopping...");
                
                _isRunning = false;

                // Unsubscribe from events
                if (_configurationProvider != null)
                {
                    _configurationProvider.ConfigurationUpdated -= OnConfigurationUpdated;
                    _configurationProvider.ConfigurationRefreshFailed -= OnConfigurationRefreshFailed;
                    
                    // Dispose configuration provider
                    _configurationProvider.Dispose();
                    _configurationProvider = null;
                }

                Logger.Info("VaultService stopped successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during VaultService stop");
            }
        }

        /// <summary>
        /// Service shutdown command
        /// </summary>
        protected override void OnShutdown()
        {
            try
            {
                Logger.Info("VaultService shutting down due to system shutdown");
                OnStop();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during VaultService shutdown");
            }
        }

        /// <summary>
        /// Handles configuration updated events
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="settings">Updated settings</param>
        private void OnConfigurationUpdated(object sender, System.Collections.Generic.Dictionary<string, object> settings)
        {
            try
            {
                Logger.Info($"Configuration updated with {settings.Count} settings");
                
                // Log some statistics about the update
                var metadata = _configurationProvider?.GetConfigurationMetadata();
                if (metadata != null)
                {
                    Logger.Debug($"Configuration metadata: {Newtonsoft.Json.JsonConvert.SerializeObject(metadata)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling configuration updated event");
            }
        }

        /// <summary>
        /// Handles configuration refresh failed events
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="exception">Exception that occurred</param>
        private void OnConfigurationRefreshFailed(object sender, Exception exception)
        {
            try
            {
                Logger.Error(exception, "Configuration refresh failed");
                
                // Check if we still have valid cached configuration
                var isAvailable = _configurationProvider?.IsConfigurationAvailable() ?? false;
                if (isAvailable)
                {
                    Logger.Info("Service continues to operate with cached configuration");
                }
                else
                {
                    Logger.Warn("No valid configuration available, service functionality may be limited");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error handling configuration refresh failed event");
            }
        }

        /// <summary>
        /// Gets the current service status for external monitoring
        /// </summary>
        /// <returns>Service status object</returns>
        public object GetServiceStatus()
        {
            try
            {
                return new
                {
                    ServiceName = this.ServiceName,
                    IsRunning = _isRunning,
                    Status = _isRunning ? "Running" : "Stopped",
                    ConfigurationAvailable = _configurationProvider?.IsConfigurationAvailable() ?? false,
                    ConfigurationMetadata = _configurationProvider?.GetConfigurationMetadata(),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting service status");
                return new
                {
                    ServiceName = this.ServiceName,
                    IsRunning = _isRunning,
                    Status = "Error",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Forces a configuration refresh (for administrative purposes)
        /// </summary>
        /// <returns>True if refresh was successful</returns>
        public async Task<bool> ForceConfigurationRefreshAsync()
        {
            try
            {
                if (_configurationProvider == null || !_isRunning)
                {
                    Logger.Warn("Cannot force refresh: service not running or configuration provider not available");
                    return false;
                }

                Logger.Info("Forcing configuration refresh");
                var result = await _configurationProvider.RefreshConfigurationAsync();
                
                if (result)
                {
                    Logger.Info("Forced configuration refresh completed successfully");
                }
                else
                {
                    Logger.Error("Forced configuration refresh failed");
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during forced configuration refresh");
                return false;
            }
        }

        /// <summary>
        /// Gets a specific configuration value (for external access)
        /// </summary>
        /// <param name="key">Configuration key</param>
        /// <returns>Configuration value</returns>
        public async Task<object> GetConfigurationValueAsync(string key)
        {
            try
            {
                if (_configurationProvider == null || !_isRunning)
                {
                    Logger.Warn("Cannot get configuration value: service not running or configuration provider not available");
                    return null;
                }

                return await _configurationProvider.GetConfigurationAsync(key);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error getting configuration value for key: {key}");
                return null;
            }
        }

        /// <summary>
        /// Gets all configuration values (for external access)
        /// </summary>
        /// <returns>All configuration values</returns>
        public async Task<System.Collections.Generic.Dictionary<string, object>> GetAllConfigurationValuesAsync()
        {
            try
            {
                if (_configurationProvider == null || !_isRunning)
                {
                    Logger.Warn("Cannot get configuration values: service not running or configuration provider not available");
                    return new System.Collections.Generic.Dictionary<string, object>();
                }

                return await _configurationProvider.GetAllConfigurationsAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error getting all configuration values");
                return new System.Collections.Generic.Dictionary<string, object>();
            }
        }
    }
}
