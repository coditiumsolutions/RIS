(() => {
  const root = document.getElementById("reception-visitors");
  if (!root) {
    return;
  }

  const cfg = {
    searchUrl: root.dataset.searchUrl,
    checkInUrl: root.dataset.checkinUrl,
    checkOutUrl: root.dataset.checkoutUrl,
    detailsUrl: root.dataset.detailsUrl,
    pageSize: Number(root.dataset.pageSize || 25)
  };

  const form = root.querySelector("[data-rv-filter-form]");
  const searchInput = root.querySelector("[data-rv-search]");
  const dateInput = root.querySelector("[data-rv-date]");
  const statusInput = root.querySelector("[data-rv-status-input]");
  const clearSearchBtn = root.querySelector("[data-rv-clear-search]");
  const clearAllBtn = root.querySelector("[data-rv-clear-all]");
  const resultsEl = root.querySelector("[data-rv-results]");
  const countEl = root.querySelector("[data-rv-count]");
  const statusMsg = root.querySelector("[data-rv-status-msg]");
  const loadMoreWrap = root.querySelector("[data-rv-load-more-wrap]");
  const loadMoreBtn = root.querySelector("[data-rv-load-more]");
  const antiforgeryInput = document.querySelector("#rv-antiforgery input[name='__RequestVerificationToken']");
  const noscriptSubmit = root.querySelector("[data-rv-noscript-submit]");

  if (statusInput) {
    statusInput.name = "status";
  }

  if (noscriptSubmit) {
    noscriptSubmit.hidden = true;
  }

  const state = {
    q: (root.dataset.initialQ || "").trim(),
    date: root.dataset.initialDate || "",
    status: root.dataset.initialStatus || "all",
    offset: 0,
    hasMore: root.dataset.initialHasMore === "1",
    total: Number(root.dataset.initialTotal || 0),
    loading: false,
    timer: null,
    abort: null
  };

  const escapeHtml = (value) => {
    const div = document.createElement("div");
    div.textContent = value == null ? "" : String(value);
    return div.innerHTML;
  };

  const antiforgeryToken = () => (antiforgeryInput ? antiforgeryInput.value : "");

  const statusClass = (status) => {
    switch (status) {
      case "CheckedIn":
        return "rd-status-in";
      case "CheckedOut":
        return "rd-status-out";
      case "Approved":
        return "rd-status-approved";
      case "Pending":
        return "rd-status-pending";
      default:
        return "rd-status-other";
    }
  };

  const formatArrival = (iso) => {
    if (!iso) {
      return "";
    }
    const [y, m, d] = iso.split("-").map(Number);
    const dt = new Date(Date.UTC(y, m - 1, d));
    return dt.toLocaleDateString("en-GB", {
      day: "numeric",
      month: "short",
      year: "numeric",
      timeZone: "UTC"
    });
  };

  const setStatusMsg = (message, kind) => {
    if (!statusMsg) {
      return;
    }
    if (!message) {
      statusMsg.hidden = true;
      statusMsg.textContent = "";
      statusMsg.className = "rv-list-status";
      return;
    }
    statusMsg.hidden = false;
    statusMsg.textContent = message;
    statusMsg.className = `rv-list-status${kind ? ` is-${kind}` : ""}`;
  };

  const syncClearSearch = () => {
    if (!clearSearchBtn || !searchInput) {
      return;
    }
    clearSearchBtn.hidden = !searchInput.value.trim();
  };

  const syncStatusChips = () => {
    root.querySelectorAll("[data-rv-status]").forEach((btn) => {
      const active = btn.getAttribute("data-rv-status") === state.status;
      btn.classList.toggle("is-active", active);
      btn.setAttribute("aria-pressed", active ? "true" : "false");
    });
    if (statusInput) {
      statusInput.value = state.status;
    }
  };

  const syncUrl = () => {
    const params = new URLSearchParams();
    if (state.q) {
      params.set("q", state.q);
    }
    if (state.date) {
      params.set("date", state.date);
    }
    if (state.status && state.status !== "all") {
      params.set("status", state.status);
    }
    const qs = params.toString();
    const next = qs ? `${window.location.pathname}?${qs}` : window.location.pathname;
    window.history.replaceState(null, "", next);
  };

  const actionForms = (visitor) => {
    const token = escapeHtml(antiforgeryToken());
    const name = escapeHtml(visitor.visitorName);
    const guests = visitor.guests || 1;
    const guestLabel = `${guests} guest${guests === 1 ? "" : "s"}`;
    const q = escapeHtml(state.q);
    const date = escapeHtml(state.date);
    const status = escapeHtml(state.status);
    const hidden = `
      <input type="hidden" name="__RequestVerificationToken" value="${token}" />
      <input type="hidden" name="q" value="${q}" />
      <input type="hidden" name="date" value="${date}" />
      <input type="hidden" name="status" value="${status}" />`;

    let primary = "";
    if (visitor.canCheckIn) {
      primary = `
        <form method="post" action="${escapeHtml(cfg.checkInUrl)}/${visitor.id}" enctype="multipart/form-data" class="rd-action-form">
          ${hidden}
          <input type="file" name="photo" accept="image/jpeg,image/png" hidden />
          <button type="submit" class="rd-action-btn rd-action-in"
            data-confirm-title="Check in visitor?"
            data-confirm-text="${name} · ${guestLabel}"
            data-confirm-ok="Check In" data-confirm-cancel="Cancel"
            data-confirm-photo="CheckIn">Check In <i class="bi bi-camera" aria-hidden="true"></i></button>
        </form>`;
    } else if (visitor.canCheckOut) {
      primary = `
        <form method="post" action="${escapeHtml(cfg.checkOutUrl)}/${visitor.id}" enctype="multipart/form-data" class="rd-action-form">
          ${hidden}
          <input type="file" name="photo" accept="image/jpeg,image/png" hidden />
          <button type="submit" class="rd-action-btn rd-action-out"
            data-confirm-title="Check out visitor?"
            data-confirm-text="${name} · ${guestLabel}"
            data-confirm-ok="Check Out" data-confirm-cancel="Cancel"
            data-confirm-photo="CheckOut">Check Out <i class="bi bi-camera" aria-hidden="true"></i></button>
        </form>`;
    }

    return `${primary}<a class="rd-action-btn rd-action-link" href="${escapeHtml(cfg.detailsUrl)}/${visitor.id}">Details</a>`;
  };

  const tableActions = (visitor) => {
    const token = escapeHtml(antiforgeryToken());
    const name = escapeHtml(visitor.visitorName);
    const guests = visitor.guests || 1;
    const guestLabel = `${guests} guest${guests === 1 ? "" : "s"}`;
    const q = escapeHtml(state.q);
    const date = escapeHtml(state.date);
    const status = escapeHtml(state.status);
    const hidden = `
      <input type="hidden" name="__RequestVerificationToken" value="${token}" />
      <input type="hidden" name="q" value="${q}" />
      <input type="hidden" name="date" value="${date}" />
      <input type="hidden" name="status" value="${status}" />`;

    let primary = "";
    if (visitor.canCheckIn) {
      primary = `
        <form method="post" action="${escapeHtml(cfg.checkInUrl)}/${visitor.id}" enctype="multipart/form-data" class="d-inline">
          ${hidden}
          <input type="file" name="photo" accept="image/jpeg,image/png" hidden />
          <button type="submit" class="btn btn-sm btn-success"
            data-confirm-title="Check in visitor?"
            data-confirm-text="${name} · ${guestLabel}"
            data-confirm-ok="Check In" data-confirm-cancel="Cancel"
            data-confirm-photo="CheckIn">Check In <i class="bi bi-camera" aria-hidden="true"></i></button>
        </form>`;
    } else if (visitor.canCheckOut) {
      primary = `
        <form method="post" action="${escapeHtml(cfg.checkOutUrl)}/${visitor.id}" enctype="multipart/form-data" class="d-inline">
          ${hidden}
          <input type="file" name="photo" accept="image/jpeg,image/png" hidden />
          <button type="submit" class="btn btn-sm btn-secondary"
            data-confirm-title="Check out visitor?"
            data-confirm-text="${name} · ${guestLabel}"
            data-confirm-ok="Check Out" data-confirm-cancel="Cancel"
            data-confirm-photo="CheckOut">Check Out <i class="bi bi-camera" aria-hidden="true"></i></button>
        </form>`;
    }

    return `${primary}
      <a href="${escapeHtml(cfg.detailsUrl)}/${visitor.id}" class="btn btn-sm btn-outline-secondary">Details</a>`;
  };

  const cardHtml = (visitor) => {
    const unit = visitor.residentUnit ? ` · Unit ${escapeHtml(visitor.residentUnit)}` : "";
    return `
      <article class="rd-card rv-card" data-visitor-id="${visitor.id}">
        <div class="rd-card-main">
          <div class="rd-card-top">
            <h3 class="rd-card-name">
              <a href="${escapeHtml(cfg.detailsUrl)}/${visitor.id}">${escapeHtml(visitor.visitorName)}</a>
            </h3>
            <span class="rd-status ${statusClass(visitor.status)}">${escapeHtml(visitor.statusLabel || visitor.status)}</span>
          </div>
          <p class="rd-card-host">${escapeHtml(visitor.residentName || "")}${unit}</p>
          <p class="rd-card-meta">
            ${escapeHtml(visitor.phone || "")}
            · ${escapeHtml(formatArrival(visitor.arrivalDate))}
            · ${visitor.guests || 1} guest${(visitor.guests || 1) === 1 ? "" : "s"}
            · Pass #${visitor.id}
            ${visitor.isWalkIn ? '<span class="rd-walkin-tag">Walk-in</span>' : ""}
          </p>
        </div>
        <div class="rd-card-actions">${actionForms(visitor)}</div>
      </article>`;
  };

  const rowHtml = (visitor) => `
    <tr data-visitor-id="${visitor.id}">
      <td class="visitor-name-cell" title="${escapeHtml(visitor.visitorName)}">
        <a href="${escapeHtml(cfg.detailsUrl)}/${visitor.id}">${escapeHtml(visitor.visitorName)}</a>
        ${visitor.isWalkIn ? '<span class="badge text-bg-info ms-1">Walk-in</span>' : ""}
      </td>
      <td>${escapeHtml(visitor.phone || "")}</td>
      <td>
        ${escapeHtml(visitor.residentName || "")}
        ${visitor.residentUnit ? `<br /><small class="text-secondary">Unit ${escapeHtml(visitor.residentUnit)}</small>` : ""}
      </td>
      <td>${escapeHtml(formatArrival(visitor.arrivalDate))}</td>
      <td>${visitor.guests || 1}</td>
      <td><span class="rd-status ${statusClass(visitor.status)}">${escapeHtml(visitor.statusLabel || visitor.status)}</span></td>
      <td>${tableActions(visitor)}</td>
    </tr>`;

  const ensureContainers = () => {
    if (!resultsEl) {
      return { cards: null, tbody: null };
    }

    let cards = resultsEl.querySelector("[data-rv-cards]");
    let tableWrap = resultsEl.querySelector("[data-rv-table-wrap]");
    let tbody = resultsEl.querySelector("[data-rv-table-body]");
    const empty = resultsEl.querySelector("[data-rv-empty]");

    if (empty) {
      empty.remove();
    }

    if (!cards) {
      resultsEl.innerHTML = `
        <div class="rv-cards" data-rv-cards></div>
        <div class="table-responsive rv-table-wrap" data-rv-table-wrap>
          <table class="table align-middle mb-0 rv-table">
            <thead>
              <tr>
                <th scope="col">Visitor</th>
                <th scope="col">Phone</th>
                <th scope="col">Resident</th>
                <th scope="col">Arrival</th>
                <th scope="col">Guests</th>
                <th scope="col">Status</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody data-rv-table-body></tbody>
          </table>
        </div>`;
      cards = resultsEl.querySelector("[data-rv-cards]");
      tableWrap = resultsEl.querySelector("[data-rv-table-wrap]");
      tbody = resultsEl.querySelector("[data-rv-table-body]");
    }

    return { cards, tbody, tableWrap };
  };

  const renderEmpty = () => {
    if (!resultsEl) {
      return;
    }
    resultsEl.innerHTML = `
      <div class="rd-empty" data-rv-empty>
        <i class="bi bi-people" aria-hidden="true"></i>
        <p>No visitors found matching your criteria.</p>
      </div>`;
  };

  const renderVisitors = (visitors, append) => {
    const items = Array.isArray(visitors) ? visitors : [];
    if (!append && items.length === 0) {
      renderEmpty();
      return;
    }

    const { cards, tbody } = ensureContainers();
    if (!cards || !tbody) {
      return;
    }

    const cardsHtml = items.map(cardHtml).join("");
    const rowsHtml = items.map(rowHtml).join("");

    if (append) {
      cards.insertAdjacentHTML("beforeend", cardsHtml);
      tbody.insertAdjacentHTML("beforeend", rowsHtml);
    } else {
      cards.innerHTML = cardsHtml;
      tbody.innerHTML = rowsHtml;
    }
  };

  const updateMeta = () => {
    if (countEl) {
      countEl.textContent = `${state.total} visitor${state.total === 1 ? "" : "s"}`;
    }
    if (loadMoreWrap) {
      loadMoreWrap.hidden = !state.hasMore;
    }
  };

  const buildSearchUrl = (offset) => {
    const params = new URLSearchParams();
    if (state.q) {
      params.set("q", state.q);
    }
    if (state.date) {
      params.set("date", state.date);
    }
    if (state.status && state.status !== "all") {
      params.set("status", state.status);
    }
    params.set("offset", String(offset));
    params.set("limit", String(cfg.pageSize));
    return `${cfg.searchUrl}?${params.toString()}`;
  };

  const fetchPage = async ({ append } = { append: false }) => {
    if (state.loading) {
      return;
    }
    state.loading = true;
    setStatusMsg(append ? "Loading more…" : "Searching…", "loading");

    if (state.abort) {
      state.abort.abort();
    }
    state.abort = new AbortController();

    try {
      const offset = append ? state.offset : 0;
      const response = await fetch(buildSearchUrl(offset), {
        credentials: "same-origin",
        headers: { Accept: "application/json" },
        signal: state.abort.signal
      });
      if (!response.ok) {
        throw new Error(`Search failed (${response.status})`);
      }
      const data = await response.json();
      const visitors = data.visitors || [];
      state.total = data.total || 0;
      state.hasMore = !!data.hasMore;
      state.offset = offset + visitors.length;

      renderVisitors(visitors, append);
      updateMeta();
      setStatusMsg("");
    } catch (err) {
      if (err && err.name === "AbortError") {
        return;
      }
      setStatusMsg("Could not load visitors. Try again.", "error");
    } finally {
      state.loading = false;
    }
  };

  const applyFilters = ({ resetOffset = true } = {}) => {
    state.q = (searchInput ? searchInput.value : "").trim();
    state.date = dateInput ? dateInput.value : "";
    syncClearSearch();
    syncStatusChips();
    syncUrl();
    if (resetOffset) {
      state.offset = 0;
    }
    fetchPage({ append: false });
  };

  const scheduleSearch = () => {
    if (state.timer) {
      window.clearTimeout(state.timer);
    }
    state.timer = window.setTimeout(() => applyFilters(), 300);
  };

  if (form) {
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      applyFilters();
    });
  }

  if (searchInput) {
    searchInput.addEventListener("input", () => {
      syncClearSearch();
      scheduleSearch();
    });
  }

  if (dateInput) {
    dateInput.addEventListener("change", () => applyFilters());
  }

  if (clearSearchBtn) {
    clearSearchBtn.addEventListener("click", () => {
      if (searchInput) {
        searchInput.value = "";
        searchInput.focus();
      }
      applyFilters();
    });
  }

  if (clearAllBtn) {
    clearAllBtn.addEventListener("click", (event) => {
      event.preventDefault();
      if (searchInput) {
        searchInput.value = "";
      }
      if (dateInput) {
        dateInput.value = "";
      }
      state.status = "all";
      applyFilters();
    });
  }

  root.querySelectorAll("[data-rv-status]").forEach((btn) => {
    btn.addEventListener("click", () => {
      state.status = btn.getAttribute("data-rv-status") || "all";
      applyFilters();
    });
  });

  if (loadMoreBtn) {
    loadMoreBtn.addEventListener("click", () => {
      if (!state.hasMore || state.loading) {
        return;
      }
      fetchPage({ append: true });
    });
  }

  // Enhance initial SSR: count shown visitors for offset continuity on Load more.
  const initialCount = root.querySelectorAll("[data-rv-cards] [data-visitor-id]").length
    || root.querySelectorAll("[data-rv-table-body] [data-visitor-id]").length;
  state.offset = initialCount;
  syncClearSearch();
  syncStatusChips();
  updateMeta();
})();
