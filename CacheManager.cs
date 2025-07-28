using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using NLog;
using VaultWindowsService.Interfaces;
using VaultWindowsService.Models;
using VaultWindowsService.Exceptions;

namespace VaultWindowsService.Services
{
    /// <summary>
    /// Singleton pattern implementation for managing application settings cache
    /// Uses memory-mapped files for cross-application domain caching
    /// Provides automatic refresh capabilities
    /// </summary>
    public sealed class CacheManager : ICacheManager, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly object LockObject = new object();
        private static volatile CacheManager _instance;
        
        private readonly VaultConfiguration _configuration;
        private readonly IVaultClient _vaultClient;
        private readonly System.Timers.Timer _refreshTimer;
        private readonly string _cacheFilePath;
        private readonly string _memoryMappedFileName;
        private readonly Mutex _cacheMutex;
        
        private CacheEntry _currentCache;
        private bool _disposed;

        /// <summary>
        /// Gets the singleton instance of CacheManager
        /// </summary>
        public static CacheManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (LockObject)
                    {
                        if (_instance == null)
                        {
                            throw new InvalidOperationException("CacheManager must be initialized before accessing Instance");
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes the singleton instance
        /// </summary>
        /// <param name="configuration">Vault configuration</param>
        /// <param name="vaultClient">Vault client instance</param>
        /// <returns>CacheManager instance</returns>
        public static CacheManager Initialize(VaultConfiguration configuration, IVaultClient vaultClient)
        {
            if (_instance == null)
            {
                lock (LockObject)
                {
                    if (_instance == null)
                    {
                        _instance = new CacheManager(configuration, vaultClient);
                    }
                }
            }
            return _instance;
        }

        private CacheManager(VaultConfiguration configuration, IVaultClient vaultClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _vaultClient = vaultClient ?? throw new ArgumentNullException(nameof(vaultClient));
            
            _cacheFilePath = _configuration.CacheFilePath;
            _memoryMappedFileName = "VaultWindowsService_Cache";
            _cacheMutex = new Mutex(false, "VaultWindowsService_CacheMutex");

            // Ensure cache directory exists
            var cacheDirectory = Path.GetDirectoryName(_cacheFilePath);
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
                Logger.Info($"Created cache directory: {cacheDirectory}");
            }

            // Initialize refresh timer
            _refreshTimer = new System.Timers.Timer(_configuration.GetCacheRefreshInterval().TotalMilliseconds);
            _refreshTimer.Elapsed += OnRefreshTimerElapsed;
            _refreshTimer.AutoReset = true;

            // Load existing cache
            LoadCacheFromFile();

            Logger.Info($"CacheManager initialized with refresh interval: {_configuration.CacheRefreshIntervalMinutes} minutes");
        }

        /// <summary>
        /// Gets cached application settings
        /// </summary>
        /// <returns>Dictionary of cached settings or null if cache is invalid/expired</returns>
        public Dictionary<string, object> GetCachedSettings()
        {
            try
            {
                _cacheMutex.WaitOne();

                if (_currentCache == null || !_currentCache.IsValid())
                {
                    Logger.Debug("Cache is invalid or expired, attempting to load from file");
                    LoadCacheFromFile();
                }

                if (_currentCache != null && _currentCache.IsValid())
                {
                    _currentCache.RecordAccess();
                    UpdateMemoryMappedFile();
                    Logger.Debug($"Retrieved {_currentCache.Data.Count} settings from cache");
                    return new Dictionary<string, object>(_currentCache.Data);
                }

                Logger.Warn("No valid cache available");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get cached settings");
                throw new CacheException("Failed to get cached settings", _cacheFilePath, "GetCachedSettings", ex);
            }
            finally
            {
                _cacheMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Updates the cache with new settings
        /// </summary>
        /// <param name="settings">Settings to cache</param>
        /// <returns>True if cache update successful</returns>
        public async Task<bool> UpdateCacheAsync(Dictionary<string, object> settings)
        {
            try
            {
                _cacheMutex.WaitOne();

                Logger.Info($"Updating cache with {settings?.Count ?? 0} settings");

                var newCache = CacheEntry.Create(
                    settings ?? new Dictionary<string, object>(),
                    _configuration.GetCacheRefreshInterval());

                _currentCache = newCache;

                // Save to file
                await SaveCacheToFileAsync();

                // Update memory-mapped file
                UpdateMemoryMappedFile();

                // Start refresh timer if not already running
                if (!_refreshTimer.Enabled)
                {
                    _refreshTimer.Start();
                    Logger.Info("Cache refresh timer started");
                }

                Logger.Info($"Cache updated successfully with {settings?.Count ?? 0} settings, expires at {newCache.ExpiresAt}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update cache");
                throw new CacheException("Failed to update cache", _cacheFilePath, "UpdateCache", ex);
            }
            finally
            {
                _cacheMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Checks if the cache is valid and not expired
        /// </summary>
        /// <returns>True if cache is valid</returns>
        public bool IsCacheValid()
        {
            try
            {
                _cacheMutex.WaitOne();
                return _currentCache?.IsValid() == true;
            }
            finally
            {
                _cacheMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Gets the cache expiration time
        /// </summary>
        /// <returns>DateTime when cache expires</returns>
        public DateTime GetCacheExpiration()
        {
            try
            {
                _cacheMutex.WaitOne();
                return _currentCache?.ExpiresAt ?? DateTime.MinValue;
            }
            finally
            {
                _cacheMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Forces cache refresh from Vault
        /// </summary>
        /// <returns>True if refresh successful</returns>
        public async Task<bool> RefreshCacheAsync()
        {
            try
            {
                Logger.Info("Starting cache refresh from Vault");

                var secrets = await _vaultClient.GetAllSecretsAsync();
                await UpdateCacheAsync(secrets);

                Logger.Info("Cache refresh completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to refresh cache from Vault");
                return false;
            }
        }

        /// <summary>
        /// Gets a specific setting from cache
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <returns>Setting value or null if not found</returns>
        public object GetSetting(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var settings = GetCachedSettings();
            return settings?.TryGetValue(key, out var value) == true ? value : null;
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public void ClearCache()
        {
            try
            {
                _cacheMutex.WaitOne();

                _currentCache = null;
                
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }

                ClearMemoryMappedFile();
                
                Logger.Info("Cache cleared successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clear cache");
                throw new CacheException("Failed to clear cache", _cacheFilePath, "ClearCache", ex);
            }
            finally
            {
                _cacheMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Cache statistics object</returns>
        public object GetCacheStatistics()
        {
            try
            {
                _cacheMutex.WaitOne();

                if (_currentCache == null)
                {
                    return new { Status = "No cache available" };
                }

                return new
                {
                    Status = _currentCache.IsValid() ? "Valid" : "Expired",
                    CreatedAt = _currentCache.CreatedAt,
                    ExpiresAt = _currentCache.ExpiresAt,
                    Version = _currentCache.Version,
                    Source = _currentCache.Source,
                    SettingsCount = _currentCache.Data?.Count ?? 0,
                    AccessCount = _currentCache.AccessCount,
                    LastAccessedAt = _currentCache.LastAccessedAt,
                    IntegrityValid = _currentCache.ValidateIntegrity(),
                    CacheFilePath = _cacheFilePath,
                    RefreshInterval = _configuration.GetCacheRefreshInterval()
                };
            }
            finally
            {
                _cacheMutex.ReleaseMutex();
            }
        }

        private async void OnRefreshTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Logger.Info("Automatic cache refresh triggered");
                await RefreshCacheAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Automatic cache refresh failed");
            }
        }

        private void LoadCacheFromFile()
        {
            try
            {
                if (!File.Exists(_cacheFilePath))
                {
                    Logger.Debug("Cache file does not exist");
                    return;
                }

                var json = File.ReadAllText(_cacheFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Logger.Debug("Cache file is empty");
                    return;
                }

                _currentCache = JsonConvert.DeserializeObject<CacheEntry>(json);
                
                if (_currentCache != null)
                {
                    if (!_currentCache.ValidateIntegrity())
                    {
                        Logger.Warn("Cache integrity validation failed, clearing cache");
                        _currentCache = null;
                        return;
                    }

                    Logger.Info($"Loaded cache from file: {_currentCache.Data?.Count ?? 0} settings, expires at {_currentCache.ExpiresAt}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load cache from file");
                _currentCache = null;
            }
        }

        private async Task SaveCacheToFileAsync()
        {
            try
            {
                if (_currentCache == null)
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(_currentCache, Formatting.Indented);
                await File.WriteAllTextAsync(_cacheFilePath, json, Encoding.UTF8);
                
                Logger.Debug($"Cache saved to file: {_cacheFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save cache to file");
                throw;
            }
        }

        private void UpdateMemoryMappedFile()
        {
            try
            {
                if (_currentCache == null)
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(_currentCache, Formatting.None);
                var data = Encoding.UTF8.GetBytes(json);

                using (var mmf = MemoryMappedFile.CreateOrOpen(_memoryMappedFileName, Math.Max(data.Length, 1024 * 1024)))
                using (var accessor = mmf.CreateViewAccessor(0, data.Length))
                {
                    accessor.WriteArray(0, data, 0, data.Length);
                }

                Logger.Debug("Memory-mapped file updated");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to update memory-mapped file");
            }
        }

        private void ClearMemoryMappedFile()
        {
            try
            {
                using (var mmf = MemoryMappedFile.CreateOrOpen(_memoryMappedFileName, 1024))
                using (var accessor = mmf.CreateViewAccessor(0, 1024))
                {
                    for (int i = 0; i < 1024; i++)
                    {
                        accessor.Write(i, (byte)0);
                    }
                }

                Logger.Debug("Memory-mapped file cleared");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to clear memory-mapped file");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _cacheMutex?.Dispose();
                _disposed = true;
                Logger.Debug("CacheManager disposed");
            }
        }
    }
}
