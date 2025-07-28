using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Cert;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;

namespace VaultService.Services
{
    /// <summary>
    /// Manages interactions with HashiCorp Vault using TLS client certificate authentication
    /// </summary>
    public class VaultClientManager : IVaultClientManager, IDisposable
    {
        private readonly ILogger<VaultClientManager> _logger;
        private readonly VaultSettings _vaultSettings;
        private IVaultClient _vaultClient;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="VaultClientManager"/> class
        /// </summary>
        /// <param name="logger">The logger instance</param>
        /// <param name="vaultSettings">Vault configuration settings</param>
        public VaultClientManager(ILogger<VaultClientManager> logger, VaultSettings vaultSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _vaultSettings = vaultSettings ?? throw new ArgumentNullException(nameof(vaultSettings));
            
            InitializeVaultClient();
        }

        /// <summary>
        /// Gets a secret from Vault by path
        /// </summary>
        /// <param name="path">The path to the secret in Vault</param>
        /// <returns>The secret data as a dictionary</returns>
        public async Task<IDictionary<string, object>> GetSecretAsync(string path)
        {
            try
            {
                _logger.LogDebug("Retrieving secret from Vault at path: {Path}", path);
                
                var secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: path.TrimStart('/'),
                    mountPoint: _vaultSettings.SecretsMountPoint);

                _logger.LogInformation("Successfully retrieved secret from Vault at path: {Path}", path);
                return secret.Data.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve secret from Vault at path: {Path}", path);
                throw new VaultException($"Failed to retrieve secret from path '{path}'. See inner exception for details.", ex);
            }
        }

        /// <summary>
        /// Gets all configured secrets from Vault in a single call
        /// </summary>
        /// <returns>A dictionary of secret paths and their values</returns>
        public async Task<IDictionary<string, IDictionary<string, object>>> GetAllSecretsAsync()
        {
            var result = new Dictionary<string, IDictionary<string, object>>();
            
            foreach (var path in _vaultSettings.SecretPaths)
            {
                try
                {
                    var secret = await GetSecretAsync(path);
                    result[path] = secret;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve secret from path: {Path}", path);
                    // Continue with other paths even if one fails
                }
            }

            return result;
        }

        /// <summary>
        /// Validates the connection to Vault
        /// </summary>
        /// <returns>True if the connection is valid, otherwise false</returns>
        public async Task<bool> ValidateConnectionAsync()
        {
            try
            {
                _logger.LogDebug("Validating Vault connection");
                
                // Try to read the Vault server's health status
                var healthStatus = await _vaultClient.V1.System.GetHealthStatusAsync();
                
                _logger.LogInformation("Successfully connected to Vault. Vault version: {Version}, Cluster ID: {ClusterId}", 
                    healthStatus.Version, healthStatus.ClusterId);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate Vault connection");
                return false;
            }
        }

        /// <summary>
        /// Initializes the Vault client with TLS client certificate authentication
        /// </summary>
        private void InitializeVaultClient()
        {
            try
            {
                _logger.LogDebug("Initializing Vault client with address: {VaultAddress}", _vaultSettings.VaultAddress);
                
                // Configure the HTTP client handler with the client certificate
                var handler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => 
                    {
                        // In production, implement proper certificate validation
                        return true; // For development only
                    }
                };

                // Load the client certificate
                var clientCertificate = new X509Certificate2(
                    _vaultSettings.ClientCertificatePath,
                    _vaultSettings.ClientCertificatePassword,
                    X509KeyStorageFlags.Exportable);
                
                handler.ClientCertificates.Add(clientCertificate);

                // Configure Vault client settings
                var vaultClientSettings = new VaultClientSettings(_vaultSettings.VaultAddress, 
                    new CertAuthMethodInfo(
                        clientCertificate.Thumbprint, 
                        _vaultSettings.VaultCertificateRoleName))
                {
                    BeforeApiRequestAction = (httpClient, requestMessage) =>
                    {
                        if (!string.IsNullOrEmpty(_vaultSettings.VaultNamespace))
                        {
                            requestMessage.Headers.Add("X-Vault-Namespace", _vaultSettings.VaultNamespace);
                        }
                    },
                    PostProcessHttpClientHandlerAction = (httpClientHandler) =>
                    {
                        // Configure additional HTTP client handler settings if needed
                    },
                    UseVaultTokenHeaderInsteadOfAuthorizationHeader = true,
                    MyHttpClientProviderFunc = handler => new HttpClient(handler)
                };

                // Create the Vault client
                _vaultClient = new VaultClient(vaultClientSettings);
                
                _logger.LogInformation("Vault client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Vault client");
                throw new VaultException("Failed to initialize Vault client. See inner exception for details.", ex);
            }
        }

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _vaultClient?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// Represents errors that occur during Vault operations
    /// </summary>
    public class VaultException : Exception
    {
        public VaultException() { }
        public VaultException(string message) : base(message) { }
        public VaultException(string message, Exception inner) : base(message, inner) { }
    }
}