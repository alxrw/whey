## Auth and registration probes

| Probe | Expected | Actual |  |
|---|---|---|---|
| Register first client | OK | OK | pass |
| Register a second, distinct client | OK | OK | pass |
| Re-register an existing key | AlreadyExists | AlreadyExists: public key is already registered | pass |
| Stored key is the client's public key | 54060F6B0FF0DB085B184356A9C5F1970EE4469EEE493310FDA205135B9FC8C2 | 54060F6B,679F86EC | pass |
| Correctly signed request from a registered client | OK | OK | pass |
| ReportInstall increments the package's TotalInteractions (scheduler input) | 1 | 1 | pass |
| ReportInstall for an untracked package | NotFound | NotFound: Package is not tracked. | pass |
| No credentials | Unauthenticated | Unauthenticated: Auth headers missing | pass |
| Signed body swapped after signing (tampered payload) | Unauthenticated | Unauthenticated: Invalid signature | pass |
| Signature made by a different key | Unauthenticated | Unauthenticated: Invalid signature | pass |
| Signature for a different method | Unauthenticated | Unauthenticated: Invalid signature | pass |
| Valid key + signature, unregistered client | Unauthenticated | Unauthenticated: Client unregistered | pass |
| Timestamp 10 minutes old (stale) | Unauthenticated | Unauthenticated: Stale or invalid timestamp | pass |
| Timestamp 10 minutes ahead | Unauthenticated | Unauthenticated: Stale or invalid timestamp | pass |
| Nonce used for the first time | OK | OK | pass |
| Exact replay of that request | Unauthenticated | Unauthenticated: Nonce already used | pass |
| Garbage public key / signature bytes | Unauthenticated | Unauthenticated: Invalid signature | pass |

**17/17 probes pass; 9/9 attack requests rejected.**

