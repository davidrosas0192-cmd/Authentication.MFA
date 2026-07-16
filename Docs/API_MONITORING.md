# Monitoring API

Base URL: `/api/monitor`

All endpoints are public — **no authentication required**. Maximum date range is 90 days. Maximum `pageSize` is 100.

---

## GET /api/monitor/summary — Dashboard Summary

Returns aggregated statistics for today's activity across all system areas.

**Response 200**
```json
{
  "loginsToday": 42,
  "loginFailuresToday": 5,
  "activeAccessSessions": 18,
  "activeRefreshSessions": 14,
  "pendingChallenges": 3,
  "lockedChallenges": 2,
  "enrollmentsInProgress": 7,
  "enrollmentsCompleted": 130,
  "securityWarningsToday": 4,
  "securityErrorsToday": 1,
  "usersTotal": 200,
  "usersActive": 195,
  "usersWithMfa": 150,
  "generatedAtUtc": "2026-07-16T12:00:00Z"
}
```

---

## GET /api/monitor/logins — Login History

Paginated login attempts from the authentication audit log.

**Query Parameters**

| Parameter  | Type     | Default | Description                          |
|------------|----------|---------|--------------------------------------|
| `userId`   | `long`   | —       | Filter by user ID                    |
| `outcome`  | `string` | —       | `success` or `failure`               |
| `method`   | `string` | —       | e.g. `password`, `mfa`               |
| `dateFrom` | `DateTime`| 90 days ago | Start date (UTC)              |
| `dateTo`   | `DateTime`| —       | End date (UTC)                       |
| `page`     | `int`    | `1`     | Page number                          |
| `pageSize` | `int`    | `20`    | Items per page (max 100)             |

**Example Request**
```
GET /api/monitor/logins?outcome=failure&page=1&pageSize=10
```

**Response 200**
```json
{
  "page": 1,
  "pageSize": 10,
  "totalCount": 28,
  "totalPages": 3,
  "items": [
    {
      "id": 1001,
      "occurredAtUtc": "2026-07-16T10:30:00Z",
      "userId": 5,
      "usernameOrEmail": "jdoe@example.com",
      "stage": "password_login",
      "method": "password",
      "outcome": "failure",
      "failureReason": "Invalid username or password.",
      "ipAddress": "192.168.1.10",
      "userAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X)"
    },
    {
      "id": 998,
      "occurredAtUtc": "2026-07-16T09:15:00Z",
      "userId": null,
      "usernameOrEmail": "unknown@test.com",
      "stage": "password_login",
      "method": "password",
      "outcome": "failure",
      "failureReason": "Invalid username or password.",
      "ipAddress": "10.0.0.5",
      "userAgent": "PostmanRuntime/7.36.0"
    }
  ]
}
```

---

## GET /api/monitor/enrollments — MFA Enrollment Sessions

Paginated MFA enrollment sessions, filterable by status.

**Query Parameters**

| Parameter  | Type     | Default | Description                          |
|------------|----------|---------|--------------------------------------|
| `status`   | `string` | —       | See statuses below                   |
| `userId`   | `long`   | —       | Filter by user ID                    |
| `dateFrom` | `DateTime`| 90 days ago | Start date (UTC)              |
| `dateTo`   | `DateTime`| —       | End date (UTC)                       |
| `page`     | `int`    | `1`     | Page number                          |
| `pageSize` | `int`    | `20`    | Items per page (max 100)             |

**Enrollment Statuses**

| Status                | Meaning                                    |
|-----------------------|--------------------------------------------|
| `pending_method`      | User selected no method yet                |
| `pending_verification`| OTP sent, waiting for code                 |
| `completed`           | Enrollment finished successfully           |
| `expired`             | Session expired before completion          |
| `cancelled`           | User cancelled                             |

**Example Request**
```
GET /api/monitor/enrollments?status=completed&page=1&pageSize=20
```

**Response 200**
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 130,
  "totalPages": 7,
  "items": [
    {
      "id": "a1b2c3d4-abcd-1234-ef56-7890abcdef12",
      "userId": 42,
      "status": "completed",
      "stepVersion": 3,
      "createdAtUtc": "2026-07-15T09:00:00Z",
      "updatedAtUtc": "2026-07-15T09:02:30Z",
      "expiresAtUtc": "2026-07-15T09:10:00Z",
      "completedAtUtc": "2026-07-15T09:02:30Z"
    }
  ]
}
```

---

## GET /api/monitor/challenges — MFA Challenges

Paginated MFA challenges, filterable by status, purpose, and method.

**Query Parameters**

| Parameter  | Type     | Default | Description                          |
|------------|----------|---------|--------------------------------------|
| `status`   | `string` | —       | See statuses below                   |
| `purpose`  | `string` | —       | `login` or `manage_mfa`              |
| `method`   | `string` | —       | `sms`, `email`, `recovery_code`, `fido2` |
| `userId`   | `long`   | —       | Filter by user ID                    |
| `dateFrom` | `DateTime`| 90 days ago | Start date (UTC)              |
| `dateTo`   | `DateTime`| —       | End date (UTC)                       |
| `page`     | `int`    | `1`     | Page number                          |
| `pageSize` | `int`    | `20`    | Items per page (max 100)             |

**Challenge Statuses**

| Status         | Meaning                                           |
|----------------|---------------------------------------------------|
| `pending`      | OTP sent, waiting for verification                |
| `verified`     | Code verified successfully                        |
| `consumed`     | Used to complete a login                          |
| `locked`       | Blocked after 5 failed attempts                   |
| `expired`      | Expired before verification                       |
| `revoked`      | Revoked manually or by session cancellation       |

**Example Request**
```
GET /api/monitor/challenges?status=locked
```

**Response 200**
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1,
  "items": [
    {
      "id": "d4e5f6a7-1234-5678-abcd-ef0123456789",
      "userId": 5,
      "purpose": "login",
      "method": "sms",
      "channel": "sms",
      "status": "locked",
      "failedAttempts": 5,
      "lastFailedAttemptAtUtc": "2026-07-16T11:00:15Z",
      "createdAtUtc": "2026-07-16T10:58:00Z",
      "expiresAtUtc": "2026-07-16T11:03:00Z",
      "verifiedAtUtc": null,
      "ipAddress": "192.168.1.10"
    }
  ]
}
```

