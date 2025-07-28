using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;
using VaultWindowsService.Interfaces;
using VaultWindowsService.Models;
using VaultWindowsService.Exceptions;

namespace VaultWindowsService.Services
{
    /// <summary>
    /// Provider pattern implementation for configuration management
    /// Orchestrates Vault client and cache manager interactions
    /// </summary>
    public class ConfigurationProvider : IConfigurationProvider
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly VaultConfiguration _configuration;
        private readonly IVaultClient _vaultClient;
        private readonly ICacheManager _cacheManager;
        private bool _isInitialized;
        private bool _disposed;

        public event EventHandler<Dictionary<string, object>> ConfigurationUpdated;
        public event EventHandler<Exception> ConfigurationRefreshFailed;

        public ConfigurationProvider(VaultConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Initialize Vault client
            _vaultClient = new VaultClientService(_configuration);
            
            // Initialize cache manager (singleton)
            _cacheManager = CacheManager.Initialize(_configuration, _vaultClient);
            
            Logger.Info("ConfigurationProvider created");
        }

        /// <summary>
        /// Initializes the configuration provider
        /// </summary>
        /// <returns>True if initialization successful</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (_isInitialized)
                {
                    Logger.Debug("ConfigurationProvider already initialized");
                    return true;
                }

                Logger.Info("Initializing ConfigurationProvider");

                // Validate configuration
                if (!_configuration.IsValid())
                {
                    throw new InvalidOperationException("Invalid configuration provided");
                }

                // Authenticate with Vault
                var authResult = await _vaultClient.AuthenticateAsync();
                if (!authResult)
                {
                    throw new VaultServiceException("Failed to authenticate with Vault", _configuration.VaultUrl, "Initialize");
                }

                // Check if we have valid cached data
                var cachedSettings = _cacheManager.GetCachedSettings();
                if (cachedSettings == null || !_cacheManager.IsCacheValid())
                {
                    Logger.Info("No valid cache found, performing initial refresh from Vault");
                    await RefreshConfigurationAsync();
                }
                else
                {
                    Logger.Info($"Using existing valid cache with {cachedSettings.Count} settings");
                }

                _isInitialized = true;
                Logger.Info("ConfigurationProvider initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize ConfigurationProvider");
                _isInitialized = false;
                throw;
            }
        }

        /// <summary>
        /// Gets a configuration value by key
        /// </summary>
        /// <param name="key">Configuration key</param>
        /// <returns>Configuration value</returns>
        public async Task<object> GetConfigurationAsync(string key)
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                // Try to get from cache first
                var cachedValue = _cacheManager.GetSetting(key);
                if (cachedValue != null)
                {
                    Logger.Debug($"Retrieved configuration value for key '{key}' from cache");
                    return cachedValue;
                }

                // If not in cache and cache is valid, the key doesn't exist
                if (_cacheManager.IsCacheValid())
                {
                    Logger.Debug($"Configuration key '{key}' not found in valid cache");
                    return null;
                }

                // Cache is invalid, refresh and try again
                Logger.Info("Cache is invalid, refreshing from Vault");
                await RefreshConfigurationAsync();
                
                cachedValue = _cacheManager.GetSetting(key);
                Logger.Debug($"Retrieved configuration value for key '{key}' after cache refresh");
                return cachedValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to get configuration for key: {key}");
                throw;
            }
        }

        /// <summary>
        /// Gets all configuration values
        /// </summary>
        /// <returns>Dictionary of all configuration values</returns>
        public async Task<Dictionary<string, object>> GetAllConfigurationsAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                // Try to get from cache first
                var cachedSettings = _cacheManager.GetCachedSettings();
                if (cachedSettings != null && _cacheManager.IsCacheValid())
                {
                    Logger.Debug($"Retrieved {cachedSettings.Count} configuration values from cache");
                    return cachedSettings;
                }

                // Cache is invalid, refresh from Vault
                Logger.Info("Cache is invalid, refreshing all configurations from Vault");
                await RefreshConfigurationAsync();
                
                cachedSettings = _cacheManager.GetCachedSettings();
                Logger.Info($"Retrieved {cachedSettings?.Count ?? 0} configuration values after refresh");
                return cachedSettings ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get all configurations");
                throw;
            }
        }

        /// <summary>
        /// Refreshes configuration from Vault
        /// </summary>
        /// <returns>True if refresh successful</returns>
        public async Task<bool> RefreshConfigurationAsync()
        {
            try
            {
                Logger.Info("Starting configuration refresh from Vault");

                // Get all secrets from Vault
                var secrets = await _vaultClient.GetAllSecretsAsync();
                
                // Update cache
                var updateResult = await _cacheManager.UpdateCacheAsync(secrets);
                
                if (updateResult)
                {
                    Logger.Info($"Configuration refreshed successfully with {secrets.Count} settings");
                    
                    // Fire configuration updated event
                    ConfigurationUpdated?.Invoke(this, secrets);
                    
                    return true;
                }
                else
                {
                    Logger.Error("Failed to update cache during configuration refresh");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh configuration from Vault");
                
                // Fire configuration refresh failed event
                ConfigurationRefreshFailed?.Invoke(this, ex);
                
                return false;
            }
        }

        /// <summary>
        /// Checks if configuration is available and valid
        /// </summary>
        /// <returns>True if configuration is available</returns>
        public bool IsConfigurationAvailable()
        {
            try
            {
                if (!_isInitialized)
                {
                    return false;
                }

                // Check if Vault client is healthy
                var vaultHealthy = _vaultClient.IsHealthyAsync().Result;
                
                // Check if cache is valid or if we can reach Vault
                var cacheValid = _cacheManager.IsCacheValid();
                
                var isAvailable = cacheValid || vaultHealthy;
                Logger.Debug($"Configuration availability check: Cache valid={cacheValid}, Vault healthy={vaultHealthy}, Available={isAvailable}");
                
                return isAvailable;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to check configuration availability");
                return false;
            }
        }

        /// <summary>
        /// Gets configuration metadata
        /// </summary>
        /// <returns>Configuration metadata object</returns>
        public object GetConfigurationMetadata()
        {
            try
            {
                var cacheStats = _cacheManager.GetCacheStatistics();
                var vaultHealthy = _vaultClient.IsHealthyAsync().Result;
                
                return new
                {
                    IsInitialized = _isInitialized,
                    IsAvailable = IsConfigurationAvailable(),
                    VaultConfiguration = new
                    {
                        VaultUrl = _configuration.VaultUrl,
                        VaultNamespace = _configuration.VaultNamespace,
                        SecretPath = _configuration.SecretPath,
                        CacheRefreshInterval = _configuration.GetCacheRefreshInterval(),
                        CacheFilePath = _configuration.CacheFilePath
                    },
                    VaultHealth = new
                    {
                        IsHealthy = vaultHealthy,
                        LastChecked = DateTime.UtcNow
                    },
                    CacheStatistics = cacheStats,
                    LastRefreshAttempt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get configuration metadata");
                return new
                {
                    Error = ex.Message,
                    IsInitialized = _isInitialized,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _vaultClient?.Dispose();
                    _cacheManager?.Dispose();
                    
                    Logger.Info("ConfigurationProvider disposed");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during ConfigurationProvider disposal");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
