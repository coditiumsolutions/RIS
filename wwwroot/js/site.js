(() => {
  const body = document.body;
  if (!body) {
    return;
  }

  const sidebarToggle = document.querySelector("[data-sidebar-toggle]");
  const sidebarBackdrop = document.querySelector("[data-sidebar-backdrop]");
  const storageKey = "rp.sidebar.collapsed";

  const isDesktop = () => window.matchMedia("(min-width: 992px)").matches;

  const applyStoredCollapse = () => {
    if (!isDesktop()) {
      body.classList.remove("sidebar-collapsed");
      return;
    }

    if (localStorage.getItem(storageKey) === "1") {
      body.classList.add("sidebar-collapsed");
    } else {
      body.classList.remove("sidebar-collapsed");
    }
  };

  const closeMobileSidebar = () => {
    body.classList.remove("sidebar-open");
  };

  if (sidebarToggle) {
    applyStoredCollapse();

    sidebarToggle.addEventListener("click", () => {
      if (isDesktop()) {
        body.classList.toggle("sidebar-collapsed");
        localStorage.setItem(storageKey, body.classList.contains("sidebar-collapsed") ? "1" : "0");
        return;
      }

      body.classList.toggle("sidebar-open");
    });
  }

  if (sidebarBackdrop) {
    sidebarBackdrop.addEventListener("click", closeMobileSidebar);
  }

  window.addEventListener("resize", () => {
    if (isDesktop()) {
      closeMobileSidebar();
      applyStoredCollapse();
    } else {
      body.classList.remove("sidebar-collapsed");
    }
  });

  document.querySelectorAll("[data-password-toggle]").forEach((button) => {
    button.addEventListener("click", () => {
      const targetId = button.getAttribute("data-password-toggle");
      const input = targetId ? document.getElementById(targetId) : null;
      if (!input) {
        return;
      }

      const show = input.getAttribute("type") === "password";
      input.setAttribute("type", show ? "text" : "password");
      button.setAttribute("aria-label", show ? "Hide password" : "Show password");
      button.setAttribute("aria-pressed", show ? "true" : "false");

      const icon = button.querySelector("i");
      if (icon) {
        icon.classList.toggle("bi-eye", !show);
        icon.classList.toggle("bi-eye-slash", show);
      }
    });
  });

  document.querySelectorAll("[data-history-back]").forEach((button) => {
    button.addEventListener("click", () => {
      window.history.back();
    });
  });

  const readConfirmAttrs = (form, trigger) => {
    const title =
      trigger.getAttribute("data-confirm-title") ||
      form.getAttribute("data-confirm-title") ||
      "";
    const text =
      trigger.getAttribute("data-confirm-text") ||
      trigger.getAttribute("data-confirm") ||
      form.getAttribute("data-confirm-text") ||
      "";
    const confirmText =
      trigger.getAttribute("data-confirm-ok") ||
      form.getAttribute("data-confirm-ok") ||
      "Confirm";
    const cancelText =
      trigger.getAttribute("data-confirm-cancel") ||
      form.getAttribute("data-confirm-cancel") ||
      "Cancel";
    const icon =
      trigger.getAttribute("data-confirm-icon") ||
      form.getAttribute("data-confirm-icon") ||
      "question";

    return { title, text, confirmText, cancelText, icon };
  };

  const askConfirm = async ({ title, text, confirmText, cancelText, icon }) => {
    if (typeof Swal !== "undefined" && typeof Swal.fire === "function") {
      const result = await Swal.fire({
        title: title || "Are you sure?",
        text: text || undefined,
        icon,
        showCancelButton: true,
        focusCancel: true,
        reverseButtons: true,
        confirmButtonText: confirmText,
        cancelButtonText: cancelText
      });
      return result.isConfirmed === true;
    }

    return window.confirm([title, text].filter(Boolean).join("\n"));
  };

  document.querySelectorAll("form").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      if (form.dataset.swalConfirmed === "1") {
        delete form.dataset.swalConfirmed;
        return;
      }

      const trigger = event.submitter instanceof HTMLElement
        ? event.submitter
        : form.querySelector("[type='submit']");

      if (!(trigger instanceof HTMLElement)) {
        return;
      }

      const attrs = readConfirmAttrs(form, trigger);
      if (!attrs.title && !attrs.text) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();

      const confirmed = await askConfirm(attrs);
      if (!confirmed) {
        return;
      }

      const photoKind =
        trigger.getAttribute("data-confirm-photo") ||
        form.getAttribute("data-confirm-photo") ||
        "";

      if (photoKind && window.VisitorCamera && typeof window.VisitorCamera.captureOptional === "function") {
        const label = photoKind.toLowerCase() === "checkout" ? "check-out" : "check-in";
        const photoResult = await window.VisitorCamera.captureOptional(label);
        if (photoResult && photoResult.cancelled) {
          return;
        }
        if (photoResult && photoResult.blob) {
          window.VisitorCamera.attachPhotoToForm(form, photoResult.blob);
        } else {
          window.VisitorCamera.attachPhotoToForm(form, null);
        }
      }

      form.dataset.swalConfirmed = "1";
      if (typeof form.requestSubmit === "function") {
        form.requestSubmit(trigger);
      } else {
        form.submit();
      }
    });
  });
})();

