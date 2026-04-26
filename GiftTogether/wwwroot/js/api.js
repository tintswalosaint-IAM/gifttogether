// ── API client ────────────────────────────────────────────────────────────────
const API = {
  base: '/api',

  _token() {
    return localStorage.getItem('gt_token');
  },

  _headers(json = true) {
    const h = {};
    if (json) h['Content-Type'] = 'application/json';
    const t = this._token();
    if (t) h['Authorization'] = `Bearer ${t}`;
    return h;
  },

  async _fetch(path, opts = {}) {
    const res = await fetch(this.base + path, {
      ...opts,
      headers: { ...this._headers(), ...opts.headers }
    });

    // Token rejected by the server — clear local state and send to login
    if (res.status === 401) {
      Auth.clear();
      window.location.href = `/login.html?reason=session`;
      // Return a never-resolving promise so the calling code doesn't continue
      return new Promise(() => {});
    }

    if (res.status === 204) return null;
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `Request failed (${res.status})`);
    return data;
  },

  // Auth
  register: (name, email, password, profileImageUrl, guestMessage) =>
    API._fetch('/auth/register', { method: 'POST', body: JSON.stringify({ name, email, password, profileImageUrl, guestMessage }) }),

  login: (email, password) =>
    API._fetch('/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),

  // Registries
  getMyRegistries: () => API._fetch('/registries'),

  createRegistry: (name, description) =>
    API._fetch('/registries', { method: 'POST', body: JSON.stringify({ name, description }) }),

  getRegistryBySlug: (slug) => API._fetch(`/registries/${slug}`),

  deleteRegistry: (id) => API._fetch(`/registries/${id}`, { method: 'DELETE' }),

  updateRegistry: (id, name, description, heroBackgroundColor, heroImageUrl) =>
    API._fetch(`/registries/${id}`, {
      method: 'PATCH',
      body: JSON.stringify({ name, description, heroBackgroundColor, heroImageUrl })
    }),

  updateGoal: (registryId, goalId, fields) =>
    API._fetch(`/registries/${registryId}/goals/${goalId}`, {
      method: 'PATCH',
      body: JSON.stringify(fields)
    }),

  uploadHeroImage: async (registryId, file) => {
    const form = new FormData();
    form.append('image', file);
    const res = await fetch(`/api/registries/${registryId}/upload-hero`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${localStorage.getItem('gt_token')}` },
      body: form
    });
    if (res.status === 401) {
      Auth.clear();
      window.location.href = '/login.html?reason=session';
      return new Promise(() => {});
    }
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `Upload failed (${res.status})`);
    return data;
  },

  // Goals
  addGoal: (registryId, name, description, targetAmount, imageUrl, productLink) =>
    API._fetch(`/registries/${registryId}/goals`, {
      method: 'POST',
      body: JSON.stringify({ name, description, targetAmount, imageUrl, productLink })
    }),

  deleteGoal: (registryId, goalId) =>
    API._fetch(`/registries/${registryId}/goals/${goalId}`, { method: 'DELETE' }),

  // Profile
  updateProfile: (profileImageUrl, guestMessage) =>
    API._fetch('/auth/profile', { method: 'PATCH', body: JSON.stringify({ profileImageUrl, guestMessage }) }),

  getProfile: () => API._fetch('/auth/profile'),

  /**
   * Upload a profile photo file. Returns { url: '/uploads/...' }.
   * Uses multipart/form-data — no Content-Type header set manually
   * (the browser sets it with the correct boundary automatically).
   */
  uploadPhoto: async (file) => {
    const form = new FormData();
    form.append('photo', file);
    const res = await fetch('/api/auth/upload-photo', {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${localStorage.getItem('gt_token')}` },
      body: form
    });
    if (res.status === 401) {
      Auth.clear();
      window.location.href = '/login.html?reason=session';
      return new Promise(() => {});
    }
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `Upload failed (${res.status})`);
    return data;
  },

  /**
   * Upload an image for a specific gift goal.
   * The server saves the file and updates goal.ImageUrl.
   * Returns { url: '/uploads/...' }.
   */
  uploadGoalImage: async (registryId, goalId, file) => {
    const form = new FormData();
    form.append('image', file);
    const res = await fetch(`/api/registries/${registryId}/goals/${goalId}/upload-image`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${localStorage.getItem('gt_token')}` },
      body: form
    });
    if (res.status === 401) {
      Auth.clear();
      window.location.href = '/login.html?reason=session';
      return new Promise(() => {});
    }
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.error || `Upload failed (${res.status})`);
    return data;
  },

  // Contributions (public — no auth required)
  contribute: (goalId, contributorName, message, amount) =>
    API._fetch(`/goals/${goalId}/contributions`, {
      method: 'POST',
      body: JSON.stringify({ contributorName, message, amount })
    }),
};

// ── Auth helpers ──────────────────────────────────────────────────────────────
const TOKEN_LIFETIME_DAYS = 90; // must match TokenService.cs

const Auth = {
  save(data) {
    localStorage.setItem('gt_token', data.token);
    localStorage.setItem('gt_user', JSON.stringify({
      id: data.userId,
      name: data.name,
      email: data.email
    }));
  },

  clear() {
    localStorage.removeItem('gt_token');
    localStorage.removeItem('gt_user');
  },

  user() {
    const u = localStorage.getItem('gt_user');
    return u ? JSON.parse(u) : null;
  },

  /**
   * Returns true only if a token exists AND has not expired client-side.
   * The server will also reject expired tokens, but checking here avoids
   * an unnecessary round-trip and gives instant redirects on page load.
   */
  isLoggedIn() {
    const token = localStorage.getItem('gt_token');
    if (!token) return false;

    try {
      // Token format: base64Payload.base64Sig
      const payloadB64 = token.split('.')[0];
      const payload = atob(payloadB64);           // "userId:unixSeconds"
      const issuedAt = parseInt(payload.split(':')[1], 10);
      const ageMs = Date.now() - issuedAt * 1000;
      if (ageMs > TOKEN_LIFETIME_DAYS * 24 * 60 * 60 * 1000) {
        // Expired client-side — clean up now
        Auth.clear();
        return false;
      }
      return true;
    } catch {
      // Malformed token
      Auth.clear();
      return false;
    }
  },

  /**
   * Call on every protected page to enforce the auth guard.
   * Redirects to login if not authenticated, returns the user object if ok.
   */
  requireAuth() {
    if (!Auth.isLoggedIn()) {
      window.location.href = '/login.html';
      return null;
    }
    return Auth.user();
  },

  /**
   * Call on auth pages (login/register) to skip them when already logged in.
   */
  redirectIfLoggedIn(destination = '/dashboard.html') {
    if (Auth.isLoggedIn()) {
      window.location.href = destination;
    }
  },

  logout() {
    Auth.clear();
    window.location.href = '/login.html';
  },
};

// ── UI helpers ────────────────────────────────────────────────────────────────
function showAlert(el, msg, type = 'error') {
  el.className = `alert alert-${type}`;
  el.textContent = msg;
  el.classList.remove('hidden');
}

function hideAlert(el) {
  el.classList.add('hidden');
}

function formatCurrency(amount) {
  return 'R\u00a0' + Number(amount).toLocaleString('en-ZA', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  });
}

function formatDate(iso) {
  return new Date(iso).toLocaleDateString('en-ZA', {
    day: 'numeric', month: 'short', year: 'numeric'
  });
}

function progressPct(raised, target) {
  if (!target || target <= 0) return 0;
  return Math.min(100, Math.round((raised / target) * 100));
}
