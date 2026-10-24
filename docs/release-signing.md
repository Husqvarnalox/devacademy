# Release signing

The release workflow (`.github/workflows/release.yml`, run by pushing a `vX.Y.Z` tag) signs every binary it can sign
with the credentials configured in the repository. Each part is optional: a missing part produces an unsigned build
and a `::warning::` in the workflow summary, so an unsigned release is never mistaken for a signed one.

| Platform | What prevents the warning | Cost | What the workflow does |
|---|---|---|---|
| macOS | Developer ID signature + notarization + stapling | Apple Developer Program, 99 USD/year. There is no free way to pass Gatekeeper for a downloaded app | signs every Mach-O and the `.app` (hardened runtime), notarizes and staples the `.app` and the `.dmg`, then checks them with `stapler validate` and `spctl --assess` |
| Windows | Authenticode signature (SmartScreen reputation grows with downloads of signed builds) | free for open source through [SignPath Foundation](https://signpath.org) | signs `DevAcademy.Desktop.exe`, builds the zip and the installer from it, signs the installer, checks both with `Get-AuthenticodeSignature` |
| Linux | nothing is quarantined; authenticity comes from a signed checksum file | free (GPG key) | signs `SHA256SUMS` (`SHA256SUMS.asc`) and attaches the public key `DevAcademy-release-key.asc` |

## macOS (Developer ID)

1. Join the Apple Developer Program and create a **Developer ID Application** certificate (Xcode → Settings → Accounts,
   or developer.apple.com → Certificates). Export it with its private key as a `.p12` file with a password.
2. Create an app-specific password for the Apple ID at account.apple.com (Sign-In and Security → App-Specific Passwords).
3. Repository secrets (Settings → Secrets and variables → Actions → Secrets):

   | Secret | Value |
   |---|---|
   | `MACOS_CERT_P12` | `base64 -i DeveloperID.p12` (one line) |
   | `MACOS_CERT_PASSWORD` | password of the `.p12` |
   | `MACOS_SIGN_IDENTITY` | `Developer ID Application: <Name> (<TEAMID>)`, as printed by `security find-identity -v -p codesigning` |
   | `APPLE_ID` | the Apple ID e-mail |
   | `APPLE_TEAM_ID` | the 10-character team ID |
   | `APPLE_APP_PASSWORD` | the app-specific password |

Not verified yet: no Developer ID certificate was available while writing the signing code. If the first notarized
build fails to load a bundled dylib, see the note in `packaging/macos/entitlements.plist`.

## Windows (SignPath Foundation, free for open source)

1. Apply at <https://signpath.org/apply> (the repository is public and MIT-licensed). The certificate is issued to
   "SignPath Foundation"; signing requests from release builds need a manual approval in SignPath.
2. Meet the conditions of <https://signpath.org/terms>: a "Code signing policy" section on the project page (see
   README.md), MFA for all team members.
3. In SignPath create the project (slug `devacademy`), a signing policy (slug `release-signing`), connect the GitHub
   repository as a trusted build system and add two artifact configurations with the contents of
   `packaging/windows/signpath/windows-app.xml` (slug `windows-app`) and `windows-installer.xml` (slug `windows-installer`).
4. Repository settings:

   | Kind | Name | Value |
   |---|---|---|
   | Secret | `SIGNPATH_API_TOKEN` | API token of a SignPath CI user with submitter rights |
   | Variable | `SIGNPATH_ORGANIZATION_ID` | organization ID from SignPath |
   | Variable | `SIGNPATH_PROJECT_SLUG` | optional, default `devacademy` |
   | Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | optional, default `release-signing` |

Each Windows RID sends two signing requests (executable, then installer), so a release needs four approvals. The
workflow waits up to an hour for each. The uninstaller inside the installer is not signed (Inno Setup can only sign it
with a local signing tool); it runs from the installed location and is not subject to SmartScreen.

## Linux and checksums (GPG)

1. Create a dedicated signing key: `gpg --quick-gen-key "DevAcademy Releases <...>" ed25519 sign 2y`.
2. Secrets: `GPG_PRIVATE_KEY` = `gpg --armor --export-secret-keys <fingerprint>`, `GPG_PASSPHRASE` = its passphrase
   (leave unset for a key without a passphrase).
3. Publish the key fingerprint in README.md: the attached public key only proves something if users compare its
   fingerprint with one from an independent place.

Users verify a download with:

```
gpg --import DevAcademy-release-key.asc
gpg --verify SHA256SUMS.asc SHA256SUMS
sha256sum --check --ignore-missing SHA256SUMS
```

## Checking a release

The workflow summary lists a warning for every unsigned part. Before publishing the draft release, check that there is
none, or state in the release notes which platforms are unsigned.
