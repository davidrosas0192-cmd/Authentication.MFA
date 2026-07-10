# wwwroot Test Client Plan

## Objective

Build a friendly web client inside `wwwroot` to test authentication, MFA, and FIDO2 endpoints with full request/response visibility.

## Functional Requirements

1. Register users from the UI (public user creation endpoint).
2. Run login and handle two results:
   - fully authenticated (`Authenticated`)
   - MFA required (`RequiresMfa`)
3. When login returns `Authenticated` and `AvailableMfaSetupOptions` contains values, show an MFA setup screen with 3 options:
   - sms
   - email
   - fido2 passwordless
4. When login returns `RequiresMfa`, the endpoint must include `AllowedMfaMethods`, and the client must store that list.
5. Show a method/device selection screen based on `AllowedMfaMethods`.
6. From that screen, once a method is selected, hide non-selected verification flows and show only the relevant flow card.
7. When fully authenticated, hide verification-method selection and show only setup/enrollment options.
8. After selecting a setup option, hide non-selected enrollment flows and show only the relevant enrollment card.
9. Show endpoint guidance after method selection, listing the exact endpoints to call.
10. Support MFA challenge flow with MFA temp token:
   - start challenge
   - verify challenge
11. Support FIDO2 login challenge flow with MFA temp token:
   - create options
   - complete login
12. Allow enrollment only when fully authenticated (full access token):
   - MFA enrollment (start/verify)
   - FIDO2 enrollment (options/complete)
13. Show API responses in a visible response box with:
   - timestamp
   - endpoint/method
   - status code
   - formatted JSON body
14. Keep console output scoped to the current action button (new action clears prior console entries).

## Proposed UX

1. Two-pane layout:
   - left: flow forms
   - right: response console
2. Session status header:
   - full token available/not available
   - MFA temp token available/not available
   - current mfaTransactionId
3. MFA method/device selection screen:
   - card list from `AllowedMfaMethods`
   - each card explains which endpoint is called
   - CTA per method: `Use SMS`, `Use Email`, `Use FIDO2`
   - after selection, only the chosen verification flow remains visible
4. MFA setup screen for authenticated users without configured MFA:
   - activated when `AvailableMfaSetupOptions` has values
   - shows three cards: `Setup SMS`, `Setup Email`, `Setup FIDO2 Passwordless`
   - uses full token to start enrollment
   - after selection, only the chosen enrollment flow remains visible
5. Utility actions:
   - clear console
   - clear session
   - copy token
   - export console JSON
6. Clear messaging:
   - required field validation
   - network error handling
   - success/error feedback per action

## Frontend Architecture (wwwroot)

1. `wwwroot/index.html`
   - layout, forms, and UI containers
2. `wwwroot/styles.css`
   - friendly responsive styling
3. `wwwroot/app.js`
   - global session state
   - HTTP client (fetch)
   - endpoint -> required token mapping
   - `AllowedMfaMethods`/`AvailableMfaSetupOptions` -> UI screen/action mapping
   - state-driven card visibility (selected flow only)
   - endpoint hints per selected method
   - response box rendering
4. `wwwroot/webauthn.js`
   - base64url <-> ArrayBuffer conversions
   - `navigator.credentials.create/get` helpers

## Token Handling

1. Store in memory + `localStorage`:
   - access token
   - MFA temp token
   - MFA transaction id
   - allowed MFA methods
   - available MFA setup options
2. Resolve token automatically per endpoint:
   - MFA/FIDO2 login challenge -> MFA temp token
   - enrollment -> full access token
3. Add `Authorization: Bearer ...` only when required.

## Expected E2E Flow

1. Create user (`POST /api/users`).
2. Login (`POST /api/auth/login`).
3. If `Authenticated` and `AvailableMfaSetupOptions` has values:
   - show MFA setup screen with `sms`, `email`, `fido2`
   - after selection, show only the selected enrollment flow
   - show endpoints for the selected enrollment method
   - run selected enrollment flow with full token
4. If `RequiresMfa`:
   - read `AllowedMfaMethods`
   - show available method/device selection screen
5. User selects a method:
   - show only the selected verification flow
   - show endpoints for the selected verification method
   - use MFA temp token for:
     - `POST /api/mfa/challenges/start`
     - `POST /api/mfa/challenges/verify`
     - or `POST /api/fido2/login/options` + `POST /api/fido2/login/complete`
6. After successful MFA/FIDO2 login:
   - store full access token
7. With full token:
   - `POST /api/mfa/enrollment/start`
   - `POST /api/mfa/enrollment/verify`
   - `POST /api/fido2/enrollment/options`
   - `POST /api/fido2/enrollment/complete`

## FIDO2 Integration Notes

1. WebAuthn requires a secure context (`https` or `localhost`).
2. Adapt backend JSON payloads to browser buffer types.
3. Capture WebAuthn errors and display them in the response box.

## Acceptance Criteria

1. User can be created from the interface.
2. Login supports both scenarios (`Authenticated` and `RequiresMfa`).
3. If login returns `Authenticated` and setup options are available, the UI shows MFA setup options (`sms`, `email`, `fido2`).
4. If login requires MFA, the UI shows methods/devices from `AllowedMfaMethods`.
5. MFA temp token can be used to complete MFA or FIDO2 login.
6. After full authentication, enrollment flows are enabled.
7. Every API call is clearly logged in the response box.
8. Console output is scoped to the currently triggered action.

## Implementation Phases

1. Phase 1: Base UI shell + response console + session state.
2. Phase 2: User registration + login + token persistence + `AllowedMfaMethods`/`AvailableMfaSetupOptions` storage.
3. Phase 3: MFA setup screen for authenticated users with available setup options.
4. Phase 4: Method/device selection screen for `RequiresMfa`.
5. Phase 5: MFA challenge flow.
6. Phase 6: FIDO2 login flow.
7. Phase 7: MFA and FIDO2 enrollment with full token.
8. Phase 8: UX polish, validation, and manual end-to-end testing.

## Current implementation status

- Implemented: state-driven visibility, endpoint hints, action-scoped logs, responsive console rendering, sample fillers, and JSON export.
- Remaining enhancements are optional UX refinements only.
