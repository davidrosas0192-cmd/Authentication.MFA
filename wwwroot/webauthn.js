(function () {
  function base64UrlToUint8Array(base64Url) {
    const padded = base64Url.replace(/-/g, "+").replace(/_/g, "/") + "===".slice((base64Url.length + 3) % 4);
    const binary = atob(padded);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) {
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
  }

  function uint8ArrayToBase64Url(bytes) {
    const input = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes || []);
    let binary = "";
    for (let i = 0; i < input.length; i += 1) {
      binary += String.fromCharCode(input[i]);
    }
    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");
  }

  function normalizeCreationOptions(raw) {
    const options = structuredClone(raw);
    options.challenge = base64UrlToUint8Array(options.challenge);

    if (options.user && typeof options.user.id === "string") {
      options.user.id = base64UrlToUint8Array(options.user.id);
    }

    if (Array.isArray(options.excludeCredentials)) {
      options.excludeCredentials = options.excludeCredentials.map((credential) => {
        const clone = { ...credential };
        if (typeof clone.id === "string") {
          clone.id = base64UrlToUint8Array(clone.id);
        }
        return clone;
      });
    }

    return options;
  }

  function normalizeRequestOptions(raw) {
    const options = structuredClone(raw);
    options.challenge = base64UrlToUint8Array(options.challenge);

    if (Array.isArray(options.allowCredentials)) {
      options.allowCredentials = options.allowCredentials.map((credential) => {
        const clone = { ...credential };
        if (typeof clone.id === "string") {
          clone.id = base64UrlToUint8Array(clone.id);
        }
        return clone;
      });
    }

    return options;
  }

  function serializeAttestationCredential(credential) {
    const response = credential.response;
    const transports = typeof response.getTransports === "function"
      ? response.getTransports()
      : [];

    return {
      Id: credential.id,
      RawId: uint8ArrayToBase64Url(credential.rawId),
      Type: credential.type,
      Response: {
        ClientDataJson: uint8ArrayToBase64Url(response.clientDataJSON),
        AttestationObject: uint8ArrayToBase64Url(response.attestationObject),
        Transports: transports.length > 0 ? transports : ["internal"],
      },
      ClientExtensionResults: credential.getClientExtensionResults(),
    };
  }

  function serializeAssertionCredential(credential) {
    const response = credential.response;

    return {
      Id: credential.id,
      RawId: uint8ArrayToBase64Url(credential.rawId),
      Type: credential.type,
      Response: {
        ClientDataJson: uint8ArrayToBase64Url(response.clientDataJSON),
        AuthenticatorData: uint8ArrayToBase64Url(response.authenticatorData),
        Signature: uint8ArrayToBase64Url(response.signature),
        UserHandle: response.userHandle ? uint8ArrayToBase64Url(response.userHandle) : null,
      },
      ClientExtensionResults: credential.getClientExtensionResults(),
    };
  }

  window.WebAuthnHelpers = {
    normalizeCreationOptions,
    normalizeRequestOptions,
    serializeAttestationCredential,
    serializeAssertionCredential,
  };
})();
