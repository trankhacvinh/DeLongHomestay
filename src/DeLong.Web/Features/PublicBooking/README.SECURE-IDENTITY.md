# Secure identity document storage

CCCD/identity images are intentionally **not** stored in `wwwroot`, Media Library, room image storage, or any static-file directory.

## Encryption envelope

- Cipher: AES-256-GCM.
- Key: `Security:IdentityDocumentEncryptionKeyBase64`, exactly 32 random bytes encoded as Base64.
- Nonce: 12 random bytes per write.
- Authentication tag: 16 bytes.
- Associated data binds ciphertext to property id, booking id, and side (`front`/`back`) so encrypted files cannot be swapped between bookings.
- Content type, original filename, and the image itself are inside the encrypted payload.
- File extension on disk: `.dlid`.

The encryption key must be provided by environment/secret management and must not be committed to Git or stored alongside `DataRoot`. If the key is unavailable or invalid, identity storage is disabled and the application never falls back to plaintext.

Authorized admin reads decrypt in memory and send `Cache-Control: private,no-store`. Public writes require the opaque booking `Idempotency-Key` that created the website booking.
