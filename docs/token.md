# The token

The forge answers an activation with a token the game keeps and checks
offline:

```
token = base64url(payload JSON) "." base64url(Ed25519 signature of the payload JSON)
```

The payload (`LicensePayload`):

| Field | |
|---|---|
| `v` | 1 |
| `license` | the licence's id |
| `software` | the software's slug |
| `email` | the seat's e-mail |
| `machine` | SHA-256 (hex) of the machine identifier |
| `issued_at` | UNIX time |
| `valid_until` | UNIX time: past it, activate again, online |
| `license_expires` | UNIX time; 0 (null on the forge): perpetual |
| `updates_until` | UNIX time; 0 (null on the forge): every release is covered |

`LicenseToken.Check()` runs the game when, all of them, the signature
verifies with the public key it ships with, `software` is its own slug,
`machine` is the hash of its own machine identifier, and now is before
`valid_until`. For a version gate, `LicensePayload.Covers(releaseDate)`
compares a release's date with `updates_until`.

`valid_until` is the forge's grace period (30 days by default) after the last
activation, never beyond the licence's expiry: a machine offline for longer
comes back online once; a revoked licence or a freed machine stops at the
latest then.

The verification is `Ed25519.Verify(signature, message, publicKey)`, RFC 8032
in managed code: no native plug-in, the same on every platform.
