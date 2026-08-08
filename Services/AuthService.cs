using System;
using Rasid.Models;

namespace Rasid.Services
{
    internal sealed class AuthService
    {
        private const string TargetName = "rasid_addin";
        private const string CredentialUserName = "api_key";

        public static AuthService Instance { get; } = new AuthService();

        public string ApiKey { get; private set; }
        public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

        private AuthService()
        {
        }

        public void SetApiKey(string apiKey)
        {
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            ApiClient.Instance.SetAuthHeader(ApiKey);
        }

        public void SaveApiKey(string apiKey = null)
        {
            var keyToSave = apiKey ?? ApiKey;
            if (string.IsNullOrWhiteSpace(keyToSave))
            {
                ClearApiKey();
                return;
            }

            keyToSave = keyToSave.Trim();
            WindowsCredentialStore.Save(TargetName, CredentialUserName, keyToSave);
            SetApiKey(keyToSave);
        }

        public bool LoadApiKey()
        {
            if (!WindowsCredentialStore.TryRead(TargetName, out var key))
                return false;

            SetApiKey(key);
            return true;
        }

        public void ClearApiKey()
        {
            SetApiKey(null);
            WindowsCredentialStore.Delete(TargetName);
        }

        public System.Threading.Tasks.Task<UserProfile> GetProfileAsync()
        {
            return new RasidApiClient(ApiClient.Instance).GetProfileAsync();
        }

        public void ClearCredentials()
        {
            ClearApiKey();
        }

        public async System.Threading.Tasks.Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                await GetProfileAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
