# Glitch Art Licensing

A Unity package that lets a game or an application sold on a Glitch Art forge
check its licence: it activates the player's machine with their licence key,
keeps the signed token the forge answers with, and checks that token offline
at each start - no server call until it expires.

- Activation, refresh and release against the forge's licence API.
- Offline check of the token: Ed25519 signature (managed code, every platform
  Unity builds for), software, machine, validity.
- No dependency beyond Unity itself (2021.3 or later).

## Installation

The package is served by the forge's Unity registry. In the project's
`Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "Glitch Art",
      "url": "https://glitchr.io/upm",
      "scopes": ["com.glitchr"]
    }
  ],
  "dependencies": {
    "com.glitchr.licensing": "1.0.0"
  }
}
```

Then **Window > Package Manager > My Registries** lists it. It can also be
added from its Git URL: `https://github.com/glitchr-studio/unity-licensing.git`.
See [docs/installation.md](docs/installation.md).

## Usage

```csharp
using Glitchr.Licensing;

const string PublicKey = "<the forge's public key, base64>";
var client = new LicenseClient("https://glitchr.io", "my-game", PublicKey);

// At start: a stored token that still runs, offline.
LicensePayload licence = client.Check(storedToken);

// Otherwise, or when it is about to run out: activate, online.
if (licence == null)
{
    ActivationResult result = await client.ActivateAsync(playerKey);
    if (result.Active) { storedToken = result.Token; licence = result.License; }
    else { /* result.Error: invalid_key, no_machine_left (result.Machines, result.ManageUrl)... */ }
}
```

See [docs/usage.md](docs/usage.md) and, for what the token holds,
[docs/token.md](docs/token.md).

## Tests

The editor tests (Window > General > Test Runner > EditMode) check the
Ed25519 verification against RFC 8032's vectors and a token signed as the
forge signs it. Add the package to the project's `testables` to see them:

```json
"testables": ["com.glitchr.licensing"]
```

## Licence

MIT - see [LICENSE.md](LICENSE.md).
