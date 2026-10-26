# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package
adheres to [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-26

### Added

- `LicenseClient`: activates this machine against a Glitch Art forge
  (`POST /api/licenses/activate`), frees its place (`POST /api/licenses/release`),
  fetches the forge's public key (`GET /api/licenses/public-key`).
- `LicenseToken`: checks a licence token offline - its Ed25519 signature, its
  software, its machine, its `valid_until`.
- `Ed25519`: signature verification in managed code (RFC 8032), no native library.
