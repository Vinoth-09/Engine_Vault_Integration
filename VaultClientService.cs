using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Cert;
using VaultSharp.V1.Commons;
using NLog;
using VaultWindowsService.Interfaces;
using VaultWindowsService.Models;
using VaultWindowsService.Services;
using VaultWindowsService.Exceptions;

namespace VaultWindowsService.Services
{
    /// <summary>
    /// Repository pattern implementation for HashiCorp Vault operations
    /// Handles TLS certificate authentication and secret retrieval
    /// </summary>
    public class VaultClientService : IVaultClient
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly VaultConfiguration _configuration;
        private readonly CertificateAuthenticationService _certificateService;
        private IVaultClient _vaultClient;
        private bool _isAuthenticated;
        private bool _disposed;

        public VaultClientService(VaultConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _certificateService = new CertificateAuthenticationService(_configuration);
        }

        /// <summary>
        /// Authenticates with Vault using TLS certificate
        /// </summary>
        /// <returns>True if authentication successful</returns>
        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                Logger.Info($"Authenticating with Vault at {_configuration.VaultUrl}");

                // Get client certificate
                var clientCertificate = _certificateService.GetClientCertificate();

                // Create HTTP client handler with certificate
                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(clientCertificate);

                // Configure Vault client
                var httpClient = new HttpClient(handler);
                
                var vaultClientSettings = new VaultClientSettings(_configuration.VaultUrl, null)
                {
                    Namespace = _configuration.VaultNamespace,
                    MyHttpClientProviderFunc = () => httpClient
                };

                // Create auth method for certificate authentication
                IAuthMethodInfo authMethod = new CertAuthMethodInfo();

                _vaultClient = new VaultClient(vaultClientSettings, authMethod);

                // Test authentication by making a simple call
                await _vaultClient.V1.System.GetHealthStatusAsync();

                _isAuthenticated = true;
                Logger.Info("Successfully authenticated with Vault using certificate");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to authenticate with Vault");
                _isAuthenticated = false;
                throw new VaultServiceException(
                    "Failed to authenticate with Vault using certificate",
                    _configuration.VaultUrl,
                    "Authentication",
                    ex);
            }
        }

        /// <summary>
        /// Retrieves all secrets from the configured Vault path
        /// </summary>
        /// <returns>Dictionary of key-value pairs representing application settings</returns>
        public async Task<Dictionary<string, object>> GetAllSecretsAsync()
        {
            try
            {
                if (!_isAuthenticated)
                {
                    await AuthenticateAsync();
                }

                Logger.Info($"Retrieving all secrets from path: {_configuration.SecretPath}");

                // Read secrets from Vault
                Secret<SecretData> kv2Secret = await _vaultClient.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(path: _configuration.SecretPath, mountPoint: "secret");

                if (kv2Secret?.Data?.Data == null)
                {
                    Logger.Warn($"No secrets found at path: {_configuration.SecretPath}");
                    return new Dictionary<string, object>();
                }

                var secrets = new Dictionary<string, object>();
                foreach (var kvp in kv2Secret.Data.Data)
                {
                    secrets[kvp.Key] = kvp.Value;
                }

                Logger.Info($"Successfully retrieved {secrets.Count} secrets from Vault");
                return secrets;
            }
            catch (VaultServiceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to retrieve secrets from path: {_configuration.SecretPath}");
                throw new VaultServiceException(
                    $"Failed to retrieve secrets from path: {_configuration.SecretPath}",
                    _configuration.VaultUrl,
                    "GetAllSecrets",
                    ex);
            }
        }

        /// <summary>
        /// Retrieves a specific secret by key
        /// </summary>
        /// <param name="key">Secret key</param>
        /// <returns>Secret value</returns>
        public async Task<object> GetSecretAsync(string key)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Key cannot be null or empty", nameof(key));
                }

                var allSecrets = await GetAllSecretsAsync();
                
                if (allSecrets.TryGetValue(key, out var value))
                {
                    Logger.Debug($"Retrieved secret for key: {key}");
                    return value;
                }

                Logger.Warn($"Secret not found for key: {key}");
                return null;
            }
            catch (Exception ex) when (!(ex is VaultServiceException))
            {
                Logger.Error(ex, $"Failed to retrieve secret for key: {key}");
                throw new VaultServiceException(
                    $"Failed to retrieve secret for key: {key}",
                    _configuration.VaultUrl,
                    "GetSecret",
                    ex);
            }
        }

        /// <summary>
        /// Checks if the Vault client is authenticated and healthy
        /// </summary>
        /// <returns>True if healthy</returns>
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                if (!_isAuthenticated || _vaultClient == null)
                {
                    return false;
                }

                var healthStatus = await _vaultClient.V1.System.GetHealthStatusAsync();
                var isHealthy = healthStatus?.Initialized == true && healthStatus?.Sealed == false;

                Logger.Debug($"Vault health check result: {isHealthy}");
                return isHealthy;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Vault health check failed");
                return false;
            }
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _vaultClient?.Dispose();
                _disposed = true;
                Logger.Debug("VaultClientService disposed");
            }
        }
    }
}
