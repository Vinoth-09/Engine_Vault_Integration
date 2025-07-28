using System;
using System.Runtime.Serialization;

namespace VaultWindowsService.Exceptions
{
    /// <summary>
    /// Exception thrown when certificate authentication fails
    /// </summary>
    [Serializable]
    public class CertificateAuthenticationException : Exception
    {
        public string CertificateThumbprint { get; }
        public string StoreName { get; }
        public string StoreLocation { get; }

        public CertificateAuthenticationException() : base("Certificate authentication failed.")
        {
        }

        public CertificateAuthenticationException(string message) : base(message)
        {
        }

        public CertificateAuthenticationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public CertificateAuthenticationException(string message, string certificateThumbprint) : base(message)
        {
            CertificateThumbprint = certificateThumbprint;
        }

        public CertificateAuthenticationException(string message, string certificateThumbprint, string storeName, string storeLocation) : base(message)
        {
            CertificateThumbprint = certificateThumbprint;
            StoreName = storeName;
            StoreLocation = storeLocation;
        }

        public CertificateAuthenticationException(string message, string certificateThumbprint, Exception innerException) : base(message, innerException)
        {
            CertificateThumbprint = certificateThumbprint;
        }

        protected CertificateAuthenticationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            CertificateThumbprint = info.GetString(nameof(CertificateThumbprint));
            StoreName = info.GetString(nameof(StoreName));
            StoreLocation = info.GetString(nameof(StoreLocation));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(CertificateThumbprint), CertificateThumbprint);
            info.AddValue(nameof(StoreName), StoreName);
            info.AddValue(nameof(StoreLocation), StoreLocation);
        }

        public override string ToString()
        {
            var baseString = base.ToString();
            if (!string.IsNullOrEmpty(CertificateThumbprint))
            {
                baseString += $"\nCertificate Thumbprint: {CertificateThumbprint}";
                baseString += $"\nStore Name: {StoreName ?? "N/A"}";
                baseString += $"\nStore Location: {StoreLocation ?? "N/A"}";
            }
            return baseString;
        }
    }
}
