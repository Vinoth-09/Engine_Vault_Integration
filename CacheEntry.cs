using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VaultWindowsService.Models
{
    /// <summary>
    /// Represents a cache entry with metadata for application settings
    /// </summary>
    public class CacheEntry
    {
        /// <summary>
        /// Timestamp when the cache entry was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the cache entry expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Version of the cache entry for tracking updates
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Source of the configuration data (e.g., "HashiCorp Vault")
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The actual configuration data
        /// </summary>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// Hash of the data for integrity checking
        /// </summary>
        public string DataHash { get; set; }

        /// <summary>
        /// Number of times this cache has been accessed
        /// </summary>
        public long AccessCount { get; set; }

        /// <summary>
        /// Last time the cache was accessed
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Creates a new cache entry
        /// </summary>
        /// <param name="data">Configuration data to cache</param>
        /// <param name="expirationInterval">How long the cache should be valid</param>
        /// <param name="source">Source of the data</param>
        /// <returns>New CacheEntry instance</returns>
        public static CacheEntry Create(Dictionary<string, object> data, TimeSpan expirationInterval, string source = "HashiCorp Vault")
        {
            var now = DateTime.UtcNow;
            var entry = new CacheEntry
            {
                CreatedAt = now,
                ExpiresAt = now.Add(expirationInterval),
                Version = 1,
                Source = source,
                Data = data ?? new Dictionary<string, object>(),
                AccessCount = 0,
                LastAccessedAt = now
            };

            entry.DataHash = entry.CalculateDataHash();
            return entry;
        }

        /// <summary>
        /// Checks if the cache entry is still valid (not expired)
        /// </summary>
        /// <returns>True if cache is valid</returns>
        public bool IsValid()
        {
            return DateTime.UtcNow < ExpiresAt;
        }

        /// <summary>
        /// Updates the access statistics
        /// </summary>
        public void RecordAccess()
        {
            AccessCount++;
            LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Updates the cache entry with new data
        /// </summary>
        /// <param name="newData">New configuration data</param>
        /// <param name="expirationInterval">New expiration interval</param>
        public void Update(Dictionary<string, object> newData, TimeSpan expirationInterval)
        {
            var now = DateTime.UtcNow;
            Data = newData ?? new Dictionary<string, object>();
            ExpiresAt = now.Add(expirationInterval);
            Version++;
            DataHash = CalculateDataHash();
            LastAccessedAt = now;
        }

        /// <summary>
        /// Calculates a hash of the data for integrity checking
        /// </summary>
        /// <returns>Hash string</returns>
        private string CalculateDataHash()
        {
            var json = JsonConvert.SerializeObject(Data, Formatting.None);
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Validates the integrity of the cached data
        /// </summary>
        /// <returns>True if data integrity is valid</returns>
        public bool ValidateIntegrity()
        {
            return DataHash == CalculateDataHash();
        }
    }
}
