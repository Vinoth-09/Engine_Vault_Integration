using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultService.Models;

namespace VaultService.Services
{
    public interface IVaultCacheManager : IDisposable
    {
        bool TryGetValue(string key, out object value);
        void Set(string key, object value, TimeSpan? expiration = null);
        void Remove(string key);
        void Clear();
        int Count { get; }
        void CleanupExpiredItems();
    }

    public class VaultCacheManager : IVaultCacheManager
    {
        private readonly ILogger<VaultCacheManager> _logger;
        private readonly CacheSettings _cacheSettings;
        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly Timer _cleanupTimer;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private bool _disposed;

        public VaultCacheManager(
            ILogger<VaultCacheManager> logger,
            IOptions<CacheSettings> cacheSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheSettings = cacheSettings?.Value ?? throw new ArgumentNullException(nameof(cacheSettings));
            
            _cache = new ConcurrentDictionary<string, CacheItem>(StringComparer.OrdinalIgnoreCase);
            
            // Setup cleanup timer if enabled
            if (_cacheSettings.Enabled && _cacheSettings.CleanupIntervalMinutes > 0)
            {
                _cleanupTimer = new Timer(
                    _ => CleanupExpiredItems(),
                    null,
                    TimeSpan.FromMinutes(_cacheSettings.CleanupIntervalMinutes),
                    TimeSpan.FromMinutes(_cacheSettings.CleanupIntervalMinutes));
            }
        }

        public bool TryGetValue(string key, out object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

            _lock.EnterReadLock();
            try
            {
                if (_cache.TryGetValue(key, out var item) && !item.IsExpired())
                {
                    value = item.Value;
                    item.LastAccessed = DateTimeOffset.UtcNow;
                    return true;
                }

                // Remove expired item
                if (item != null)
                {
                    _ = _cache.TryRemove(key, out _);
                }

                value = null;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Set(string key, object value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var expirationTime = expiration ?? TimeSpan.FromMinutes(_cacheSettings.ExpirationMinutes);
            var newItem = new CacheItem(value, expirationTime);

            _lock.EnterWriteLock();
            try
            {
                _cache[key] = newItem;
                _logger.LogDebug("Cached item with key: {Key}, Expires: {Expiration}", 
                    key, newItem.ExpirationTime);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));

            _lock.EnterWriteLock();
            try
            {
                _ = _cache.TryRemove(key, out _);
                _logger.LogDebug("Removed item from cache: {Key}", key);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _cache.Clear();
                _logger.LogInformation("Cache cleared");
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public int Count => _cache.Count;

        public void CleanupExpiredItems()
        {
            if (!_cacheSettings.Enabled)
                return;

            _logger.LogDebug("Starting cache cleanup...");
            int removedCount = 0;
            var now = DateTimeOffset.UtcNow;

            _lock.EnterUpgradeableReadLock();
            try
            {
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired())
                    .Select(kvp => kvp.Key)
                    .ToList();

                _lock.EnterWriteLock();
                try
                {
                    foreach (var key in expiredKeys)
                    {
                        if (_cache.TryRemove(key, out _))
                        {
                            removedCount++;
                        }
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }

            _logger.LogInformation("Cache cleanup completed. Removed {Count} expired items", removedCount);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cleanupTimer?.Dispose();
                    _lock?.Dispose();
                }
                _disposed = true;
            }
        }

        ~VaultCacheManager()
        {
            Dispose(false);
        }
    }

    internal class CacheItem
    {
        public object Value { get; }
        public DateTimeOffset Created { get; }
        public DateTimeOffset LastAccessed { get; set; }
        public TimeSpan Expiration { get; }
        public DateTimeOffset ExpirationTime => Created.Add(Expiration);

        public CacheItem(object value, TimeSpan expiration)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Created = DateTimeOffset.UtcNow;
            LastAccessed = Created;
            Expiration = expiration;
        }

        public bool IsExpired()
        {
            return DateTimeOffset.UtcNow >= ExpirationTime;
        }
    }
}
