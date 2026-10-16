# Security Policy

DevAcademy is a local desktop application that runs learner and lesson code on the user's machine and can store AI provider
keys. Security-relevant behaviour and its limits are described in [SECURITY_MODEL.md](SECURITY_MODEL.md); read the
"Isolation levels" section before reporting a sandbox escape — `ProcessLimits` is explicitly not a security boundary.

## Supported versions

There are no tagged releases yet. Only the `main` branch is supported; fixes land there.

## Reporting a vulnerability

Please **do not open a public issue**. Report privately through GitHub's "Report a vulnerability" (private security advisory) on
the repository's Security tab. If that is unavailable, contact the maintainers through the contact listed on the repository
profile. Include: affected component and commit, platform, reproduction steps, impact, and whether an API key or user
file was exposed.

What to expect: acknowledgement within 7 days, a fix or mitigation plan within 30 days for confirmed issues, credit in the
advisory unless you prefer otherwise. These are targets for a volunteer project, not guarantees.

## In scope

Escaping `OsSandbox`/`Container` isolation as documented, leaking API keys (SQLite, logs, exports, child environments), path
traversal when materialising lesson or learner files, content packages causing code execution outside the execution engine,
corruption of the local database through crafted import files.

## Out of scope

Behaviour of `ProcessLimits` as a security boundary, a malicious local administrator, kernel or hypervisor escapes, supply-chain
issues in toolchains the user installed, vulnerabilities in third-party AI providers, and attacks needing the user to paste a
harmful AI suggestion into a terminal themselves.
