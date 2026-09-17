const PAGE_SIZE = 50;
const REQUEST_TIMEOUT_MS = 3 * 60 * 1000; // A first-time (uncached) export of a large domain can take a while.
const SLOW_LOAD_HINT_MS = 5000;
const COLUMN_COUNT = 10;

const state = {
  skip: 0,
  total: 0,
  forceRefresh: false,
  users: [],
};

const el = {
  connectivity: document.getElementById("connectivity"),
  search: document.getElementById("search"),
  includeDisabled: document.getElementById("includeDisabled"),
  refresh: document.getElementById("refresh"),
  addUser: document.getElementById("addUser"),
  message: document.getElementById("message"),
  body: document.getElementById("usersBody"),
  prevPage: document.getElementById("prevPage"),
  nextPage: document.getElementById("nextPage"),
  pageInfo: document.getElementById("pageInfo"),
  asOf: document.getElementById("asOf"),
  userDialog: document.getElementById("userDialog"),
  userForm: document.getElementById("userForm"),
  userDialogTitle: document.getElementById("userDialogTitle"),
  userDialogCancel: document.getElementById("userDialogCancel"),
  userDialogError: document.getElementById("userDialogError"),
  createOnlyFields: document.getElementById("createOnlyFields"),
  passwordDialog: document.getElementById("passwordDialog"),
  passwordForm: document.getElementById("passwordForm"),
  passwordDialogUser: document.getElementById("passwordDialogUser"),
  passwordDialogCancel: document.getElementById("passwordDialogCancel"),
  passwordDialogError: document.getElementById("passwordDialogError"),
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

function showDialogError(target, text) {
  if (!text) {
    target.hidden = true;
    target.textContent = "";
    return;
  }
  target.hidden = false;
  target.textContent = text;
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
  el.body.innerHTML = `<tr class="empty-row"><td colspan="${COLUMN_COUNT}">Loading&hellip;</td></tr>`;

  const slowHintTimer = setTimeout(() => {
    el.body.innerHTML = `<tr class="empty-row"><td colspan="${COLUMN_COUNT}">Still loading&hellip; the first read of a large directory can take a minute or two. Later loads are served from cache and are fast.</td></tr>`;
  }, SLOW_LOAD_HINT_MS);

  const params = new URLSearchParams({
    skip: String(state.skip),
    take: String(PAGE_SIZE),
    includeDisabled: String(el.includeDisabled.checked),
  });
  const q = el.search.value.trim();
  if (q) params.set("q", q);
  if (state.forceRefresh) params.set("forceRefresh", "true");
  state.forceRefresh = false;

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  try {
    const res = await fetch(`/api/adusers?${params.toString()}`, { signal: controller.signal });
    if (!res.ok) {
      const errBody = await res.json().catch(() => ({}));
      throw new Error(errBody.error || `Request failed (${res.status})`);
    }
    const data = await res.json();
    state.total = data.total;
    state.users = data.users || [];
    renderRows(state.users);
    renderPager();
    renderAsOf(data.asOf);
  } catch (err) {
    el.body.innerHTML = "";
    if (err.name === "AbortError") {
      showMessage("The directory read timed out. The domain may be very large, or AD may be unreachable — check the server console log for progress.");
    } else {
      showMessage(err.message || "Failed to load users.");
    }
    renderPager();
  } finally {
    clearTimeout(slowHintTimer);
    clearTimeout(timeoutId);
  }
}

function renderAsOf(asOf) {
  if (!el.asOf) return;
  if (!asOf) {
    el.asOf.textContent = "";
    return;
  }
  el.asOf.textContent = `Data as of ${new Date(asOf).toLocaleTimeString()}`;
}

function renderRows(users) {
  if (!users || users.length === 0) {
    el.body.innerHTML = `<tr class="empty-row"><td colspan="${COLUMN_COUNT}">No users found.</td></tr>`;
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
      <td>
        <div class="row-actions">
          <button type="button" class="small secondary" data-action="edit" data-sam="${escapeHtml(u.samAccountName)}">Edit</button>
          <button type="button" class="small secondary" data-action="toggle" data-sam="${escapeHtml(u.samAccountName)}" data-disabled="${u.isDisabled}">${u.isDisabled ? "Enable" : "Disable"}</button>
          <button type="button" class="small secondary" data-action="reset-password" data-sam="${escapeHtml(u.samAccountName)}">Reset password</button>
        </div>
      </td>
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

// --- Add / Edit user dialog ---------------------------------------------

function openUserDialog(mode, user) {
  el.userForm.reset();
  showDialogError(el.userDialogError, "");
  const samInput = el.userForm.elements.samAccountName;

  if (mode === "create") {
    el.userDialogTitle.textContent = "Add User";
    el.userForm.dataset.mode = "create";
    el.userForm.dataset.sam = "";
    samInput.disabled = false;
    el.createOnlyFields.hidden = false;
  } else {
    el.userDialogTitle.textContent = `Edit ${user.displayName || user.samAccountName}`;
    el.userForm.dataset.mode = "edit";
    el.userForm.dataset.sam = user.samAccountName;
    samInput.value = user.samAccountName;
    samInput.disabled = true;
    el.createOnlyFields.hidden = true;

    el.userForm.elements.firstName.value = user.firstName || "";
    el.userForm.elements.surname.value = user.surname || "";
    el.userForm.elements.displayName.value = user.displayName || "";
    el.userForm.elements.email.value = user.email || "";
    el.userForm.elements.telephone.value = user.telephone || "";
    el.userForm.elements.mobile.value = user.mobile || "";
    el.userForm.elements.jobTitle.value = user.jobTitle || "";
    el.userForm.elements.department.value = user.department || "";
    el.userForm.elements.employeeId.value = user.employeeId || "";
  }

  el.userDialog.showModal();
}

el.addUser.addEventListener("click", () => openUserDialog("create"));
el.userDialogCancel.addEventListener("click", () => el.userDialog.close());

el.userForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  showDialogError(el.userDialogError, "");

  const form = new FormData(el.userForm);
  const mode = el.userForm.dataset.mode;

  try {
    if (mode === "create") {
      const payload = {
        samAccountName: form.get("samAccountName")?.trim(),
        firstName: form.get("firstName")?.trim(),
        surname: form.get("surname")?.trim(),
        displayName: form.get("displayName")?.trim() || null,
        email: form.get("email")?.trim() || null,
        telephone: form.get("telephone")?.trim() || null,
        mobile: form.get("mobile")?.trim() || null,
        jobTitle: form.get("jobTitle")?.trim() || null,
        department: form.get("department")?.trim() || null,
        employeeId: form.get("employeeId")?.trim() || null,
        initialPassword: form.get("initialPassword") || null,
        requirePasswordChangeAtNextLogon: form.get("requirePasswordChangeAtNextLogon") === "on",
        enabled: form.get("enabled") === "on",
      };
      await apiCall("POST", "/api/adusers", payload);
    } else {
      const sam = el.userForm.dataset.sam;
      const payload = {
        firstName: form.get("firstName")?.trim() || null,
        surname: form.get("surname")?.trim() || null,
        displayName: form.get("displayName")?.trim() || null,
        email: form.get("email")?.trim() ?? null,
        telephone: form.get("telephone")?.trim() ?? null,
        mobile: form.get("mobile")?.trim() ?? null,
        jobTitle: form.get("jobTitle")?.trim() ?? null,
        department: form.get("department")?.trim() ?? null,
        employeeId: form.get("employeeId")?.trim() ?? null,
      };
      await apiCall("PATCH", `/api/adusers/${encodeURIComponent(sam)}`, payload);
    }

    el.userDialog.close();
    await loadUsers();
  } catch (err) {
    showDialogError(el.userDialogError, err.message || "Save failed.");
  }
});

// --- Enable / disable ----------------------------------------------------

async function toggleAccount(samAccountName, currentlyDisabled) {
  const verb = currentlyDisabled ? "enable" : "disable";
  if (!confirm(`${verb === "enable" ? "Enable" : "Disable"} account "${samAccountName}"?`)) return;

  try {
    await apiCall("POST", `/api/adusers/${encodeURIComponent(samAccountName)}/${verb}`, null);
    await loadUsers();
  } catch (err) {
    showMessage(err.message || `Failed to ${verb} account.`);
  }
}

// --- Reset password dialog ------------------------------------------------

let passwordTargetSam = null;

function openPasswordDialog(samAccountName) {
  passwordTargetSam = samAccountName;
  el.passwordForm.reset();
  showDialogError(el.passwordDialogError, "");
  el.passwordDialogUser.textContent = samAccountName;
  el.passwordDialog.showModal();
}

el.passwordDialogCancel.addEventListener("click", () => el.passwordDialog.close());

el.passwordForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  showDialogError(el.passwordDialogError, "");

  const form = new FormData(el.passwordForm);
  const payload = {
    newPassword: form.get("newPassword"),
    requireChangeAtNextLogon: form.get("requireChangeAtNextLogon") === "on",
  };

  try {
    await apiCall("POST", `/api/adusers/${encodeURIComponent(passwordTargetSam)}/reset-password`, payload);
    el.passwordDialog.close();
  } catch (err) {
    showDialogError(el.passwordDialogError, err.message || "Password reset failed.");
  }
});

// --- Row action delegation ------------------------------------------------

el.body.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) return;

  const sam = button.dataset.sam;
  const action = button.dataset.action;

  if (action === "edit") {
    const user = state.users.find((u) => u.samAccountName === sam);
    if (user) openUserDialog("edit", user);
  } else if (action === "toggle") {
    toggleAccount(sam, button.dataset.disabled === "true");
  } else if (action === "reset-password") {
    openPasswordDialog(sam);
  }
});

// --- Shared fetch helper ---------------------------------------------------

async function apiCall(method, url, body) {
  const res = await fetch(url, {
    method,
    headers: body === null ? undefined : { "Content-Type": "application/json" },
    body: body === null ? undefined : JSON.stringify(body),
  });

  if (res.status === 204) return null;

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(data.error || `Request failed (${res.status})`);
  }
  return data;
}

// --- Toolbar / paging wiring ------------------------------------------------

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
  state.forceRefresh = true;
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
