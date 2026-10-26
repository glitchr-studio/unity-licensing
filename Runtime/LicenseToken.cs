using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Glitchr.Licensing
{
    /// <summary>
    /// A licence token, checked offline:
    /// <c>base64url(payload JSON) "." base64url(Ed25519 signature of the payload JSON)</c>.
    /// A game runs when the signature is the forge's, the token is for this software and this
    /// machine, and its <c>valid_until</c> has not passed.
    /// </summary>
    public static class LicenseToken
    {
        /// <summary>The payload, when the signature verifies with <paramref name="publicKey"/> (base64); else null.</summary>
        public static LicensePayload Verify(string token, string publicKey)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(publicKey))
            {
                return null;
            }
            string[] parts = token.Trim().Split('.');
            if (parts.Length != 2)
            {
                return null;
            }

            byte[] json, signature, key;
            try
            {
                json = Base64Url.Decode(parts[0]);
                signature = Base64Url.Decode(parts[1]);
                key = Convert.FromBase64String(publicKey.Trim());
            }
            catch (FormatException)
            {
                return null;
            }
            if (!Ed25519.Verify(signature, json, key))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<LicensePayload>(Encoding.UTF8.GetString(json));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether that token lets this software run on this machine now: genuine, its own
        /// software, its own machine, still valid. The payload when it does; else null.
        /// </summary>
        public static LicensePayload Check(string token, string publicKey, string software, string machineIdentifier, DateTimeOffset? now = null)
        {
            LicensePayload payload = Verify(token, publicKey);
            if (payload == null || payload.software != software || payload.machine != MachineHash(machineIdentifier))
            {
                return null;
            }

            return (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() < payload.valid_until ? payload : null;
        }

        /// <summary>The machine as the forge stores it: SHA-256 (hex) of its trimmed identifier.</summary>
        public static string MachineHash(string identifier)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes((identifier ?? string.Empty).Trim()));
                StringBuilder hex = new StringBuilder(64);
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2"));
                }

                return hex.ToString();
            }
        }
    }

    /// <summary>Base64 with the URL alphabet and no padding, as the forge writes its tokens.</summary>
    public static class Base64Url
    {
        public static byte[] Decode(string text)
        {
            string base64 = text.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
                case 1: throw new FormatException("Not base64url.");
            }

            return Convert.FromBase64String(base64);
        }

        public static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
