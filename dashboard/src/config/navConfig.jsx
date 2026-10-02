import { IconHome, IconLayers, IconSearch, IconCpu, IconUsers, IconActivity, IconGear } from '../components/Icons';

/**
 * Single source of truth for the left nav and the tabbed hub pages. `Sidebar` renders
 * from `NAV_SECTIONS`, `HubView` renders a hub's tab strip from the item's `tabs`, and
 * `App` generates a replace-redirect for every `legacyPaths` entry so old bookmarks land
 * on the matching hub + tab (with the original query string preserved).
 *
 * gate values (client-side gating is UX only; the server still enforces authorization):
 *   'none'          everyone
 *   'admin'         global admin (isAdmin)
 *   'adminOrTenant' global admin or tenant admin
 *
 * An item with tabs is visible when at least one of its tabs is visible.
 * `matchers` are route path prefixes owned by the item (used for active highlighting).
 * `legacyPaths` are pre-hub routes (relative to /dashboard) that redirect to the tab.
 */

export const DASHBOARD_BASE = '/dashboard';

export const NAV_SECTIONS = [
  {
    key: 'workspace',
    labelKey: 'nav.sections.workspace',
    items: [
      { key: 'home', path: '/dashboard/home', labelKey: 'nav.home', icon: IconHome, gate: 'none' },
      {
        key: 'memory',
        path: '/dashboard/memory',
        labelKey: 'nav.memory',
        icon: IconLayers,
        // Scope detail routes (scopes/:scopeId/...) belong to the Memory hub.
        matchers: ['/dashboard/memory', '/dashboard/scopes'],
        tabs: [
          { key: 'scopes', labelKey: 'nav.tabs.scopes', gate: 'none', legacyPaths: ['scopes'] },
          { key: 'memories', labelKey: 'nav.tabs.memories', gate: 'none', legacyPaths: ['memories'] },
          { key: 'instructions', labelKey: 'nav.tabs.instructions', gate: 'none', legacyPaths: ['instructions'] }
        ]
      },
      {
        key: 'recall',
        path: '/dashboard/recall',
        labelKey: 'nav.recall',
        icon: IconSearch,
        tabs: [
          { key: 'search', labelKey: 'nav.tabs.search', gate: 'none', legacyPaths: ['search'] },
          { key: 'chat', labelKey: 'nav.tabs.chat', gate: 'none', legacyPaths: ['chat'] }
        ]
      }
    ]
  },
  {
    key: 'administration',
    labelKey: 'nav.sections.administration',
    items: [
      {
        key: 'models',
        path: '/dashboard/models',
        labelKey: 'nav.models',
        icon: IconCpu,
        tabs: [
          { key: 'embedding', labelKey: 'nav.tabs.embedding', gate: 'none', legacyPaths: ['endpoints/embedding'] },
          {
            key: 'inference',
            labelKey: 'nav.tabs.inference',
            gate: 'none',
            legacyPaths: ['endpoints/inference', 'endpoints/rerank']
          }
        ]
      },
      {
        key: 'access',
        path: '/dashboard/access',
        labelKey: 'nav.access',
        icon: IconUsers,
        tabs: [
          { key: 'tenants', labelKey: 'nav.tabs.tenants', gate: 'admin', legacyPaths: ['tenants'] },
          { key: 'users', labelKey: 'nav.tabs.users', gate: 'adminOrTenant', legacyPaths: ['users'] },
          { key: 'credentials', labelKey: 'nav.tabs.credentials', gate: 'adminOrTenant', legacyPaths: ['credentials'] }
        ]
      },
      {
        key: 'monitoring',
        path: '/dashboard/monitoring',
        labelKey: 'nav.monitoring',
        icon: IconActivity,
        tabs: [
          { key: 'requests', labelKey: 'nav.tabs.requests', gate: 'none', legacyPaths: ['request-history'] },
          { key: 'operations', labelKey: 'nav.tabs.operations', gate: 'none', legacyPaths: ['operations'] },
          { key: 'api-explorer', labelKey: 'nav.tabs.apiExplorer', gate: 'none', legacyPaths: ['api-explorer'] }
        ]
      },
      {
        key: 'system',
        path: '/dashboard/system',
        labelKey: 'nav.system',
        icon: IconGear,
        tabs: [
          { key: 'settings', labelKey: 'nav.tabs.settings', gate: 'none', legacyPaths: ['settings'] },
          {
            key: 'agent-onboarding',
            labelKey: 'nav.tabs.agentOnboarding',
            gate: 'none',
            legacyPaths: ['agent-protocol']
          },
          { key: 'collections', labelKey: 'nav.tabs.collections', gate: 'none', legacyPaths: ['collections'] }
        ]
      }
    ]
  }
];

/** Flat list of every nav item. */
export const NAV_ITEMS = NAV_SECTIONS.flatMap((section) => section.items);

/** Look up a hub (nav item) by key. */
export function findNavItem(key) {
  return NAV_ITEMS.find((item) => item.key === key) || null;
}

/** Evaluate a gate string against the current auth flags. */
export function gateVisible(gate, auth) {
  const { isAdmin, isTenantAdmin } = auth || {};
  switch (gate) {
    case 'admin':
      return !!isAdmin;
    case 'adminOrTenant':
      return !!(isAdmin || isTenantAdmin);
    case 'none':
    default:
      return true;
  }
}

/** True when the nav item should be shown (its own gate passes and, for hubs, any tab is visible). */
export function itemVisible(item, auth) {
  if (!gateVisible(item.gate || 'none', auth)) return false;
  if (!item.tabs) return true;
  return item.tabs.some((tab) => gateVisible(tab.gate || 'none', auth));
}

/** True when the current path belongs to the given nav item (exact or matcher prefix). */
export function itemIsActive(item, pathname) {
  const matchers = item.matchers && item.matchers.length ? item.matchers : [item.path];
  return matchers.some((m) => pathname === m || pathname.startsWith(`${m}/`));
}

/** Every legacy route → hub + tab redirect, derived from the config. */
export const LEGACY_REDIRECTS = NAV_ITEMS.flatMap((item) =>
  (item.tabs || []).flatMap((tab) =>
    (tab.legacyPaths || []).map((from) => ({ from, to: item.path, tab: tab.key }))
  )
);
