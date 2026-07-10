(() => {
  const STORAGE_KEY = "mfa_client_state_v1";
  const ALL_MFA_OPTIONS = ["sms", "email", "fido2"];

  const logs = [];

  const state = {
    apiBaseUrl: "",
    accessToken: null,
    refreshToken: null,
    mfaToken: null,
    mfaTransactionId: null,
    allowedMfaMethods: [],
    availableMfaSetupOptions: [],
    lastLoginUsername: "",
    pendingFido2LoginOptions: null,
    pendingFido2LoginTransactionId: null,
    pendingFido2EnrollOptions: null,
    pendingFido2EnrollTransactionId: null,
    lastEnrollmentMethod: null,
    selectedVerifyMethod: null,
    selectedSetupMethod: null,
  };

  const refs = {
    apiBaseUrl: document.getElementById("apiBaseUrl"),
    fullTokenBadge: document.getElementById("fullTokenBadge"),
    mfaTokenBadge: document.getElementById("mfaTokenBadge"),
    mfaTxBadge: document.getElementById("mfaTxBadge"),
    accessTokenView: document.getElementById("accessTokenView"),
    mfaTokenView: document.getElementById("mfaTokenView"),
    clearSessionBtn: document.getElementById("clearSessionBtn"),
    authActionBtn: document.getElementById("authActionBtn"),
    copyAccessBtn: document.getElementById("copyAccessBtn"),
    copyMfaBtn: document.getElementById("copyMfaBtn"),
    clearConsoleBtn: document.getElementById("clearConsoleBtn"),
    consoleOutput: document.getElementById("consoleOutput"),
    exportConsoleBtn: document.getElementById("exportConsoleBtn"),

    createUserForm: document.getElementById("createUserForm"),
    createUserCard: document.getElementById("createUserCard"),
    fillCreateUserBtn: document.getElementById("fillCreateUserBtn"),
    createUsername: document.getElementById("createUsername"),
    createEmail: document.getElementById("createEmail"),
    createPassword: document.getElementById("createPassword"),

    loginForm: document.getElementById("loginForm"),
    loginCard: document.getElementById("loginCard"),
    fillLoginBtn: document.getElementById("fillLoginBtn"),
    loginUsername: document.getElementById("loginUsername"),
    loginPassword: document.getElementById("loginPassword"),

    setupMfaCard: document.getElementById("setupMfaCard"),
    setupOptionsGrid: document.getElementById("setupOptionsGrid"),
    setupEndpointsHint: document.getElementById("setupEndpointsHint"),

    mfaMethodsCard: document.getElementById("mfaMethodsCard"),
    mfaMethodsGrid: document.getElementById("mfaMethodsGrid"),
    verifyEndpointsHint: document.getElementById("verifyEndpointsHint"),

    startMfaChallengeForm: document.getElementById("startMfaChallengeForm"),
    mfaChallengeCard: document.getElementById("mfaChallengeCard"),
    fillMfaChallengeBtn: document.getElementById("fillMfaChallengeBtn"),
    challengeMethod: document.getElementById("challengeMethod"),
    challengeTx: document.getElementById("challengeTx"),

    verifyMfaChallengeForm: document.getElementById("verifyMfaChallengeForm"),
    verifyChallengeTx: document.getElementById("verifyChallengeTx"),
    verifyChallengeCode: document.getElementById("verifyChallengeCode"),

    fido2LoginOptionsBtn: document.getElementById("fido2LoginOptionsBtn"),
    fido2LoginCompleteBtn: document.getElementById("fido2LoginCompleteBtn"),
    fido2LoginCard: document.getElementById("fido2LoginCard"),

    startEnrollmentForm: document.getElementById("startEnrollmentForm"),
    mfaEnrollmentCard: document.getElementById("mfaEnrollmentCard"),
    fillEnrollmentBtn: document.getElementById("fillEnrollmentBtn"),
    enrollMethod: document.getElementById("enrollMethod"),
    enrollContact: document.getElementById("enrollContact"),

    verifyEnrollmentForm: document.getElementById("verifyEnrollmentForm"),
    enrollTx: document.getElementById("enrollTx"),
    enrollCode: document.getElementById("enrollCode"),

    fido2EnrollOptionsBtn: document.getElementById("fido2EnrollOptionsBtn"),
    fido2EnrollCompleteBtn: document.getElementById("fido2EnrollCompleteBtn"),
    fido2EnrollmentCard: document.getElementById("fido2EnrollmentCard"),
  };

  function boot() {
    hydrateState();
    wireEvents();
    render();
  }

  function wireEvents() {
    refs.apiBaseUrl.addEventListener("change", () => {
      state.apiBaseUrl = refs.apiBaseUrl.value.trim();
      persistState();
    });

    refs.clearConsoleBtn.addEventListener("click", () => {
      logs.length = 0;
      refs.consoleOutput.innerHTML = "";
    });
    refs.exportConsoleBtn.addEventListener("click", exportLogs);

    refs.clearSessionBtn.addEventListener("click", clearSession);
    refs.authActionBtn.addEventListener("click", onAuthAction);
    refs.copyAccessBtn.addEventListener("click", () => copyText(state.accessToken, "Access token copied."));
    refs.copyMfaBtn.addEventListener("click", () => copyText(state.mfaToken, "MFA token copied."));

    refs.fillCreateUserBtn.addEventListener("click", fillCreateUserSample);
    refs.fillLoginBtn.addEventListener("click", fillLoginFromLastCreated);
    refs.fillMfaChallengeBtn.addEventListener("click", fillMfaChallengeFromSession);
    refs.fillEnrollmentBtn.addEventListener("click", fillEnrollmentSample);

    refs.createUserForm.addEventListener("submit", onCreateUser);
    refs.loginForm.addEventListener("submit", onLogin);
    refs.startMfaChallengeForm.addEventListener("submit", onStartMfaChallenge);
    refs.verifyMfaChallengeForm.addEventListener("submit", onVerifyMfaChallenge);

    refs.fido2LoginOptionsBtn.addEventListener("click", onFido2LoginOptions);
    refs.fido2LoginCompleteBtn.addEventListener("click", onFido2LoginComplete);

    refs.startEnrollmentForm.addEventListener("submit", onStartEnrollment);
    refs.verifyEnrollmentForm.addEventListener("submit", onVerifyEnrollment);

    refs.fido2EnrollOptionsBtn.addEventListener("click", onFido2EnrollOptions);
    refs.fido2EnrollCompleteBtn.addEventListener("click", onFido2EnrollComplete);
  }

  function hydrateState() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        refs.apiBaseUrl.value = "";
        return;
      }

      const parsed = JSON.parse(raw);
      Object.assign(state, parsed);
      refs.apiBaseUrl.value = state.apiBaseUrl || "";
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  function persistState() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  }

  function clearSession() {
    state.accessToken = null;
    state.refreshToken = null;
    state.mfaToken = null;
    state.mfaTransactionId = null;
    state.allowedMfaMethods = [];
    state.availableMfaSetupOptions = [];
    state.pendingFido2LoginOptions = null;
    state.pendingFido2LoginTransactionId = null;
    state.pendingFido2EnrollOptions = null;
    state.pendingFido2EnrollTransactionId = null;
    state.lastEnrollmentMethod = null;
    state.selectedVerifyMethod = null;
    state.selectedSetupMethod = null;

    persistState();
    render();
    logInfo("Session cleared.");
  }

  function render() {
    renderBadges();
    renderTokens();
    renderMfaMethods();
    renderMfaSetupOptions();
    renderEndpointHints();
    renderFlowVisibility();
    renderTransactionFields();
  }

  function renderBadges() {
    refs.fullTokenBadge.className = `badge ${state.accessToken ? "on" : "off"}`;
    refs.fullTokenBadge.textContent = `Full Token: ${state.accessToken ? "Ready" : "Missing"}`;

    refs.mfaTokenBadge.className = `badge ${state.mfaToken ? "warn" : "off"}`;
    refs.mfaTokenBadge.textContent = `MFA Temp Token: ${state.mfaToken ? "Ready" : "Missing"}`;

    refs.mfaTxBadge.className = `badge ${state.mfaTransactionId ? "warn" : "off"}`;
    refs.mfaTxBadge.textContent = `MFA Tx: ${state.mfaTransactionId || "None"}`;

    if (state.accessToken) {
      refs.authActionBtn.hidden = false;
      refs.authActionBtn.textContent = "Logout";
    } else if (state.mfaToken) {
      refs.authActionBtn.hidden = false;
      refs.authActionBtn.textContent = "Cancel Authentication";
    } else {
      refs.authActionBtn.hidden = true;
      refs.authActionBtn.textContent = "Logout";
    }
  }

  function renderTokens() {
    refs.accessTokenView.value = state.accessToken || "";
    refs.mfaTokenView.value = state.mfaToken || "";
  }

  function renderTransactionFields() {
    refs.challengeTx.value = state.mfaTransactionId || refs.challengeTx.value;
    refs.verifyChallengeTx.value = state.mfaTransactionId || refs.verifyChallengeTx.value;
  }

  function renderMfaMethods() {
    const methods = (state.allowedMfaMethods || []).map((x) => (x || "").toLowerCase());
    const show = !!state.mfaToken && !state.accessToken && methods.length > 0;
    refs.mfaMethodsCard.hidden = !show;

    refs.mfaMethodsGrid.innerHTML = "";
    if (!show) {
      return;
    }

    methods.forEach((method) => {
      const btn = document.createElement("button");
      btn.className = "option-btn";
      btn.type = "button";
      btn.innerHTML = `<strong>${method.toUpperCase()}</strong><span>Select this method flow.</span>`;
      btn.addEventListener("click", () => {
        state.selectedVerifyMethod = method;
        state.selectedSetupMethod = null;

        if (method === "sms" || method === "email") {
          refs.challengeMethod.value = method;
          refs.challengeTx.value = state.mfaTransactionId || "";
          refs.verifyChallengeTx.value = state.mfaTransactionId || "";
        } else if (method === "fido2") {
          refs.challengeTx.value = state.mfaTransactionId || "";
          refs.verifyChallengeTx.value = state.mfaTransactionId || "";
        }

        persistState();
        render();

        if (method === "fido2") {
          focusCard(refs.fido2LoginCard);
        } else {
          focusCard(refs.mfaChallengeCard);
        }
      });
      refs.mfaMethodsGrid.appendChild(btn);
    });
  }

  function renderMfaSetupOptions() {
    const options = (state.availableMfaSetupOptions || []).map((x) => (x || "").toLowerCase());
    const show = !!state.accessToken && options.length > 0;
    refs.setupMfaCard.hidden = !show;

    refs.setupOptionsGrid.innerHTML = "";
    if (!show) {
      return;
    }

    options.forEach((method) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "option-btn";

      const label = method === "fido2" ? "Setup FIDO2 Passwordless" : `Setup ${method.toUpperCase()}`;
      btn.innerHTML = `<strong>${label}</strong><span>Starts the enrollment flow with full token.</span>`;

      btn.addEventListener("click", () => {
        state.selectedSetupMethod = method;
        state.selectedVerifyMethod = null;

        if (method === "sms" || method === "email") {
          refs.enrollMethod.value = method;
          refs.enrollContact.focus();
        } else if (method === "fido2") {
          refs.enrollMethod.value = "sms";
        }

        persistState();
        render();

        if (method === "fido2") {
          focusCard(refs.fido2EnrollmentCard);
        } else {
          focusCard(refs.mfaEnrollmentCard);
        }
      });

      refs.setupOptionsGrid.appendChild(btn);
    });
  }

  function renderEndpointHints() {
    const verify = (state.selectedVerifyMethod || "").toLowerCase();
    if (verify === "sms" || verify === "email") {
      refs.verifyEndpointsHint.hidden = false;
      refs.verifyEndpointsHint.innerHTML =
        `<strong>Use these endpoints:</strong><br/><code>POST /api/mfa/challenges/start</code><br/><code>POST /api/mfa/challenges/verify</code>`;
    } else if (verify === "fido2") {
      refs.verifyEndpointsHint.hidden = false;
      refs.verifyEndpointsHint.innerHTML =
        `<strong>Use these endpoints:</strong><br/><code>POST /api/fido2/login/options</code><br/><code>POST /api/fido2/login/complete</code>`;
    } else {
      refs.verifyEndpointsHint.hidden = true;
      refs.verifyEndpointsHint.innerHTML = "";
    }

    const setup = (state.selectedSetupMethod || "").toLowerCase();
    if (setup === "sms" || setup === "email") {
      refs.setupEndpointsHint.hidden = false;
      refs.setupEndpointsHint.innerHTML =
        `<strong>Use these endpoints:</strong><br/><code>POST /api/mfa/enrollment/start</code><br/><code>POST /api/mfa/enrollment/verify</code>`;
    } else if (setup === "fido2") {
      refs.setupEndpointsHint.hidden = false;
      refs.setupEndpointsHint.innerHTML =
        `<strong>Use these endpoints:</strong><br/><code>POST /api/fido2/enrollment/options</code><br/><code>POST /api/fido2/enrollment/complete</code>`;
    } else {
      refs.setupEndpointsHint.hidden = true;
      refs.setupEndpointsHint.innerHTML = "";
    }
  }

  function renderFlowVisibility() {
    const hasFullToken = !!state.accessToken;
    const hasMfaToken = !!state.mfaToken && !hasFullToken;

    const selectedVerifyMethod = (state.selectedVerifyMethod || "").toLowerCase();
    const selectedSetupMethod = (state.selectedSetupMethod || "").toLowerCase();

    refs.mfaMethodsCard.hidden = !(hasMfaToken && (state.allowedMfaMethods || []).length > 0);
    refs.mfaChallengeCard.hidden = !(hasMfaToken && (selectedVerifyMethod === "sms" || selectedVerifyMethod === "email"));
    refs.fido2LoginCard.hidden = !(hasMfaToken && selectedVerifyMethod === "fido2");

    refs.setupMfaCard.hidden = !(hasFullToken && (state.availableMfaSetupOptions || []).length > 0);
    refs.mfaEnrollmentCard.hidden = !(hasFullToken && (selectedSetupMethod === "sms" || selectedSetupMethod === "email"));
    refs.fido2EnrollmentCard.hidden = !(hasFullToken && selectedSetupMethod === "fido2");
  }

  function baseUrl() {
    const value = (state.apiBaseUrl || "").trim();
    return value.endsWith("/") ? value.slice(0, -1) : value;
  }

  function endpoint(path) {
    const root = baseUrl();
    return root ? `${root}${path}` : path;
  }

  async function apiCall(path, method, body, authType) {
    const url = endpoint(path);
    const headers = {
      "Content-Type": "application/json",
    };

    if (authType === "full") {
      if (!state.accessToken) {
        throw new Error("Full access token is missing.");
      }
      headers.Authorization = `Bearer ${state.accessToken}`;
    }

    if (authType === "mfa") {
      if (!state.mfaToken) {
        throw new Error("MFA temp token is missing.");
      }
      headers.Authorization = `Bearer ${state.mfaToken}`;
    }

    const startedAt = new Date();
    let response;
    let parsed = null;
    let raw = "";

    try {
      response = await fetch(url, {
        method,
        headers,
        body: body == null ? undefined : JSON.stringify(body),
      });

      raw = await response.text();
      if (raw) {
        try {
          parsed = JSON.parse(raw);
        } catch {
          parsed = { raw };
        }
      }

      appendLog({
        startedAt,
        method,
        path,
        requestBody: body,
        status: response.status,
        responseBody: parsed || raw,
      });

      const success = !!(parsed && parsed.success === true);
      return {
        ok: response.ok,
        success,
        status: response.status,
        payload: parsed,
        data: parsed && typeof parsed === "object" ? parsed.data : null,
      };
    } catch (error) {
      appendLog({
        startedAt,
        method,
        path,
        requestBody: body,
        status: "NETWORK_ERROR",
        responseBody: { message: error.message },
      });
      throw error;
    }
  }

  function appendLog(entry) {
    logs.push({
      timestamp: entry.startedAt.toISOString(),
      method: entry.method,
      path: entry.path,
      status: entry.status,
      requestBody: entry.requestBody,
      responseBody: entry.responseBody,
    });

    const block = document.createElement("div");
    block.className = "log-item";

    const when = entry.startedAt.toLocaleTimeString();
    const head = document.createElement("div");
    head.className = "log-head";
    head.textContent = `[${when}] ${entry.method} ${entry.path}`;

    const meta = document.createElement("div");
    meta.className = "log-meta";
    meta.textContent = `Status: ${entry.status}`;

    const req = document.createElement("pre");
    req.textContent = `Request:\n${pretty(entry.requestBody)}`;

    const res = document.createElement("pre");
    res.textContent = `Response:\n${pretty(entry.responseBody)}`;

    block.appendChild(head);
    block.appendChild(meta);
    block.appendChild(req);
    block.appendChild(res);

    refs.consoleOutput.prepend(block);
  }

  function exportLogs() {
    if (logs.length === 0) {
      logInfo("Console has no entries to export.");
      return;
    }

    const payload = {
      exportedAt: new Date().toISOString(),
      count: logs.length,
      entries: logs,
    };

    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `mfa-api-console-${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    logInfo("Console exported as JSON.");
  }

  function pretty(value) {
    if (value == null) {
      return "(empty)";
    }

    if (typeof value === "string") {
      return value;
    }

    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return String(value);
    }
  }

  function logInfo(message) {
    void message;
  }

  async function onCreateUser(event) {
    event.preventDefault();
    beginAction("Create User");

    const payload = {
      username: refs.createUsername.value.trim(),
      email: refs.createEmail.value.trim(),
      password: refs.createPassword.value,
    };

    const result = await apiCall("/api/users", "POST", payload, "none");
    if (result.success) {
      state.lastLoginUsername = payload.username;
      persistState();
      refs.loginUsername.value = payload.username;
      refs.loginPassword.value = payload.password;
      focusCard(refs.loginCard);
    }
  }

  async function onLogin(event) {
    event.preventDefault();
    beginAction("Login");

    const username = refs.loginUsername.value.trim();
    const payload = {
      username,
      password: refs.loginPassword.value,
    };

    const result = await apiCall("/api/auth/login", "POST", payload, "none");
    if (!result.success || !result.data) {
      return;
    }

    const loginData = result.data;
    state.lastLoginUsername = username;

    if (loginData.status === "RequiresMfa") {
      state.mfaToken = loginData.mfaToken || null;
      state.mfaTransactionId = loginData.mfaTransactionId || null;
      state.allowedMfaMethods = loginData.allowedMfaMethods || [];
      state.accessToken = null;
      state.refreshToken = null;
      state.availableMfaSetupOptions = [];
      state.selectedVerifyMethod = null;
      state.selectedSetupMethod = null;
      focusCard(refs.mfaMethodsCard);
    }

    if (loginData.status === "Authenticated") {
      state.accessToken = loginData.accessToken || null;
      state.refreshToken = loginData.refreshToken || null;
      state.availableMfaSetupOptions = loginData.availableMfaSetupOptions || [];
      state.mfaToken = null;
      state.mfaTransactionId = null;
      state.allowedMfaMethods = [];
      state.selectedVerifyMethod = null;
      state.selectedSetupMethod = null;

      if ((state.availableMfaSetupOptions || []).length > 0) {
        focusCard(refs.setupMfaCard);
      } else {
        focusCard(refs.mfaEnrollmentCard);
      }
    }

    persistState();
    render();
  }

  async function onAuthAction() {
    if (state.accessToken) {
      beginAction("Logout");

      try {
        await apiCall("/api/auth/logout", "POST", {}, "full");
      } finally {
        clearSession();
      }

      return;
    }

    if (state.mfaToken) {
      beginAction("Cancel Authentication");

      try {
        await apiCall("/api/auth/cancel-authentication", "POST", {}, "mfa");
      } finally {
        clearSession();
      }
    }
  }

  async function onStartMfaChallenge(event) {
    event.preventDefault();
    beginAction("Start MFA Challenge");

    const tx = refs.challengeTx.value.trim() || state.mfaTransactionId;
    if (!tx) {
      throw new Error("MFA transaction id is required.");
    }

    const payload = {
      mfaTransactionId: tx,
      method: refs.challengeMethod.value,
    };

    await apiCall("/api/mfa/challenges/start", "POST", payload, "mfa");
    focusCard(refs.mfaChallengeCard);
  }

  async function onVerifyMfaChallenge(event) {
    event.preventDefault();
    beginAction("Verify MFA Challenge");

    const tx = refs.verifyChallengeTx.value.trim() || state.mfaTransactionId;
    if (!tx) {
      throw new Error("MFA transaction id is required.");
    }

    const payload = {
      mfaTransactionId: tx,
      code: refs.verifyChallengeCode.value.trim(),
    };

    const result = await apiCall("/api/mfa/challenges/verify", "POST", payload, "mfa");
    if (!result.success || !result.data) {
      return;
    }

    hydrateAuthenticatedSession(result.data);
    state.selectedSetupMethod = null;
    state.selectedVerifyMethod = null;
    persistState();
    render();
    focusCard(refs.setupMfaCard.hidden ? refs.loginCard : refs.setupMfaCard);
  }

  async function onFido2LoginOptions() {
    beginAction("FIDO2 Login Options");

    const payload = {
      usernameOrEmail: state.lastLoginUsername || "",
    };

    const result = await apiCall("/api/fido2/login/options", "POST", payload, "mfa");
    if (!result.success || !result.data) {
      return;
    }

    state.pendingFido2LoginOptions = result.data.options;
    state.pendingFido2LoginTransactionId = result.data.transactionId;
    persistState();
    render();
    focusCard(refs.fido2LoginCard);
  }

  async function onFido2LoginComplete() {
    beginAction("Complete FIDO2 Login");

    if (!state.pendingFido2LoginOptions || !state.pendingFido2LoginTransactionId) {
      throw new Error("Create FIDO2 login options first.");
    }

    const publicKey = window.WebAuthnHelpers.normalizeRequestOptions(state.pendingFido2LoginOptions);
    const credential = await navigator.credentials.get({ publicKey });

    const assertionResponse = window.WebAuthnHelpers.serializeAssertionCredential(credential);

    const payload = {
      transactionId: state.pendingFido2LoginTransactionId,
      assertionResponse,
    };

    const result = await apiCall("/api/fido2/login/complete", "POST", payload, "mfa");
    if (!result.success || !result.data) {
      return;
    }

    hydrateAuthenticatedSession(result.data);
    state.pendingFido2LoginOptions = null;
    state.pendingFido2LoginTransactionId = null;
    state.selectedSetupMethod = null;
    state.selectedVerifyMethod = null;
    persistState();
    render();
    focusCard(refs.setupMfaCard.hidden ? refs.loginCard : refs.setupMfaCard);
  }

  async function onStartEnrollment(event) {
    event.preventDefault();
    beginAction("Start MFA Enrollment");

    const payload = {
      method: refs.enrollMethod.value,
      contactValue: refs.enrollContact.value.trim(),
    };

    state.lastEnrollmentMethod = payload.method;

    const result = await apiCall("/api/mfa/enrollment/start", "POST", payload, "full");
    if (!result.success || !result.data) {
      return;
    }

    const tx = result.data.enrollmentTransactionId;
    if (tx) {
      refs.enrollTx.value = tx;
    }

    persistState();
    render();
    focusCard(refs.mfaEnrollmentCard);
  }

  async function onVerifyEnrollment(event) {
    event.preventDefault();
    beginAction("Verify MFA Enrollment");

    const payload = {
      enrollmentTransactionId: refs.enrollTx.value.trim(),
      code: refs.enrollCode.value.trim(),
    };

    const result = await apiCall("/api/mfa/enrollment/verify", "POST", payload, "full");
    if (!result.success) {
      return;
    }

    if (state.lastEnrollmentMethod) {
      state.availableMfaSetupOptions = (state.availableMfaSetupOptions || []).filter(
        (x) => x.toLowerCase() !== state.lastEnrollmentMethod
      );

      if ((state.selectedSetupMethod || "").toLowerCase() === state.lastEnrollmentMethod.toLowerCase()) {
        state.selectedSetupMethod = null;
      }
    }

    persistState();
    render();
    focusCard(refs.setupMfaCard.hidden ? refs.fido2EnrollmentCard : refs.setupMfaCard);
  }

  async function onFido2EnrollOptions() {
    beginAction("FIDO2 Enrollment Options");

    const result = await apiCall("/api/fido2/enrollment/options", "POST", {}, "full");
    if (!result.success || !result.data) {
      return;
    }

    state.pendingFido2EnrollOptions = result.data.options;
    state.pendingFido2EnrollTransactionId = result.data.transactionId;
    persistState();
    render();
    focusCard(refs.fido2EnrollmentCard);
  }

  async function onFido2EnrollComplete() {
    beginAction("Complete FIDO2 Enrollment");

    if (!state.pendingFido2EnrollOptions || !state.pendingFido2EnrollTransactionId) {
      throw new Error("Create FIDO2 enrollment options first.");
    }

    const publicKey = window.WebAuthnHelpers.normalizeCreationOptions(state.pendingFido2EnrollOptions);
    const credential = await navigator.credentials.create({ publicKey });

    const attestationResponse = window.WebAuthnHelpers.serializeAttestationCredential(credential);

    // Defensive fallback in case an older cached serializer is loaded.
    if (!attestationResponse.Response) {
      attestationResponse.Response = {};
    }

    if (!Array.isArray(attestationResponse.Response.Transports) || attestationResponse.Response.Transports.length === 0) {
      attestationResponse.Response.Transports = ["internal"];
    }

    const payload = {
      transactionId: state.pendingFido2EnrollTransactionId,
      attestationResponse,
    };

    const result = await apiCall("/api/fido2/enrollment/complete", "POST", payload, "full");
    if (!result.success) {
      return;
    }

    state.pendingFido2EnrollOptions = null;
    state.pendingFido2EnrollTransactionId = null;
    state.availableMfaSetupOptions = (state.availableMfaSetupOptions || []).filter(
      (x) => x.toLowerCase() !== "fido2"
    );
    if ((state.selectedSetupMethod || "").toLowerCase() === "fido2") {
      state.selectedSetupMethod = null;
    }

    persistState();
    render();
    focusCard(refs.setupMfaCard.hidden ? refs.loginCard : refs.setupMfaCard);
  }

  function hydrateAuthenticatedSession(data) {
    state.accessToken = data.accessToken || null;
    state.refreshToken = data.refreshToken || null;
    state.mfaToken = null;
    state.mfaTransactionId = null;
    state.allowedMfaMethods = [];
    state.selectedVerifyMethod = null;
    state.selectedSetupMethod = null;

    if (Array.isArray(data.availableMfaSetupOptions)) {
      state.availableMfaSetupOptions = data.availableMfaSetupOptions;
    }

    persistState();
    render();
  }

  async function copyText(value, successMessage) {
    if (!value) {
      return;
    }

    await navigator.clipboard.writeText(value);
    void successMessage;
  }

  function beginAction(actionName) {
    void actionName;
    logs.length = 0;
    refs.consoleOutput.innerHTML = "";
  }

  function focusCard(element) {
    if (!element || element.hidden) {
      return;
    }

    element.scrollIntoView({ behavior: "smooth", block: "center" });
    element.classList.remove("focus-pulse");
    window.requestAnimationFrame(() => {
      element.classList.add("focus-pulse");
    });
    window.setTimeout(() => {
      element.classList.remove("focus-pulse");
    }, 1100);
  }

  function fillCreateUserSample() {
    const random = Math.floor(Math.random() * 90000) + 10000;
    const username = `demo${random}`;
    refs.createUsername.value = username;
    refs.createEmail.value = `${username}@example.com`;
    refs.createPassword.value = "DemoPass123!";
  }

  function fillLoginFromLastCreated() {
    if (state.lastLoginUsername) {
      refs.loginUsername.value = state.lastLoginUsername;
      refs.loginPassword.value = refs.loginPassword.value || "DemoPass123!";
      return;
    }

    refs.loginUsername.value = refs.createUsername.value || "";
    refs.loginPassword.value = refs.createPassword.value || refs.loginPassword.value;
  }

  function fillMfaChallengeFromSession() {
    if (state.mfaTransactionId) {
      refs.challengeTx.value = state.mfaTransactionId;
      refs.verifyChallengeTx.value = state.mfaTransactionId;
    }

    const method = (state.allowedMfaMethods || []).find((x) => {
      const value = (x || "").toLowerCase();
      return value === "sms" || value === "email";
    });
    if (method) {
      refs.challengeMethod.value = method.toLowerCase();
    }
  }

  function fillEnrollmentSample() {
    const method = refs.enrollMethod.value;
    if (method === "sms") {
      refs.enrollContact.value = "+15555550123";
    } else {
      refs.enrollContact.value = "demo.user@example.com";
    }
  }

  window.addEventListener("unhandledrejection", (event) => {
    appendLog({
      startedAt: new Date(),
      method: "ERROR",
      path: "client",
      requestBody: null,
      status: "UNHANDLED",
      responseBody: { message: event.reason?.message || String(event.reason) },
    });
  });

  boot();
})();
