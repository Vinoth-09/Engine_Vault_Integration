using System.Collections.Generic;
using System.Threading.Tasks;

namespace VaultService.Services
{
    /// <summary>
    /// Defines the interface for Vault client operations
    /// </summary>
    public interface IVaultClientManager
    {
        /// <summary>
        /// Gets a secret from Vault by path
        /// </summary>
        /// <param name="path">The path to the secret in Vault</param>
        /// <returns>The secret data as a dictionary</returns>
        Task<IDictionary<string, object>> GetSecretAsync(string path);

        /// <summary>
        /// Gets all configured secrets from Vault in a single call
        /// </summary>
        /// <returns>A dictionary of secret paths and their values</returns>
        Task<IDictionary<string, IDictionary<string, object>>> GetAllSecretsAsync();

        /// <summary>
        /// Validates the connection to Vault
        /// </summary>
        /// <returns>True if the connection is valid, otherwise false</returns>
        Task<bool> ValidateConnectionAsync();
    }
}
