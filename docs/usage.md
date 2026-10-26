# Usage

## The client

```csharp
var client = new LicenseClient(siteUrl, softwareSlug, publicKey);
```

- `siteUrl`: the site the licence was sold on, `https://glitchr.io`.
- `softwareSlug`: the software's slug on that site - a key only opens its own
  software.
- `publicKey`: the forge's public key, base64 (`GET /api/licenses/public-key`).
  Ship it in the game rather than fetching it at run time: a key fetched at
  run time could be swapped.

The machine is `SystemInfo.deviceUniqueIdentifier` and its name
`SystemInfo.deviceName`; set `client.MachineIdentifier` and
`client.MachineName` to change them.

## Activating

```csharp
ActivationResult result = await client.ActivateAsync(key);
```

`result.Active` is true when the forge activated the machine and its token
checked out; `result.Token` is then to be stored (the game chooses where:
`PlayerPrefs`, a file under `Application.persistentDataPath`). Activating an
already active machine takes no new place: call it again whenever the token
is about to run out (`result.License.ValidUntil`).

Otherwise `result.Error` says why:

| Error | What to do |
|---|---|
| `invalid_key` | Ask for the key again. |
| `wrong_software` | The key is another software's. |
| `license_not_valid` | The licence expired or was revoked: say so. |
| `no_machine_left` | Show `result.Machines`; open `result.ManageUrl` to free one. |
| `not_configured` | The site has no signing key yet. |
| `token_not_genuine` | The token's signature, software or machine is wrong: do not run. |
| `network_error: ...` | No answer: run on the stored token while it is valid. |

## At each start, offline

```csharp
LicensePayload licence = client.Check(storedToken);
```

Null when the token is not the forge's, is another software's or another
machine's, or has passed its `valid_until`. Then activate again, online.

## Freeing the machine

```csharp
bool released = await client.ReleaseAsync(key);
```

Before an uninstall, from a "deactivate" menu: the seat's place goes back to
its holder.

## A development site

A site with a self-signed certificate (a local forge) is refused by
UnityWebRequest. Give the client a handler that accepts that certificate,
for development builds only:

```csharp
client.CertificateHandler = () => new MyPinnedCertificateHandler();
```