---

## GET /api/monitor/sessions — Token Sessions

Paginated access and/or refresh token sessions.

**Query Parameters**

| Parameter    | Type     | Default | Description                             |
|--------------|----------|---------|-----------------------------------------|
| `userId`     | `long`   | —       | Filter by user ID                       |
| `type`       | `string` | both    | `access` or `refresh`                   |
| `onlyActive` | `bool`   | `false` | Only non-revoked, non-expired sessions  |
| `page`       | `int`    | `1`     | Page number                             |
| `pageSize`   | `int`    | `20`    | Items per page (max 100)                |

**Example Request**
```
GET /api/monitor/sessions?type=refresh&onlyActive=true&userId=5
```

**Response 200**
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 3,
  "totalPages": 1,
  "items": [
    {
      "id": "f7a8b9c0-1234-5678-abcd-ef0123456789",
      "type": "refresh",
      "userId": 5,
      "issuedAtUtc": "2026-07-16T10:00:00Z",
      "expiresAtUtc": "2026-07-21T10:00:00Z",
      "isRevoked": false,
      "revokedAtUtc": null,
      "revokeReason": null,
      "lastRotatedAtUtc": "2026-07-16T11:30:00Z",
      "ipAddress": "192.168.1.10",
      "userAgent": "Mozilla/5.0"
    }
  ]
}
```

---

## GET /api/monitor/security-events — Security Audit Events

Paginated security events, filterable by severity, category, and type.

**Query Parameters**

| Parameter   | Type     | Default | Description                              |
|-------------|----------|---------|------------------------------------------|
| `severity`  | `string` | —       | `Information`, `Warning`, `Error`, `Critical` |
| `category`  | `string` | —       | e.g. `Authentication`                   |
| `eventType` | `string` | —       | e.g. `auth.refresh_token_rejected`      |
| `outcome`   | `string` | —       | `success` or `failure`                  |
| `userId`    | `long`   | —       | Filter by user ID                       |
| `dateFrom`  | `DateTime`| 90 days ago | Start date (UTC)                |
| `dateTo`    | `DateTime`| —       | End date (UTC)                          |
| `page`      | `int`    | `1`     | Page number                             |
| `pageSize`  | `int`    | `20`    | Items per page (max 100)                |

**Example Request**
```
GET /api/monitor/security-events?severity=Warning&dateFrom=2026-07-16
```

**Response 200**
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 4,
  "totalPages": 1,
  "items": [
    {
      "id": 2001,
      "occurredAtUtc": "2026-07-16T11:05:00Z",
      "category": "Authentication",
      "eventType": "auth.refresh_token_rejected",
      "severity": "Warning",
      "outcome": "failure",
      "userId": 5,
      "usernameOrEmail": null,
      "ipAddress": "192.168.1.10",
      "failureReason": "Token expired",
      "requestPath": "/api/sessions/refresh",
      "httpMethod": "POST"
    },
    {
      "id": 1998,
      "occurredAtUtc": "2026-07-16T10:58:30Z",
      "category": "Authentication",
      "eventType": "auth.password.login_rate_limited",
      "severity": "Warning",
      "outcome": "failure",
      "userId": null,
      "usernameOrEmail": null,
      "ipAddress": "10.0.0.5",
      "failureReason": "Rate limit exceeded",
      "requestPath": "/api/sessions",
      "httpMethod": "POST"
    }
  ]
}
```

---

## GET /api/monitor/users — Users Summary

Paginated user list with their enabled MFA methods.

**Query Parameters**

| Parameter  | Type     | Default | Description                          |
|------------|----------|---------|--------------------------------------|
| `isActive` | `bool`   | —       | Filter active/inactive users         |
| `hasMfa`   | `bool`   | —       | Filter users with/without MFA        |
| `page`     | `int`    | `1`     | Page number                          |
| `pageSize` | `int`    | `20`    | Items per page (max 100)             |

**Example Request**
```
GET /api/monitor/users?hasMfa=false&isActive=true
```

**Response 200**
```json
{
  "page": 1,
  "pageSize": 20,
  "totalCount": 50,
  "totalPages": 3,
  "items": [
    {
      "id": 7,
      "username": "newuser",
      "email": "newuser@example.com",
      "isActive": true,
      "isFido2Enabled": false,
      "createdAtUtc": "2026-07-10T08:00:00Z",
      "lastLoginAtUtc": "2026-07-16T09:45:00Z",
      "mfaMethodCount": 0,
      "mfaMethods": []
    }
  ]
}
```
