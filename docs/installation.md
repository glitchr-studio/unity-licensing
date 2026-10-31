# Installation

## From the forge's Unity registry

The forge serves its Unity packages as an npm registry, the kind Unity's
Package Manager reads as a scoped registry. Add it to the project's
`Packages/manifest.json`, once:

```json
"scopedRegistries": [
  {
    "name": "Glitch Art",
    "url": "https://glitchr.io/upm",
    "scopes": ["io.glitchr"]
  }
]
```

Then **Window > Package Manager**, **My Registries**: the free packages are
listed; select one and **Install**. Or add it to `dependencies` by hand:

```json
"io.glitchr.licensing": "1.0.0"
```

A new version published on the forge shows up as an update in the same
window.

## A licensed package

A licensed package is served to the holder of a licence that covers it. Give
the registry your licence key (made from your account, "My licences") in
`~/.upmconfig.toml` (`%USERPROFILE%\.upmconfig.toml` on Windows):

```toml
[npmAuth."https://glitchr.io/upm"]
token = "glk_XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX-XXXX"
alwaysAuth = true
```

The Package Manager sends it with every request to that registry. Without it
the package is not listed and its installation is refused (401); with a key
whose licence does not cover it, refused too (403).

## From Git

```json
"io.glitchr.licensing": "https://github.com/glitchr-studio/unity-licensing.git#1.0.0"
```
