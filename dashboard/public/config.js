// Placeholder for local `vite dev` — the production container's
// docker-entrypoint.sh overwrites /usr/share/nginx/html/config.js from env
// vars (NOTDORY_SERVER_URL, NOTDORY_ADMIN_EMAIL, NOTDORY_TENANT_ID) at startup.
// Leaving this empty means the SPA falls back to the compile-time defaults
// baked in by vite.config.js.
window.__NOTDORY_CONFIG__ = {};
