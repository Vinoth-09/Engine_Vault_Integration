using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VaultWindowsService.Interfaces
{
    /// <summary>
    /// Provider pattern interface for configuration management
    /// Orchestrates Vault client and cache manager interactions
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// Initializes the configuration provider
        /// </summary>
        /// <returns>True if initialization successful</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// Gets a configuration value by key
        /// </summary>
        /// <param name="key">Configuration key</param>
        /// <returns>Configuration value</returns>
        Task<object> GetConfigurationAsync(string key);

        /// <summary>
        /// Gets all configuration values
        /// </summary>
        /// <returns>Dictionary of all configuration values</returns>
        Task<Dictionary<string, object>> GetAllConfigurationsAsync();

        /// <summary>
        /// Refreshes configuration from Vault
        /// </summary>
        /// <returns>True if refresh successful</returns>
        Task<bool> RefreshConfigurationAsync();

        /// <summary>
        /// Checks if configuration is available and valid
        /// </summary>
        /// <returns>True if configuration is available</returns>
        bool IsConfigurationAvailable();

        /// <summary>
        /// Gets configuration metadata
        /// </summary>
        /// <returns>Configuration metadata object</returns>
        object GetConfigurationMetadata();

        /// <summary>
        /// Event fired when configuration is updated
        /// </summary>
        event EventHandler<Dictionary<string, object>> ConfigurationUpdated;

        /// <summary>
        /// Event fired when configuration refresh fails
        /// </summary>
        event EventHandler<Exception> ConfigurationRefreshFailed;

        /// <summary>
        /// Disposes resources
        /// </summary>
        void Dispose();
    }
}
