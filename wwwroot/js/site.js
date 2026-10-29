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

  document.querySelectorAll("form").forEach((form) => {
    form.addEventListener("submit", (event) => {
      const trigger = event.submitter;
      if (!(trigger instanceof HTMLElement)) {
        return;
      }

      const message = trigger.getAttribute("data-confirm");
      if (message && !window.confirm(message)) {
        event.preventDefault();
      }
    });
  });
})();
