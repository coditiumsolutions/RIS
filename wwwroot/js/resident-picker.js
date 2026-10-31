(() => {
  const DEBOUNCE_MS = 250;
  const MIN_CHARS = 2;

  const pickers = document.querySelectorAll("[data-resident-picker]");
  if (pickers.length === 0) {
    return;
  }

  const debounce = (fn, wait) => {
    let timer = 0;
    return (...args) => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => fn(...args), wait);
    };
  };

  const escapeHtml = (value) => {
    const div = document.createElement("div");
    div.textContent = value == null ? "" : String(value);
    return div.innerHTML;
  };

  const detailLine = (item) => {
    const bits = [];
    if (item.customerId) bits.push("ID " + item.customerId);
    if (item.towerDisplay) bits.push(item.towerDisplay);
    if (item.unitNo) bits.push("Unit " + item.unitNo);
    if (item.block) bits.push("Block " + item.block);
    if (item.mobileMasked) bits.push(item.mobileMasked);
    return bits.join(" · ");
  };

  const initPicker = (root) => {
    const lookupUrl = root.getAttribute("data-lookup-url");
    const inputName = root.getAttribute("data-input-name") || "ResidentId";
    if (!lookupUrl) {
      return;
    }

    const search = root.querySelector("[data-rp-search]");
    const hidden = root.querySelector("[data-rp-hidden]");
    const list = root.querySelector("[data-rp-list]");
    const status = root.querySelector("[data-rp-status]");
    const card = root.querySelector("[data-rp-card]");
    const cardBody = root.querySelector("[data-rp-card-body]");
    const changeBtn = root.querySelector("[data-rp-change]");
    const searchWrap = root.querySelector("[data-rp-search-wrap]");

    if (!search || !hidden || !list || !status || !card || !cardBody || !changeBtn || !searchWrap) {
      return;
    }

    let items = [];
    let activeIndex = -1;
    let abortController = null;
    let open = false;

    const setStatus = (text, isError) => {
      status.textContent = text || "";
      status.hidden = !text;
      status.classList.toggle("is-error", Boolean(isError));
    };

    const closeList = () => {
      open = false;
      list.hidden = true;
      list.innerHTML = "";
      items = [];
      activeIndex = -1;
      search.setAttribute("aria-expanded", "false");
      search.removeAttribute("aria-activedescendant");
    };

    const showSelected = (item) => {
      hidden.value = String(item.id);
      hidden.dispatchEvent(new Event("change", { bubbles: true }));

      const lines = [
        `<div class="resident-picker-card-name">${escapeHtml(item.name)}</div>`,
        `<div class="resident-picker-card-meta"><span>Customer ID</span><strong>${escapeHtml(item.customerId || "—")}</strong></div>`
      ];

      if (item.towerDisplay) {
        lines.push(`<div class="resident-picker-card-meta"><span>Tower</span><strong>${escapeHtml(item.towerDisplay)}</strong></div>`);
      }
      if (item.unitNo) {
        lines.push(`<div class="resident-picker-card-meta"><span>Unit / plot</span><strong>${escapeHtml(item.unitNo)}</strong></div>`);
      }
      if (item.block) {
        lines.push(`<div class="resident-picker-card-meta"><span>Block</span><strong>${escapeHtml(item.block)}</strong></div>`);
      }
      if (item.floor) {
        lines.push(`<div class="resident-picker-card-meta"><span>Floor</span><strong>${escapeHtml(item.floor)}</strong></div>`);
      }
      if (item.street) {
        lines.push(`<div class="resident-picker-card-meta"><span>Street</span><strong>${escapeHtml(item.street)}</strong></div>`);
      }
      if (item.mobileMasked) {
        lines.push(`<div class="resident-picker-card-meta"><span>Mobile</span><strong>${escapeHtml(item.mobileMasked)}</strong></div>`);
      }

      cardBody.innerHTML = lines.join("");
      card.hidden = false;
      searchWrap.hidden = true;
      closeList();
      setStatus("");
      search.value = "";
    };

    const clearSelection = () => {
      hidden.value = "";
      hidden.dispatchEvent(new Event("change", { bubbles: true }));
      card.hidden = true;
      cardBody.innerHTML = "";
      searchWrap.hidden = false;
      search.focus();
    };

    const highlight = () => {
      const options = list.querySelectorAll("[data-rp-option]");
      options.forEach((el, index) => {
        const isActive = index === activeIndex;
        el.classList.toggle("is-active", isActive);
        if (isActive) {
          search.setAttribute("aria-activedescendant", el.id);
          el.scrollIntoView({ block: "nearest" });
        }
      });
    };

    const renderList = () => {
      if (!open) {
        return;
      }

      if (items.length === 0) {
        list.innerHTML = `<div class="resident-picker-empty" role="option" aria-disabled="true">No residents found</div>`;
        list.hidden = false;
        search.setAttribute("aria-expanded", "true");
        return;
      }

      list.innerHTML = items
        .map((item, index) => {
          const id = `${inputName}-opt-${item.id}`;
          return `<button type="button" class="resident-picker-option" role="option" id="${id}" data-rp-option data-index="${index}" aria-selected="${index === activeIndex ? "true" : "false"}">
            <span class="resident-picker-option-name">${escapeHtml(item.name)}</span>
            <span class="resident-picker-option-meta">${escapeHtml(detailLine(item))}</span>
          </button>`;
        })
        .join("");
      list.hidden = false;
      search.setAttribute("aria-expanded", "true");
      highlight();
    };

    const selectIndex = (index) => {
      if (index < 0 || index >= items.length) {
        return;
      }
      showSelected(items[index]);
    };

    const fetchResults = async (query) => {
      if (abortController) {
        abortController.abort();
      }

      const trimmed = query.trim();
      if (trimmed.length < MIN_CHARS) {
        closeList();
        setStatus(trimmed.length === 0 ? "" : `Type at least ${MIN_CHARS} characters`);
        return;
      }

      abortController = new AbortController();
      setStatus("Searching…");
      open = true;

      try {
        const url = new URL(lookupUrl, window.location.origin);
        url.searchParams.set("q", trimmed);
        const response = await fetch(url.toString(), {
          method: "GET",
          credentials: "same-origin",
          headers: { Accept: "application/json" },
          signal: abortController.signal
        });

        if (!response.ok) {
          throw new Error("Lookup failed");
        }

        const payload = await response.json();
        items = Array.isArray(payload) ? payload : (payload.items || []);
        activeIndex = items.length > 0 ? 0 : -1;
        setStatus(items.length === 0 ? "No residents found" : `${items.length} match${items.length === 1 ? "" : "es"}`);
        renderList();
      } catch (err) {
        if (err && err.name === "AbortError") {
          return;
        }
        items = [];
        activeIndex = -1;
        setStatus("Could not search residents", true);
        renderList();
      }
    };

    const runSearch = debounce(() => fetchResults(search.value), DEBOUNCE_MS);

    search.addEventListener("input", () => {
      runSearch();
    });

    search.addEventListener("keydown", (event) => {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        if (!open) {
          fetchResults(search.value);
          return;
        }
        if (items.length === 0) {
          return;
        }
        activeIndex = (activeIndex + 1) % items.length;
        highlight();
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        if (!open || items.length === 0) {
          return;
        }
        activeIndex = (activeIndex - 1 + items.length) % items.length;
        highlight();
      } else if (event.key === "Enter") {
        if (open && activeIndex >= 0) {
          event.preventDefault();
          selectIndex(activeIndex);
        }
      } else if (event.key === "Escape") {
        event.preventDefault();
        closeList();
        setStatus("");
      }
    });

    list.addEventListener("click", (event) => {
      const option = event.target.closest("[data-rp-option]");
      if (!option) {
        return;
      }
      const index = Number(option.getAttribute("data-index"));
      selectIndex(index);
    });

    changeBtn.addEventListener("click", (event) => {
      event.preventDefault();
      clearSelection();
    });

    document.addEventListener("click", (event) => {
      if (!root.contains(event.target)) {
        closeList();
      }
    });

    // Prefill from server-rendered selection (validation postback).
    if (hidden.value) {
      searchWrap.hidden = true;
      card.hidden = false;
    }
  };

  pickers.forEach(initPicker);
})();
