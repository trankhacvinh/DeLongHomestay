# Secure identity document storage

CCCD/identity images are intentionally **not** stored in `wwwroot`, Media Library, room image storage, or any static-file directory.

## Encryption envelope

- Cipher: AES-256-GCM.
- Master key: one random 32-byte key stored at `DataRoot/security/identity-master.key`.
- Nonce: 12 random bytes per write.
- Authentication tag: 16 bytes.
- Associated data binds ciphertext to property id, booking id, and side (`front`/`back`) so encrypted files cannot be swapped between bookings.
- Content type, original filename, and the image itself are inside the encrypted payload.
- File extension on disk: `.dlid`.

## Automatic key management

The application creates the master key automatically the first time secure identity storage is initialized. On Unix-like systems it also attempts to restrict the key file to owner read/write (`0600`). There is no normal deployment step for creating or copying a separate secret.

When moving servers, back up and restore the **entire `DataRoot`**, including both:

- `DataRoot/private/identity-documents/...`
- `DataRoot/security/identity-master.key`

If encrypted `.dlid` files already exist but the master key file is missing, the application deliberately refuses to generate a replacement key. This avoids silently making older CCCD unreadable. Restore the missing key from the same `DataRoot` backup.

For backward compatibility, if the previous `Security:IdentityDocumentEncryptionKeyBase64` setting is present and the new master key file does not yet exist, the application seeds `identity-master.key` with that existing key. After a successful start and backup of `DataRoot`, the external setting is no longer required.

This convenience model protects against accidental exposure of the CCCD directory or static-file misconfiguration, but it does **not** protect against an attacker who steals the complete `DataRoot`, because the encrypted files and master key are both in that backup boundary.

Authorized admin reads decrypt only in memory and send `Cache-Control: private,no-store`. Public writes require the opaque booking `Idempotency-Key` that created the website booking. The application never falls back to storing CCCD plaintext.
