# PhiChat Backend

A secure messaging backend built with **ASP.NET Core 8**.\
PhiChat provides the server-side infrastructure for a real-time
end-to-end encrypted messaging system, including authentication, message
delivery, public-key distribution, and user communication features.

> Backend repository of PhiChat.

## Features

-   Real-time messaging using **SignalR**
-   End-to-end encryption: the server stores and relays only ciphertext
    (messages and attachments) and never sees keys that can decrypt them
-   Identity public keys with passphrase-encrypted private-key backups
-   JWT-based authentication
-   User registration and login
-   Phone-based authentication support
-   Message delivery and read status tracking
-   Reply and forward message support
-   File attachment support
-   Message reactions
-   Contact management
-   User online/offline status
-   Global exception handling middleware
-   Entity Framework Core database layer
-   Docker support

## Architecture

The project follows a layered architecture:

    Phichat.API
        └── Controllers, SignalR Hub, Middleware

    Phichat.Application
        └── DTOs, Interfaces, Validation Rules

    Phichat.Domain
        └── Entities and Core Models

    Phichat.Infrastructure
        └── Database, Services, External Implementations

## Security Approach

PhiChat is designed around end-to-end encrypted communication (see
[End-to-End Encryption](#end-to-end-encryption)):

-   Messages and attachments are encrypted on the clients; the backend
    stores and relays ciphertext only and holds no key that can decrypt it.
-   Authentication uses short-lived JWT access tokens (15 min) plus rotating
    refresh tokens in an HttpOnly cookie scoped to `/api/auth`; reuse of a
    rotated refresh token revokes the whole session.
-   Passwords are hashed with salted PBKDF2-HMAC-SHA512 (legacy hashes are
    upgraded transparently on login).
-   Phone sign-up requires a verified SMS code (per-code attempt limit,
    per-phone cooldown and hourly cap; codes are stored as keyed hashes).
-   Login, registration, SMS and upload endpoints are rate limited.
-   Every message operation checks that the caller is a participant;
    forwarded attachments are copied server-side, never taken from the client.
-   Uploaded files are served with `nosniff`, a sandboxing CSP, and as
    downloads unless they are images, audio or video.

## End-to-End Encryption

Encryption and decryption happen in the client (WebCrypto). The server
distributes public keys, stores encrypted key backups, and checks that
messages are in the encrypted format for the current keys.

-   **Identity key**: each account has one ECDH P-256 key pair. The key id
    is base64url of the first 16 bytes of SHA-256 over the public key
    (SPKI), so a key id also authenticates the key served for it. The
    server accepts only canonical, on-curve P-256 keys.
-   **Conversation key**: ECDH between the two identity keys, then
    HKDF-SHA256 (salted with both key ids) into an AES-256-GCM key.
-   **Message body**: `v2:{senderKeyId}:{recipientKeyId}:{base64(iv|ciphertext|tag)}`;
    the header is authenticated as AES-GCM additional data. The plaintext is
    a JSON envelope with the text and, for attachments, the file's own
    random AES-256-GCM key, name, type and size.
-   **Attachments** are encrypted before upload; the server only sees an
    opaque `attachment.bin`. Forwarding re-encrypts the envelope for the new
    chat and reuses the stored ciphertext.
-   **Backup**: the private key (PKCS#8) is encrypted on the client with
    AES-256-GCM under PBKDF2-SHA256 (600k iterations) of a recovery
    passphrase chosen by the user; the server stores only that ciphertext.
    A lost passphrase means a new key: earlier messages become unreadable.
-   **Key changes**: replaced keys are kept (revoked) so peers can still read
    earlier messages. Sends encrypted for an outdated key are rejected
    (`recipient_key_changed`, `sender_key_outdated`) so the client
    re-encrypts. Related users get an `IdentityKeyChanged` hub event, and
    clients show a warning plus a 60-digit security code to compare.

Key endpoints (all require authentication):

    GET  /api/keys/me              own key + encrypted backup (204 if none)
    POST /api/keys/me              publish the first key
    PUT  /api/keys/me              replace the key (lost passphrase)
    PUT  /api/keys/me/backup       re-encrypted backup (passphrase change)
    GET  /api/keys/{userId}        a user's active public key
    GET  /api/keys/{userId}/{keyId} a specific (possibly revoked) public key

Messages sent before end-to-end encryption used server-held keys; those keys
were deleted, so such messages can no longer be decrypted.

## Technologies

-   **.NET 8**
-   **ASP.NET Core Web API**
-   **SignalR**
-   **Entity Framework Core 8**
-   **SQL Server**
-   **JWT Authentication**
-   **FluentValidation**
-   **Docker**

## Project Structure

    Phichat
    │
    ├── Phichat.API
    │   ├── Controllers
    │   ├── Hubs
    │   └── Middleware
    │
    ├── Phichat.Application
    │   ├── DTOs
    │   ├── Interfaces
    │   └── Validators
    │
    ├── Phichat.Domain
    │   └── Entities
    │
    └── Phichat.Infrastructure
        ├── Data
        ├── Services
        └── Migrations

## Getting Started

### Requirements

-   .NET 8 SDK
-   SQL Server
-   Docker (optional)

### Configuration

Secrets are **not** stored in `appsettings.json`. The API refuses to start
until both of these are configured:

| Setting | Development | Production (environment variable) |
|---|---|---|
| `Jwt:Key` (at least 32 bytes, random) | user-secrets | `Jwt__Key` |
| `ConnectionStrings:DefaultConnection` | `appsettings.Development.json` (local SQL Express) or user-secrets | `ConnectionStrings__DefaultConnection` |

Set the JWT signing key for local development (run once per machine):

``` bash
dotnet user-secrets set "Jwt:Key" "<a-long-random-secret>" --project Phichat.API
```

A suitable key can be generated with `openssl rand -base64 64`.

To use a different database locally, override the connection string the same way:

``` bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project Phichat.API
```

Uploaded files (`Phichat.API/Uploads`, `Phichat.API/wwwroot/uploads`) and logs
are runtime data and are ignored by git; the folders are created on startup.

Web Push needs a VAPID key pair. Without configuration the API generates one on
first start and keeps it in `Phichat.API/App_Data/vapid-keys.json` (ignored by
git). In production set it explicitly - replacing it invalidates every browser
subscription:

| Setting | Environment variable |
|---|---|
| `Push:VapidPublicKey` (base64url, 65-byte P-256 point) | `Push__VapidPublicKey` |
| `Push:VapidPrivateKey` (base64url, 32 bytes) | `Push__VapidPrivateKey` |
| `Push:Subject` (`mailto:` or `https:` contact for push services) | `Push__Subject` |

### Database Migration

Run from the repository root (the design-time factory reads the same
appsettings, user-secrets and environment variables as the API):

``` bash
dotnet ef database update --project Phichat.Infrastructure --startup-project Phichat.API
```

### Run the API

``` bash
dotnet run --project Phichat.API
```

The API will start on the configured application URL.

## Pins, Blocking and Link Previews

-   **Pinned messages** are shared by both participants of a conversation
    (`GET /api/messages/pinned/{peerId}`, `POST|DELETE /api/messages/{id}/pin`,
    hub event `PinsChanged`). Only the message id is stored; content stays
    encrypted.
-   **Blocking** (`GET /api/users/blocked`, `POST|DELETE /api/users/{id}/block`)
    stops messages, reactions, pins and typing between the two users and
    hides presence and last seen both ways. The blocked user gets a neutral
    `cannot_message_user` error; the blocker gets `user_blocked`.
-   **Link previews** (`POST /api/link-preview` with `{ "url": ... }`) fetch
    Open Graph metadata for the sender's client, which puts the preview inside
    the encrypted message so recipients never contact the site. Requests are
    SSRF-guarded: http(s) on ports 80/443 only, every connection checked
    against public addresses after DNS resolution (private, loopback,
    link-local/metadata, CGNAT, NAT64/6to4 refused), redirects re-validated,
    size and time limits, per-user rate limit. The URL is sent in the body so
    it does not appear in request logs.

## Groups

Groups (`/api/groups`) have one owner, admins and members (at most 200).
Admins rename the group, change its photo and add or remove members; the
owner also promotes admins and can delete the group. An owner who leaves hands
the group to the longest-serving admin (or member); the last member leaving
deletes it. Membership changes are written into the chat as service messages
(`Message.SystemEvent`, JSON, not encrypted - the server knows the members
anyway).

Group messages are end-to-end encrypted with the same identity keys:

    g1:{senderKeyId}:{keyId}.{wrappedKey},...:{base64(iv || ciphertext || tag)}

The content is encrypted once with a random AES-256-GCM message key, which is
wrapped for every member with the pairwise ECDH key of the sender and that
member (HKDF label `phichat/v2/group-key-wrap`). The server only checks that the
body is wrapped for exactly the current members' active keys
(`group_keys_changed` otherwise, and the client re-encrypts). Additional data
binds the content to the group and sender key, and each wrapped key to the
group, both key ids and a hash of the content, so a member who knows a message
key cannot alter another member's message. Members only receive messages sent
after they joined. Read state is a per-member position (`MarkGroupRead`, event
`GroupRead`); a message shows as read once any other member has read it.

## Privacy, Sessions and Notifications

-   **Last seen** (`GET|PUT /api/settings/privacy`): everyone, my contacts or
    nobody. Presence is mutual - two users see each other's online status and
    last seen only when both settings allow it (and neither blocked the other).
    Hidden users are reported as `lastSeenHidden` ("last seen recently").
-   **Read receipts** in private chats flow only while both users have them on;
    the read state is still recorded for unread counts.
-   **Active sessions** (`GET /api/sessions`, `DELETE /api/sessions/{id}`,
    `DELETE /api/sessions/others`): one session per refresh-token family; access
    tokens carry it as the `sid` claim. Ending a session revokes its refresh
    tokens, rejects its access tokens at once (an in-memory revocation list -
    move it to a shared store when running several API instances), removes its
    push subscriptions and tells its open pages (`SessionTerminated`) to sign out.
-   **Muted chats** (`GET /api/settings/mutes`, `PUT|DELETE /api/settings/mutes/{chatId}`)
    are shared by the user's devices.
-   **Web Push** (`/api/push`): browsers subscribe with the VAPID key; new
    messages are pushed (RFC 8291 encryption, VAPID signing, implemented with
    .NET crypto) only to devices without a live connection, and never contain
    message text - only the sender or group name. Endpoints must belong to a
    known browser push service and connections are checked like link previews.

Request lines (`Microsoft.AspNetCore.Hosting.Diagnostics`) are not logged: the
hub's WebSocket URL carries the access token in its query string.

## Real-Time Communication

PhiChat uses SignalR for:

-   Sending messages instantly
-   Receiving online status updates
-   Tracking user presence
-   Delivering real-time events

Hub (clients pass the access token as the `access_token` query parameter):

    /hubs/chat

Events are addressed per user, so every open connection (several tabs,
a reload, a reconnect) receives them. A user is online while at least one
connection is open; online/offline and last-seen updates are sent only to
related users (conversation partners, contacts and members of the same groups)
whose privacy settings allow it.

## Tests

``` bash
dotnet test
```

Unit tests live in `Phichat.Tests` (presence tracking, password hashing,
upload file-name sanitizing and image signature checks, identity-key
validation and key-id compatibility with WebCrypto, the encrypted message
formats, the identity-key service against SQLite, pins and blocking, privacy
rules, sessions, mutes, Web Push encryption (RFC 8291 test vector) and VAPID,
and group membership, permissions, encryption checks and read state).

## API Documentation

Swagger is available in development mode:

    /swagger

## Screenshots / Demo

![Message Flow](./screenshots/demo.gif)

Recommended additions:

-   Swagger API overview screenshot
-   Database diagram
-   Real-time message flow GIF
-   Client + server communication demo

## Future Improvements

Possible improvements:

-   Forward secrecy (a ratchet per conversation) and per-device keys
-   Integration tests against a real database
-   Production deployment configuration

## License

This project is licensed under the terms defined in the repository
license.
