using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VaultWindowsService.Interfaces
{
    /// <summary>
    /// Repository pattern interface for HashiCorp Vault operations
    /// </summary>
    public interface IVaultClient
    {
        /// <summary>
        /// Authenticates with Vault using TLS certificate
        /// </summary>
        /// <returns>True if authentication successful</returns>
        Task<bool> AuthenticateAsync();

        /// <summary>
        /// Retrieves all secrets from the configured Vault path
        /// </summary>
        /// <returns>Dictionary of key-value pairs representing application settings</returns>
        Task<Dictionary<string, object>> GetAllSecretsAsync();

        /// <summary>
        /// Retrieves a specific secret by key
        /// </summary>
        /// <param name="key">Secret key</param>
        /// <returns>Secret value</returns>
        Task<object> GetSecretAsync(string key);

        /// <summary>
        /// Checks if the Vault client is authenticated and healthy
        /// </summary>
        /// <returns>True if healthy</returns>
        Task<bool> IsHealthyAsync();

        /// <summary>
        /// Disposes resources
        /// </summary>
        void Dispose();
    }
}
