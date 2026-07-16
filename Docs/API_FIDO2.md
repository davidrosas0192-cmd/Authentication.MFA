# FIDO2 API

Base URL: `/api/fido2`

FIDO2 (WebAuthn) allows passwordless authentication using hardware security keys, biometrics, or platform authenticators (Face ID, Windows Hello, etc.).

**Auth Schemes:**
- `Bearer <access_token>` — For enrollment endpoints
- `Bearer <mfa_token>` — For login/authentication endpoints

> The FIDO2 login flow is triggered after calling `POST /api/mfa/challenges` with `"method": "fido2"`, which sets the challenge status to `pending` and directs the client to use these endpoints.

---

## POST /api/fido2/enrollments — Create Enrollment Options

Returns a WebAuthn `PublicKeyCredentialCreationOptions` object. The client uses this to prompt the user to register their FIDO2 device.

**Headers**
```
Authorization: Bearer <access_token>
```

**Response 200**
```json
{
  "success": true,
  "data": {
    "transactionId": "f1e2d3c4-1234-5678-abcd-ef0123456789",
    "options": {
      "rp": {
        "name": "Authentication Fido2 Local",
        "id": "localhost"
      },
      "user": {
        "id": "dXNlcklkNDI=",
        "name": "jdoe@example.com",
        "displayName": "jdoe"
      },
      "challenge": "cmFuZG9tQ2hhbGxlbmdlQnl0ZXM=",
      "pubKeyCredParams": [
        { "type": "public-key", "alg": -7 },
        { "type": "public-key", "alg": -257 }
      ],
      "timeout": 60000,
      "attestation": "none",
      "authenticatorSelection": {
        "userVerification": "preferred",
        "residentKey": "preferred"
      }
    }
  }
}
```

---

## PATCH /api/fido2/enrollments/current — Complete FIDO2 Enrollment

Submits the authenticator's attestation response to complete registration.

**Headers**
```
Authorization: Bearer <access_token>
```

**Request**

The body is the raw `PublicKeyCredential` JSON from `navigator.credentials.create()`.

```json
{
  "transactionId": "f1e2d3c4-1234-5678-abcd-ef0123456789",
  "attestationResponse": {
    "id": "credentialIdBase64Url",
    "rawId": "credentialIdBase64Url",
    "type": "public-key",
    "response": {
      "clientDataJSON": "eyJ0eXBlIjoid2ViYXV0aG4uY3JlYXRlIiwiY2hhbGxlbmdlIjoiLi4uIn0=",
      "attestationObject": "o2NmbXRkbm9uZWdhdHRTdG10oGhhdXRoRGF0YVkBZ..."
    }
  }
}
```

**Response 200**
```json
{
  "success": true,
  "message": "FIDO2 credential enrolled successfully.",
  "data": {
    "credentialId": "credentialIdBase64Url",
    "deviceName": "Touch ID - MacBook Pro",
    "enrolledAtUtc": "2026-07-16T12:00:00Z"
  }
}
```

**Response 400 — Attestation verification failed**
```json
{
  "status": 400,
  "title": "Bad Request",
  "detail": "FIDO2 attestation verification failed."
}
```

---

## POST /api/fido2/authentications — Create Login Options

Returns a WebAuthn `PublicKeyCredentialRequestOptions` object to authenticate with a registered device.

> Must be called after `POST /api/mfa/challenges` with `"method": "fido2"` to set the MFA challenge to `pending`.

**Headers**
```
Authorization: Bearer <mfa_token>
```

**Request**
```json
{
  "mfaTransactionId": "d4e5f6a7-1234-5678-abcd-ef0123456789"
}
```

**Response 200**
```json
{
  "success": true,
  "data": {
    "transactionId": "d4e5f6a7-1234-5678-abcd-ef0123456789",
    "options": {
      "challenge": "cmFuZG9tQ2hhbGxlbmdlQnl0ZXM=",
      "timeout": 60000,
      "rpId": "localhost",
      "allowCredentials": [
        {
          "type": "public-key",
          "id": "credentialIdBase64Url"
        }
      ],
      "userVerification": "preferred"
    }
  }
}
```

---

## PATCH /api/fido2/authentications/current — Complete FIDO2 Login

Submits the authenticator's assertion response to verify the login. On success, returns full access + refresh tokens.

**Headers**
```
Authorization: Bearer <mfa_token>
```

**Request**

The body is the raw `PublicKeyCredential` JSON from `navigator.credentials.get()`.

```json
{
  "assertionResponse": {
    "id": "credentialIdBase64Url",
    "rawId": "credentialIdBase64Url",
    "type": "public-key",
    "response": {
      "clientDataJSON": "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0IiwiY2hhbGxlbmdlIjoiLi4uIn0=",
      "authenticatorData": "SZYN5YgOjGh0NBcPZHZgW4/krrmihjLHmVzzuoMdl2MBAAAABg==",
      "signature": "MEQCIBp2F...",
      "userHandle": null
    }
  }
}
```

**Response 200 — Authenticated**
```json
{
  "success": true,
  "message": "FIDO2 authentication succeeded.",
  "data": {
    "status": "Authenticated",
    "mfaRequired": false,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "bmV3UmVmcmVzaFRva2VuSGVyZQ...",
    "expiresIn": 900,
    "allowedMfaMethods": ["fido2", "sms"]
  }
}
```

**Response 401 — Assertion failed**
```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "FIDO2 assertion verification failed."
}
```

---

## Full FIDO2 Login Flow

```
1. POST /api/sessions              → { mfaToken, allowedMfaMethods: ["fido2"] }
2. POST /api/mfa/challenges        → { method: "fido2" } → { mfaTransactionId, status: "pending" }
3. POST /api/fido2/authentications → { mfaTransactionId } → { options }
4. [Browser] navigator.credentials.get(options) → assertionResponse
5. PATCH /api/fido2/authentications/current → { assertionResponse } → { accessToken, refreshToken }
```

## Full FIDO2 Enrollment Flow

```
1. POST /api/fido2/enrollments         → { options }
2. [Browser] navigator.credentials.create(options) → attestationResponse
3. PATCH /api/fido2/enrollments/current → { transactionId, attestationResponse } → { credentialId }
```
