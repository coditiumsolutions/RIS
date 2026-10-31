(() => {
  const root = document.getElementById("reception-desk");
  if (!root) {
    return;
  }

  const cfg = {
    today: root.dataset.today,
    searchUrl: root.dataset.searchUrl,
    dayUrl: root.dataset.dayUrl,
    calendarUrl: root.dataset.calendarUrl,
    summaryUrl: root.dataset.summaryUrl,
    checkInUrl: root.dataset.checkinUrl,
    checkOutUrl: root.dataset.checkoutUrl,
    detailsUrl: root.dataset.detailsUrl
  };

  const searchInput = document.getElementById("rd-search");
  const clearSearchBtn = root.querySelector("[data-rd-clear-search]");
  const listEl = root.querySelector("[data-rd-visitor-list]");
  const listTitle = root.querySelector("[data-rd-list-title]");
  const listMeta = root.querySelector("[data-rd-list-meta]");
  const listStatus = root.querySelector("[data-rd-list-status]");
  const calGrid = root.querySelector("[data-rd-cal-grid]");
  const calStrip = root.querySelector("[data-rd-cal-strip]");
  const calTitle = root.querySelector("[data-rd-cal-title]");
  const antiforgeryInput = document.querySelector("#rd-antiforgery input[name='__RequestVerificationToken']");

  const state = {
    selectedDate: cfg.today,
    filter: "all",
    searchQuery: "",
    searchMode: false,
    year: Number(cfg.today.slice(0, 4)),
    month: Number(cfg.today.slice(5, 7)),
    calendarDays: new Map(),
    refreshTimer: null,
    searchTimer: null,
    abort: null
  };

  const MONTH_NAMES = [
    "January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December"
  ];

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

  const escapeHtml = (value) => {
    const div = document.createElement("div");
    div.textContent = value == null ? "" : String(value);
    return div.innerHTML;
  };

  const formatDisplayDate = (iso) => {
    if (!iso) {
      return "";
    }
    const [y, m, d] = iso.split("-").map(Number);
    const dt = new Date(Date.UTC(y, m - 1, d));
    return dt.toLocaleDateString("en-GB", {
      weekday: "short",
      day: "numeric",
      month: "short",
      year: "numeric",
      timeZone: "UTC"
    });
  };

  const isPhoneLayout = () => window.matchMedia("(max-width: 767.98px)").matches;

  const antiforgeryToken = () => (antiforgeryInput ? antiforgeryInput.value : "");

  const setStatus = (message, kind) => {
    if (!listStatus) {
      return;
    }
    if (!message) {
      listStatus.hidden = true;
      listStatus.textContent = "";
      listStatus.className = "rd-list-status";
      return;
    }
    listStatus.hidden = false;
    listStatus.textContent = message;
    listStatus.className = `rd-list-status${kind ? ` is-${kind}` : ""}`;
  };

  const updateSummaryChips = (summary) => {
    if (!summary) {
      return;
    }
    const map = {
      expected: summary.expectedToday,
      checkedin: summary.checkedInNow,
      checkedout: summary.checkedOutToday,
      walkins: summary.walkInsToday,
      all: (summary.expectedToday || 0) + (summary.checkedInNow || 0)
    };

    Object.keys(map).forEach((key) => {
      const el = root.querySelector(`[data-rd-summary="${key}"]`);
      if (el) {
        el.textContent = String(map[key] ?? 0);
      }
    });

    const walkinsChip = root.querySelector("[data-rd-walkins-chip]");
    if (walkinsChip) {
      walkinsChip.hidden = !(summary.walkInsToday > 0);
    }
  };

  const setActiveFilter = (filter) => {
    state.filter = filter;
    root.querySelectorAll("[data-rd-filter]").forEach((btn) => {
      const active = btn.getAttribute("data-rd-filter") === filter;
      btn.classList.toggle("is-active", active);
      btn.setAttribute("aria-pressed", active ? "true" : "false");
    });
  };

  const actionFormHtml = (visitor) => {
    const token = escapeHtml(antiforgeryToken());
    const name = escapeHtml(visitor.visitorName);
    const guests = visitor.guests || 1;
    const guestLabel = `${guests} guest${guests === 1 ? "" : "s"}`;

    if (visitor.canCheckIn) {
      return `
        <form method="post" action="${escapeHtml(cfg.checkInUrl)}/${visitor.id}" enctype="multipart/form-data" class="rd-action-form">
          <input type="hidden" name="__RequestVerificationToken" value="${token}" />
          <input type="hidden" name="from" value="dashboard" />
          <input type="file" name="photo" accept="image/jpeg,image/png" hidden />
          <button type="submit"
                  class="rd-action-btn rd-action-in"
                  data-confirm-title="Check in visitor?"
                  data-confirm-text="${name} · ${guestLabel}"
                  data-confirm-ok="Check In"
                  data-confirm-cancel="Cancel"
                  data-confirm-photo="CheckIn">Check In</button>
        </form>`;
    }

    if (visitor.canCheckOut) {
      return `
        <form method="post" action="${escapeHtml(cfg.checkOutUrl)}/${visitor.id}" enctype="multipart/form-data" class="rd-action-form">
          <input type="hidden" name="__RequestVerificationToken" value="${token}" />
          <input type="hidden" name="from" value="dashboard" />
          <input type="file" name="photo" accept="image/jpeg,image/png" hidden />
          <button type="submit"
                  class="rd-action-btn rd-action-out"
                  data-confirm-title="Check out visitor?"
                  data-confirm-text="${name} · ${guestLabel}"
                  data-confirm-ok="Check Out"
                  data-confirm-cancel="Cancel"
                  data-confirm-photo="CheckOut">Check Out</button>
        </form>`;
    }

    return "";
  };

  const renderVisitors = (visitors, options = {}) => {
    if (!listEl) {
      return;
    }

    const items = Array.isArray(visitors) ? visitors : [];
    if (listMeta) {
      listMeta.textContent = items.length
        ? `${items.length} visitor${items.length === 1 ? "" : "s"}`
        : "";
    }

    if (!items.length) {
      listEl.innerHTML = `
        <div class="rd-empty">
          <i class="bi bi-people" aria-hidden="true"></i>
          <p>${escapeHtml(options.emptyMessage || "No visitors for this selection.")}</p>
        </div>`;
      return;
    }

    listEl.innerHTML = items
      .map((v) => {
        const unit = v.residentUnit ? ` · Unit ${escapeHtml(v.residentUnit)}` : "";
        const metaBits = [
          v.phone ? escapeHtml(v.phone) : "",
          v.vehicle ? `Veh ${escapeHtml(v.vehicle)}` : "",
          v.cnic ? `CNIC ${escapeHtml(v.cnic)}` : "",
          `Pass #${v.id}`
        ].filter(Boolean);

        const times = [
          v.checkedInAt ? `In ${escapeHtml(v.checkedInAt)}` : "",
          v.checkedOutAt ? `Out ${escapeHtml(v.checkedOutAt)}` : ""
        ].filter(Boolean).join(" · ");

        return `
          <article class="rd-card" data-visitor-id="${v.id}">
            <div class="rd-card-main">
              <div class="rd-card-top">
                <h3 class="rd-card-name">${escapeHtml(v.visitorName)}</h3>
                <span class="rd-status ${statusClass(v.status)}">${escapeHtml(v.statusLabel || v.status)}</span>
              </div>
              <p class="rd-card-host">${escapeHtml(v.residentName || "")}${unit}</p>
              <p class="rd-card-meta">${metaBits.join(" · ")}</p>
              <p class="rd-card-purpose">${escapeHtml(v.purpose || "")}${v.isWalkIn ? ' <span class="rd-walkin-tag">Walk-in</span>' : ""}${times ? ` · ${times}` : ""}</p>
            </div>
            <div class="rd-card-actions">
              ${actionFormHtml(v)}
              <a class="rd-action-btn rd-action-link" href="${escapeHtml(cfg.detailsUrl)}/${v.id}">Details</a>
            </div>
          </article>`;
      })
      .join("");
  };

  const fetchJson = async (url) => {
    if (state.abort) {
      state.abort.abort();
    }
    state.abort = new AbortController();
    const response = await fetch(url, {
      credentials: "same-origin",
      headers: { Accept: "application/json" },
      signal: state.abort.signal
    });
    if (!response.ok) {
      throw new Error(`Request failed (${response.status})`);
    }
    return response.json();
  };

  const loadSummary = async () => {
    try {
      const summary = await fetchJson(cfg.summaryUrl);
      updateSummaryChips(summary);
      if (summary && summary.today) {
        cfg.today = summary.today;
      }
    } catch (err) {
      if (err && err.name === "AbortError") {
        return;
      }
    }
  };

  const loadDayList = async () => {
    if (state.searchMode && state.searchQuery) {
      return;
    }

    setStatus("Loading visitors…", "loading");
    if (listTitle) {
      const label = state.selectedDate === cfg.today ? "Today" : formatDisplayDate(state.selectedDate);
      listTitle.textContent = label;
    }

    try {
      const url = `${cfg.dayUrl}?date=${encodeURIComponent(state.selectedDate)}&filter=${encodeURIComponent(state.filter)}`;
      const data = await fetchJson(url);
      setStatus("");
      renderVisitors(data.visitors || [], {
        emptyMessage: "No visitors for this day."
      });
      highlightSelectedDay();
    } catch (err) {
      if (err && err.name === "AbortError") {
        return;
      }
      setStatus("Could not load visitors. Pull to retry or wait for refresh.", "error");
    }
  };

  const runSearch = async (query) => {
    state.searchQuery = query;
    state.searchMode = query.length > 0;

    if (clearSearchBtn) {
      clearSearchBtn.hidden = query.length === 0;
    }

    if (!state.searchMode) {
      if (listTitle) {
        listTitle.textContent = state.selectedDate === cfg.today ? "Today" : formatDisplayDate(state.selectedDate);
      }
      await loadDayList();
      return;
    }

    if (listTitle) {
      listTitle.textContent = "Search results";
    }
    setStatus("Searching…", "loading");

    try {
      const data = await fetchJson(`${cfg.searchUrl}?q=${encodeURIComponent(query)}`);
      setStatus("");
      renderVisitors(data.visitors || [], {
        emptyMessage: "No matches. Try name, phone, CNIC, unit, vehicle, or pass code."
      });
    } catch (err) {
      if (err && err.name === "AbortError") {
        return;
      }
      setStatus("Search failed. Try again.", "error");
    }
  };

  const dayCounts = (iso) => state.calendarDays.get(iso) || null;

  const highlightSelectedDay = () => {
    root.querySelectorAll("[data-rd-day]").forEach((el) => {
      const selected = el.getAttribute("data-rd-day") === state.selectedDate;
      el.classList.toggle("is-selected", selected);
      el.setAttribute("aria-selected", selected ? "true" : "false");
    });
  };

  const renderMonthGrid = () => {
    if (!calGrid || !calTitle) {
      return;
    }

    calTitle.textContent = `${MONTH_NAMES[state.month - 1]} ${state.year}`;
    const first = new Date(Date.UTC(state.year, state.month - 1, 1));
    // Monday-first: JS getUTCDay Sun=0 → shift
    let startOffset = (first.getUTCDay() + 6) % 7;
    const daysInMonth = new Date(Date.UTC(state.year, state.month, 0)).getUTCDate();
    const cells = [];

    for (let i = 0; i < startOffset; i += 1) {
      cells.push('<div class="rd-cal-cell is-empty" aria-hidden="true"></div>');
    }

    for (let day = 1; day <= daysInMonth; day += 1) {
      const iso = `${state.year}-${String(state.month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
      const counts = dayCounts(iso);
      const isToday = iso === cfg.today;
      const expected = counts ? counts.expected : 0;
      const total = counts ? counts.total : 0;
      const badge = expected > 0
        ? `<span class="rd-cal-badge" title="${expected} expected">${expected}</span>`
        : total > 0
          ? `<span class="rd-cal-dot" title="${total} visitors"></span>`
          : "";

      cells.push(`
        <button type="button"
                class="rd-cal-cell${isToday ? " is-today" : ""}${iso === state.selectedDate ? " is-selected" : ""}"
                data-rd-day="${iso}"
                aria-label="${formatDisplayDate(iso)}${expected ? `, ${expected} expected` : ""}"
                aria-selected="${iso === state.selectedDate ? "true" : "false"}">
          <span class="rd-cal-num">${day}</span>
          ${badge}
        </button>`);
    }

    calGrid.innerHTML = cells.join("");
    calGrid.hidden = isPhoneLayout();
    if (calStrip) {
      calStrip.hidden = !isPhoneLayout();
    }
  };

  const renderDayStrip = () => {
    if (!calStrip) {
      return;
    }

    const selected = new Date(`${state.selectedDate}T00:00:00Z`);
    const days = [];
    for (let offset = -3; offset <= 3; offset += 1) {
      const dt = new Date(selected);
      dt.setUTCDate(selected.getUTCDate() + offset);
      const iso = dt.toISOString().slice(0, 10);
      const counts = dayCounts(iso);
      const expected = counts ? counts.expected : 0;
      const weekday = dt.toLocaleDateString("en-GB", { weekday: "short", timeZone: "UTC" });
      days.push(`
        <button type="button"
                class="rd-strip-day${iso === cfg.today ? " is-today" : ""}${iso === state.selectedDate ? " is-selected" : ""}"
                data-rd-day="${iso}"
                role="option"
                aria-selected="${iso === state.selectedDate ? "true" : "false"}">
          <span class="rd-strip-weekday">${weekday}</span>
          <span class="rd-strip-num">${dt.getUTCDate()}</span>
          ${expected > 0 ? `<span class="rd-cal-badge">${expected}</span>` : "<span class=\"rd-strip-spacer\"></span>"}
        </button>`);
    }

    calStrip.innerHTML = days.join("");
    calStrip.hidden = !isPhoneLayout();
    if (calGrid) {
      calGrid.hidden = isPhoneLayout();
    }
    if (calTitle && isPhoneLayout()) {
      calTitle.textContent = formatDisplayDate(state.selectedDate);
    }
  };

  const loadCalendar = async () => {
    try {
      const data = await fetchJson(`${cfg.calendarUrl}?year=${state.year}&month=${state.month}`);
      state.calendarDays = new Map();
      (data.days || []).forEach((d) => {
        state.calendarDays.set(d.date, d);
      });
      renderMonthGrid();
      renderDayStrip();
    } catch (err) {
      if (err && err.name === "AbortError") {
        return;
      }
      renderMonthGrid();
      renderDayStrip();
    }
  };

  const selectDate = async (iso, options = {}) => {
    state.selectedDate = iso;
    const parts = iso.split("-").map(Number);
    if (!options.keepMonth) {
      state.year = parts[0];
      state.month = parts[1];
    }

    if (state.searchMode) {
      state.searchMode = false;
      state.searchQuery = "";
      if (searchInput) {
        searchInput.value = "";
      }
      if (clearSearchBtn) {
        clearSearchBtn.hidden = true;
      }
    }

    highlightSelectedDay();
    renderDayStrip();
    if (!isPhoneLayout()) {
      renderMonthGrid();
    }
    await loadDayList();
  };

  const shiftMonth = async (delta) => {
    let m = state.month + delta;
    let y = state.year;
    if (m < 1) {
      m = 12;
      y -= 1;
    } else if (m > 12) {
      m = 1;
      y += 1;
    }
    state.year = y;
    state.month = m;

    if (isPhoneLayout()) {
      const dt = new Date(`${state.selectedDate}T00:00:00Z`);
      dt.setUTCDate(dt.getUTCDate() + (delta > 0 ? 7 : -7));
      await selectDate(dt.toISOString().slice(0, 10));
      await loadCalendar();
      return;
    }

    await loadCalendar();
  };

  const scheduleRefresh = () => {
    if (state.refreshTimer) {
      window.clearInterval(state.refreshTimer);
    }
    state.refreshTimer = window.setInterval(async () => {
      if (document.hidden) {
        return;
      }
      await loadSummary();
      if (!state.searchMode) {
        await loadDayList();
      }
      await loadCalendar();
    }, 60000);
  };

  // Events
  root.querySelectorAll("[data-rd-filter]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const filter = btn.getAttribute("data-rd-filter") || "all";
      setActiveFilter(filter);
      if (state.selectedDate !== cfg.today && (filter === "checkedin" || filter === "walkins" || filter === "expected" || filter === "checkedout" || filter === "all")) {
        // Chips are "today" summary — jump to today when filtering from chips.
        state.selectedDate = cfg.today;
        state.year = Number(cfg.today.slice(0, 4));
        state.month = Number(cfg.today.slice(5, 7));
      }
      if (state.searchMode) {
        state.searchMode = false;
        state.searchQuery = "";
        if (searchInput) {
          searchInput.value = "";
        }
        if (clearSearchBtn) {
          clearSearchBtn.hidden = true;
        }
      }
      await loadCalendar();
      await loadDayList();
    });
  });

  if (searchInput) {
    searchInput.addEventListener("input", () => {
      const value = searchInput.value.trim();
      if (state.searchTimer) {
        window.clearTimeout(state.searchTimer);
      }
      state.searchTimer = window.setTimeout(() => {
        runSearch(value);
      }, 280);
    });
  }

  if (clearSearchBtn) {
    clearSearchBtn.addEventListener("click", () => {
      if (searchInput) {
        searchInput.value = "";
        searchInput.focus();
      }
      runSearch("");
    });
  }

  root.addEventListener("click", (event) => {
    const dayBtn = event.target.closest("[data-rd-day]");
    if (dayBtn) {
      const iso = dayBtn.getAttribute("data-rd-day");
      if (iso) {
        selectDate(iso);
      }
    }
  });

  const prevBtn = root.querySelector("[data-rd-cal-prev]");
  const nextBtn = root.querySelector("[data-rd-cal-next]");
  const todayBtn = root.querySelector("[data-rd-cal-today]");

  if (prevBtn) {
    prevBtn.addEventListener("click", () => shiftMonth(-1));
  }
  if (nextBtn) {
    nextBtn.addEventListener("click", () => shiftMonth(1));
  }
  if (todayBtn) {
    todayBtn.addEventListener("click", async () => {
      setActiveFilter("all");
      state.year = Number(cfg.today.slice(0, 4));
      state.month = Number(cfg.today.slice(5, 7));
      await selectDate(cfg.today);
      await loadCalendar();
    });
  }

  const passCodeBtn = root.querySelector("[data-rd-pass-code]");
  if (passCodeBtn) {
    passCodeBtn.addEventListener("click", async () => {
      let code = "";
      if (typeof Swal !== "undefined" && typeof Swal.fire === "function") {
        const result = await Swal.fire({
          title: "Enter pass code",
          input: "text",
          inputLabel: "Visitor pass / request code",
          inputPlaceholder: "e.g. 1042",
          showCancelButton: true,
          confirmButtonText: "Find",
          cancelButtonText: "Cancel",
          inputAttributes: {
            inputmode: "numeric",
            autocomplete: "off",
            autocapitalize: "off"
          }
        });
        if (!result.isConfirmed) {
          return;
        }
        code = String(result.value || "").trim();
      } else {
        code = window.prompt("Enter pass / request code") || "";
        code = code.trim();
      }

      if (!code) {
        return;
      }

      if (searchInput) {
        searchInput.value = code;
      }
      setActiveFilter("all");
      await runSearch(code);
      if (searchInput) {
        searchInput.focus();
      }
    });
  }

  window.addEventListener("resize", () => {
    renderMonthGrid();
    renderDayStrip();
  });

  // Boot
  (async () => {
    setActiveFilter("all");
    await loadSummary();
    await loadCalendar();
    await loadDayList();
    scheduleRefresh();
  })();
})();
