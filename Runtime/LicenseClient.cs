using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Glitchr.Licensing
{
    /// <summary>
    /// A Glitch Art forge licence, from the game: activate this machine with the player's key
    /// (and get a signed token to run offline until it expires), free the machine's place, fetch
    /// the forge's public key. Calls <c>POST /api/licenses/activate</c>, <c>POST
    /// /api/licenses/release</c> and <c>GET /api/licenses/public-key</c> of the site; the token
    /// it is answered with is checked here (<see cref="LicenseToken.Check"/>) before it is
    /// handed over.
    /// <code>
    /// var client = new LicenseClient("https://glitchr.io", "my-game", PublicKey);
    /// ActivationResult result = await client.ActivateAsync(key);
    /// if (result.Active) { store result.Token; run; }
    /// </code>
    /// </summary>
    public sealed class LicenseClient
    {
        /// <summary>The site the licence was bought on: https://glitchr.io.</summary>
        public string SiteUrl { get; }

        /// <summary>The software's slug on the site.</summary>
        public string Software { get; }

        /// <summary>The forge's public key (base64): ship it with the game rather than fetching it.</summary>
        public string PublicKey { get; }

        /// <summary>A stable identifier of this machine. <see cref="SystemInfo.deviceUniqueIdentifier"/> by default.</summary>
        public Func<string> MachineIdentifier { get; set; } = () => SystemInfo.deviceUniqueIdentifier;

        /// <summary>What the machine is called on the holder's account. <see cref="SystemInfo.deviceName"/> by default.</summary>
        public Func<string> MachineName { get; set; } = () => SystemInfo.deviceName;

        /// <summary>
        /// Checks the site's certificate, when the platform's own checks must be replaced - a
        /// development site with a self-signed certificate. Null: the platform's checks.
        /// </summary>
        public Func<CertificateHandler> CertificateHandler { get; set; }

        /// <summary>Seconds before a request is given up.</summary>
        public int Timeout { get; set; } = 20;

        public LicenseClient(string siteUrl, string software, string publicKey)
        {
            if (string.IsNullOrEmpty(siteUrl)) throw new ArgumentException("The site's address is needed.", nameof(siteUrl));
            if (string.IsNullOrEmpty(software)) throw new ArgumentException("The software's slug is needed.", nameof(software));
            SiteUrl = siteUrl.TrimEnd('/');
            Software = software;
            PublicKey = publicKey;
        }

        /// <summary>
        /// Activates this machine - or refreshes it, taking no new place - and checks the token.
        /// Never throws for an answer of the site: <see cref="ActivationResult.Error"/> says why.
        /// </summary>
        public async Task<ActivationResult> ActivateAsync(string key)
        {
            var body = new ActivationRequest
            {
                key = (key ?? string.Empty).Trim(),
                software = Software,
                machine = MachineIdentifier(),
                name = MachineName(),
                platform = PlatformName(Application.platform),
                version = Application.version,
            };
            Answer answer = await PostAsync("/api/licenses/activate", JsonUtility.ToJson(body));
            ActivationResponse response = answer.Parse<ActivationResponse>() ?? new ActivationResponse();

            var result = new ActivationResult
            {
                Status = answer.Status,
                Error = response.error ?? answer.NetworkError,
                Machines = response.machines ?? Array.Empty<ActivatedMachine>(),
                ManageUrl = response.manage_url,
                MachinesLeft = response.machines_left,
            };
            if (answer.Status == 200 && response.status == "active")
            {
                result.License = LicenseToken.Check(response.token, PublicKey, Software, body.machine);
                if (result.License == null)
                {
                    result.Error = "token_not_genuine";
                }
                else
                {
                    result.Token = response.token;
                }
            }

            return result;
        }

        /// <summary>Whether a stored token still lets the game run here, offline.</summary>
        public LicensePayload Check(string token)
        {
            return LicenseToken.Check(token, PublicKey, Software, MachineIdentifier());
        }

        /// <summary>Frees this machine's place (an uninstall, a "deactivate" menu). True: it held one.</summary>
        public async Task<bool> ReleaseAsync(string key)
        {
            var body = new ReleaseRequest { key = (key ?? string.Empty).Trim(), machine = MachineIdentifier() };
            Answer answer = await PostAsync("/api/licenses/release", JsonUtility.ToJson(body));
            if (answer.Status != 200)
            {
                throw new InvalidOperationException("The machine could not be released: " + (answer.Parse<ActivationResponse>()?.error ?? answer.NetworkError ?? answer.Status.ToString()));
            }

            return answer.Parse<ActivationResponse>()?.status == "released";
        }

        /// <summary>The forge's public key, base64 - to compare with the one shipped, not to trust blindly.</summary>
        public async Task<string> FetchPublicKeyAsync()
        {
            Answer answer = await SendAsync(UnityWebRequest.Get(SiteUrl + "/api/licenses/public-key"));

            return answer.Status == 200 ? answer.Parse<PublicKeyResponse>()?.public_key : null;
        }

        /// <summary>The platform as the forge names it: macos, windows, linux, android, ios, webgl.</summary>
        public static string PlatformName(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer: return "macos";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer: return "windows";
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer: return "linux";
                case RuntimePlatform.Android: return "android";
                case RuntimePlatform.IPhonePlayer: return "ios";
                case RuntimePlatform.WebGLPlayer: return "webgl";
                default: return platform.ToString().ToLowerInvariant();
            }
        }

        private Task<Answer> PostAsync(string path, string json)
        {
            var request = new UnityWebRequest(SiteUrl + path, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json");

            return SendAsync(request);
        }

        private Task<Answer> SendAsync(UnityWebRequest request)
        {
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = Timeout;
            CertificateHandler handler = CertificateHandler?.Invoke();
            if (handler != null)
            {
                request.certificateHandler = handler;
                request.disposeCertificateHandlerOnDispose = true;
            }

            var completion = new TaskCompletionSource<Answer>();
            request.SendWebRequest().completed += _ =>
            {
                var answer = new Answer
                {
                    Status = request.responseCode,
                    Text = request.downloadHandler?.text,
                    NetworkError = request.result == UnityWebRequest.Result.ConnectionError ? "network_error: " + request.error : null,
                };
                request.Dispose();
                completion.TrySetResult(answer);
            };

            return completion.Task;
        }

        private sealed class Answer
        {
            public long Status;
            public string Text;
            public string NetworkError;

            public T Parse<T>() where T : class
            {
                if (string.IsNullOrEmpty(Text) || Text.TrimStart()[0] != '{') return null;
                try
                {
                    return JsonUtility.FromJson<T>(Text);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

#pragma warning disable 0649
        [Serializable] private class ActivationRequest { public string key, software, machine, name, platform, version; }
        [Serializable] private class ReleaseRequest { public string key, machine; }
        [Serializable] private class ActivationResponse { public string status, token, error, manage_url; public int machines_left; public LicensePayload license; public ActivatedMachine[] machines; }
        [Serializable] private class PublicKeyResponse { public string algorithm, public_key; }
#pragma warning restore 0649
    }

    /// <summary>What an activation came to.</summary>
    public sealed class ActivationResult
    {
        /// <summary>The site's HTTP status (0: no answer).</summary>
        public long Status;

        /// <summary>
        /// Why it did not: invalid_key (ask for the key again), wrong_software,
        /// license_not_valid, no_machine_left (show <see cref="Machines"/>, open <see cref="ManageUrl"/>),
        /// not_configured, token_not_genuine, network_error: ...; null when active.
        /// </summary>
        public string Error;

        /// <summary>The token to store and check offline (<see cref="LicenseClient.Check"/>).</summary>
        public string Token;

        /// <summary>The checked token's payload.</summary>
        public LicensePayload License;

        public int MachinesLeft;

        /// <summary>The machines holding the seat's places, when none is left.</summary>
        public ActivatedMachine[] Machines = Array.Empty<ActivatedMachine>();

        /// <summary>The holder's account page, where a machine is freed.</summary>
        public string ManageUrl;

        public bool Active => License != null;
    }

    [Serializable]
    public class ActivatedMachine
    {
        public string name;
        public string platform;
        public string last_seen;
    }
}
