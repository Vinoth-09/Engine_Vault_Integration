using System;
using System.Runtime.Serialization;

namespace VaultWindowsService.Exceptions
{
    /// <summary>
    /// Exception thrown when cache operations fail
    /// </summary>
    [Serializable]
    public class CacheException : Exception
    {
        public string CacheFilePath { get; }
        public string Operation { get; }

        public CacheException() : base("A cache operation error occurred.")
        {
        }

        public CacheException(string message) : base(message)
        {
        }

        public CacheException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public CacheException(string message, string cacheFilePath, string operation) : base(message)
        {
            CacheFilePath = cacheFilePath;
            Operation = operation;
        }

        public CacheException(string message, string cacheFilePath, string operation, Exception innerException) : base(message, innerException)
        {
            CacheFilePath = cacheFilePath;
            Operation = operation;
        }

        protected CacheException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            CacheFilePath = info.GetString(nameof(CacheFilePath));
            Operation = info.GetString(nameof(Operation));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(CacheFilePath), CacheFilePath);
            info.AddValue(nameof(Operation), Operation);
        }
    }
}
