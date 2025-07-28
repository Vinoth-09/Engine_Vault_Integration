using System;
using System.Runtime.Serialization;

namespace VaultWindowsService.Exceptions
{
    /// <summary>
    /// Exception thrown when Vault service operations fail
    /// Implements standard exception pattern with serialization support
    /// </summary>
    [Serializable]
    public class VaultServiceException : Exception
    {
        public string VaultUrl { get; }
        public string Operation { get; }
        public int? StatusCode { get; }

        public VaultServiceException() : base("A Vault service error occurred.")
        {
        }

        public VaultServiceException(string message) : base(message)
        {
        }

        public VaultServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public VaultServiceException(string message, string vaultUrl, string operation) : base(message)
        {
            VaultUrl = vaultUrl;
            Operation = operation;
        }

        public VaultServiceException(string message, string vaultUrl, string operation, int statusCode) : base(message)
        {
            VaultUrl = vaultUrl;
            Operation = operation;
            StatusCode = statusCode;
        }

        public VaultServiceException(string message, string vaultUrl, string operation, Exception innerException) : base(message, innerException)
        {
            VaultUrl = vaultUrl;
            Operation = operation;
        }

        protected VaultServiceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            VaultUrl = info.GetString(nameof(VaultUrl));
            Operation = info.GetString(nameof(Operation));
            StatusCode = (int?)info.GetValue(nameof(StatusCode), typeof(int?));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(VaultUrl), VaultUrl);
            info.AddValue(nameof(Operation), Operation);
            info.AddValue(nameof(StatusCode), StatusCode);
        }

        public override string ToString()
        {
            var baseString = base.ToString();
            if (!string.IsNullOrEmpty(VaultUrl) || !string.IsNullOrEmpty(Operation))
            {
                baseString += $"\nVault URL: {VaultUrl ?? "N/A"}";
                baseString += $"\nOperation: {Operation ?? "N/A"}";
                if (StatusCode.HasValue)
                {
                    baseString += $"\nStatus Code: {StatusCode.Value}";
                }
            }
            return baseString;
        }
    }
}
