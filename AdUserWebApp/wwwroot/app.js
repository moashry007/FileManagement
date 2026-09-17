const PAGE_SIZE = 50;

const state = {
  skip: 0,
  total: 0,
};

const el = {
  connectivity: document.getElementById("connectivity"),
  search: document.getElementById("search"),
  includeDisabled: document.getElementById("includeDisabled"),
  refresh: document.getElementById("refresh"),
  message: document.getElementById("message"),
  body: document.getElementById("usersBody"),
  prevPage: document.getElementById("prevPage"),
  nextPage: document.getElementById("nextPage"),
  pageInfo: document.getElementById("pageInfo"),
};

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  }[c]));
}

function showMessage(text) {
  if (!text) {
    el.message.hidden = true;
    el.message.textContent = "";
    return;
  }
  el.message.hidden = false;
  el.message.textContent = text;
}

async function checkConnectivity() {
  el.connectivity.className = "status status-unknown";
  el.connectivity.textContent = "Checking connectivity…";
  try {
    const res = await fetch("/api/adusers/probe");
    const data = await res.json();
    if (res.ok && data.connected) {
      el.connectivity.className = "status status-ok";
      el.connectivity.textContent = `Connected to ${data.connectedServer} (${data.elapsedMilliseconds} ms)`;
    } else {
      el.connectivity.className = "status status-error";
      el.connectivity.textContent = `Not connected: ${data.error ?? "unknown error"}`;
    }
  } catch (err) {
    el.connectivity.className = "status status-error";
    el.connectivity.textContent = "Not connected: request failed";
  }
}

async function loadUsers() {
  showMessage("");
  el.body.innerHTML = `<tr class="empty-row"><td colspan="9">Loading&hellip;</td></tr>`;

  const params = new URLSearchParams({
    skip: String(state.skip),
    take: String(PAGE_SIZE),
    includeDisabled: String(el.includeDisabled.checked),
  });
  const q = el.search.value.trim();
  if (q) params.set("q", q);

  try {
    const res = await fetch(`/api/adusers?${params.toString()}`);
    if (!res.ok) {
      const errBody = await res.json().catch(() => ({}));
      throw new Error(errBody.error || `Request failed (${res.status})`);
    }
    const data = await res.json();
    state.total = data.total;
    renderRows(data.users);
    renderPager();
  } catch (err) {
    el.body.innerHTML = "";
    showMessage(err.message || "Failed to load users.");
    renderPager();
  }
}

function renderRows(users) {
  if (!users || users.length === 0) {
    el.body.innerHTML = `<tr class="empty-row"><td colspan="9">No users found.</td></tr>`;
    return;
  }

  el.body.innerHTML = users.map((u) => `
    <tr>
      <td>${escapeHtml(u.samAccountName)}</td>
      <td>${escapeHtml(u.displayName)}</td>
      <td>${escapeHtml(u.email)}</td>
      <td>${escapeHtml(u.department)}</td>
      <td>${escapeHtml(u.jobTitle)}</td>
      <td>${escapeHtml(u.telephone)}</td>
      <td>${escapeHtml(u.mobile)}</td>
      <td>${escapeHtml(u.employeeId)}</td>
      <td><span class="badge ${u.isDisabled ? "badge-disabled" : "badge-active"}">${u.isDisabled ? "Disabled" : "Active"}</span></td>
    </tr>
  `).join("");
}

function renderPager() {
  const from = state.total === 0 ? 0 : state.skip + 1;
  const to = Math.min(state.skip + PAGE_SIZE, state.total);
  el.pageInfo.textContent = `${from}–${to} of ${state.total}`;
  el.prevPage.disabled = state.skip === 0;
  el.nextPage.disabled = state.skip + PAGE_SIZE >= state.total;
}

let searchTimer;
el.search.addEventListener("input", () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    state.skip = 0;
    loadUsers();
  }, 350);
});

el.includeDisabled.addEventListener("change", () => {
  state.skip = 0;
  loadUsers();
});

el.refresh.addEventListener("click", () => {
  state.skip = 0;
  checkConnectivity();
  loadUsers();
});

el.prevPage.addEventListener("click", () => {
  state.skip = Math.max(0, state.skip - PAGE_SIZE);
  loadUsers();
});

el.nextPage.addEventListener("click", () => {
  state.skip += PAGE_SIZE;
  loadUsers();
});

checkConnectivity();
loadUsers();
