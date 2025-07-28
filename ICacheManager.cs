using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VaultWindowsService.Interfaces
{
    /// <summary>
    /// Singleton pattern interface for managing application settings cache
    /// Provides cross-application domain caching capabilities
    /// </summary>
    public interface ICacheManager
    {
        /// <summary>
        /// Gets cached application settings
        /// </summary>
        /// <returns>Dictionary of cached settings or null if cache is invalid/expired</returns>
        Dictionary<string, object> GetCachedSettings();

        /// <summary>
        /// Updates the cache with new settings
        /// </summary>
        /// <param name="settings">Settings to cache</param>
        /// <returns>True if cache update successful</returns>
        Task<bool> UpdateCacheAsync(Dictionary<string, object> settings);

        /// <summary>
        /// Checks if the cache is valid and not expired
        /// </summary>
        /// <returns>True if cache is valid</returns>
        bool IsCacheValid();

        /// <summary>
        /// Gets the cache expiration time
        /// </summary>
        /// <returns>DateTime when cache expires</returns>
        DateTime GetCacheExpiration();

        /// <summary>
        /// Forces cache refresh from Vault
        /// </summary>
        /// <returns>True if refresh successful</returns>
        Task<bool> RefreshCacheAsync();

        /// <summary>
        /// Gets a specific setting from cache
        /// </summary>
        /// <param name="key">Setting key</param>
        /// <returns>Setting value or null if not found</returns>
        object GetSetting(string key);

        /// <summary>
        /// Clears the cache
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        /// <returns>Cache statistics object</returns>
        object GetCacheStatistics();
    }
}
