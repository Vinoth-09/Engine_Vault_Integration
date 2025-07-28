using System;
using System.Configuration;

namespace VaultWindowsService.Models
{
    /// <summary>
    /// Configuration model for HashiCorp Vault connection settings
    /// </summary>
    public class VaultConfiguration
    {
        public string VaultUrl { get; set; }
        public string VaultNamespace { get; set; }
        public string SecretPath { get; set; }
        public string CertificateThumbprint { get; set; }
        public string CertificateStoreName { get; set; }
        public string CertificateStoreLocation { get; set; }
        public int CacheRefreshIntervalMinutes { get; set; }
        public string CacheFilePath { get; set; }

        /// <summary>
        /// Creates VaultConfiguration from App.config settings
        /// </summary>
        /// <returns>VaultConfiguration instance</returns>
        public static VaultConfiguration FromAppConfig()
        {
            return new VaultConfiguration
            {
                VaultUrl = ConfigurationManager.AppSettings["VaultUrl"] ?? throw new ConfigurationErrorsException("VaultUrl not configured"),
                VaultNamespace = ConfigurationManager.AppSettings["VaultNamespace"],
                SecretPath = ConfigurationManager.AppSettings["VaultSecretPath"] ?? throw new ConfigurationErrorsException("VaultSecretPath not configured"),
                CertificateThumbprint = ConfigurationManager.AppSettings["CertificateThumbprint"] ?? throw new ConfigurationErrorsException("CertificateThumbprint not configured"),
                CertificateStoreName = ConfigurationManager.AppSettings["CertificateStoreName"] ?? "My",
                CertificateStoreLocation = ConfigurationManager.AppSettings["CertificateStoreLocation"] ?? "LocalMachine",
                CacheRefreshIntervalMinutes = int.Parse(ConfigurationManager.AppSettings["CacheRefreshIntervalMinutes"] ?? "60"),
                CacheFilePath = ConfigurationManager.AppSettings["CacheFilePath"] ?? @"C:\ProgramData\VaultWindowsService\appsettings.json"
            };
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>True if configuration is valid</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(VaultUrl) &&
                   !string.IsNullOrWhiteSpace(SecretPath) &&
                   !string.IsNullOrWhiteSpace(CertificateThumbprint) &&
                   CacheRefreshIntervalMinutes > 0;
        }

        /// <summary>
        /// Gets the cache refresh interval as TimeSpan
        /// </summary>
        /// <returns>TimeSpan representing cache refresh interval</returns>
        public TimeSpan GetCacheRefreshInterval()
        {
            return TimeSpan.FromMinutes(CacheRefreshIntervalMinutes);
        }
    }
}
