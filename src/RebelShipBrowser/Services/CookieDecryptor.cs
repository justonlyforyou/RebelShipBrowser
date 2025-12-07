using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Handles decryption of Steam's encrypted cookies using DPAPI and AES-256-GCM
    /// </summary>
    public static class CookieDecryptor
    {
        /// <summary>
        /// Extracts and decrypts the AES key from Steam's LocalPrefs.json using Windows DPAPI
        /// </summary>
        /// <param name="localPrefsPath">Path to LocalPrefs.json</param>
        /// <returns>Decrypted AES key or null if extraction fails</returns>
        public static byte[]? GetAesKey(string localPrefsPath)
        {
            try
            {
                if (!File.Exists(localPrefsPath))
                {
                    return null;
                }

                var json = File.ReadAllText(localPrefsPath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("os_crypt", out var osCrypt))
                {
                    return null;
                }

                if (!osCrypt.TryGetProperty("encrypted_key", out var encryptedKeyElement))
                {
                    return null;
                }

                var encryptedKeyB64 = encryptedKeyElement.GetString();
                if (string.IsNullOrEmpty(encryptedKeyB64))
                {
                    return null;
                }

                // Base64 decode
                var encryptedKey = Convert.FromBase64String(encryptedKeyB64);

                // Skip 'DPAPI' header (5 bytes)
                var keyToDecrypt = encryptedKey[5..];

                // Use Windows DPAPI to decrypt
                return ProtectedData.Unprotect(
                    keyToDecrypt,
                    null,
                    DataProtectionScope.CurrentUser
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Decrypts an AES-256-GCM encrypted cookie value
        /// </summary>
        /// <param name="encryptedValue">Encrypted cookie bytes from SQLite</param>
        /// <param name="aesKey">Decrypted AES key</param>
        /// <returns>Decrypted and URL-decoded cookie value</returns>
        public static string? DecryptCookieValue(byte[] encryptedValue, byte[] aesKey)
        {
            if (encryptedValue == null)
            {
                throw new ArgumentNullException(nameof(encryptedValue));
            }

            if (aesKey == null)
            {
                throw new ArgumentNullException(nameof(aesKey));
            }

            try
            {
                // Skip 'v10' or 'v11' prefix (3 bytes)
                var payload = encryptedValue[3..];

                // Structure: Nonce (12 bytes) | Ciphertext | Tag (16 bytes)
                var nonce = payload[..12];
                var ciphertextWithTag = payload[12..];
                var tag = ciphertextWithTag[^16..];
                var ciphertext = ciphertextWithTag[..^16];

                var plaintext = new byte[ciphertext.Length];

                using var aesGcm = new AesGcm(aesKey, 16);
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

                var result = Encoding.UTF8.GetString(plaintext);

                // URL-decode and trim
                return Uri.UnescapeDataString(result).Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Queries the SQLite cookies database for a specific cookie
        /// </summary>
        /// <param name="dbPath">Path to Cookies SQLite database</param>
        /// <param name="domain">Domain to search for (e.g., 'shippingmanager.cc')</param>
        /// <param name="cookieName">Name of the cookie to find</param>
        /// <returns>Encrypted cookie value or null if not found</returns>
        public static byte[]? GetEncryptedCookieFromDb(string dbPath, string domain, string cookieName)
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    return null;
                }

                using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT encrypted_value
                    FROM cookies
                    WHERE host_key LIKE @domain
                    AND name = @name
                    LIMIT 1";
                command.Parameters.AddWithValue("@domain", $"%{domain}");
                command.Parameters.AddWithValue("@name", cookieName);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return (byte[])reader["encrypted_value"];
                }

                return null;
            }
            catch (SqliteException)
            {
                // Database locked or other SQLite error
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Copies the database to a temp location if it's locked
        /// </summary>
        /// <param name="sourcePath">Original database path</param>
        /// <returns>Path to the copied database or original if copy fails</returns>
        public static string CopyDatabaseIfLocked(string sourcePath)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"cookies_copy_{Guid.NewGuid()}.db");

            try
            {
                File.Copy(sourcePath, tempPath, overwrite: true);
                return tempPath;
            }
            catch
            {
                return sourcePath;
            }
        }

        /// <summary>
        /// Cleans up a temporary database copy
        /// </summary>
        public static void CleanupTempDatabase(string tempPath)
        {
            if (tempPath == null)
            {
                throw new ArgumentNullException(nameof(tempPath));
            }

            try
            {
                if (tempPath.Contains("cookies_copy_", StringComparison.Ordinal) && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
