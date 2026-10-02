---
type: "Project"
title: "Dashboard nav: 7 tabbed hubs defined in navConfig.jsx"
description: "Workspace (Home, Memory, Recall) + Administration (Models, Access, Monitoring, System); tabs via ?tab=; old URLs redirect via legacyPaths."
timestamp: "2026-10-02T03:40:52.2520420Z"
created: "2026-10-02T03:40:51.9200630Z"
slug: "dashboard-navigation"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["solution-projects"]
version: "1"
salience: "0.5"
---
As of 2026-10-01 the dashboard sidebar follows the sibling projects (AssistantHub, Pneuma, LiteGraph) pattern of a few hubs with in-page tabs.
- Single source: dashboard/src/config/navConfig.jsx (NAV_SECTIONS with gate 'none'|'admin'|'adminOrTenant' per tab, matchers for active highlighting, legacyPaths for redirects).
- Workspace: Home; Memory (Scopes, Memories, Instructions; the scopes/:scopeId/... routes highlight Memory); Recall (Search, Chat).
- Administration: Models (Embedding, Inference); Access (Tenants admin-only, Users, Credentials); Monitoring (Requests, Operations, API Explorer); System (Settings, Agent Onboarding, Collections).
- Ordering principle: the operator's workflow (organize memory, recall it, configure models, access, watch traffic, system).
- Pieces: components/Tabs.jsx (ported from AssistantHub: ?tab= in the URL, arrow/Home/End keys, lazy panel), views/hubs/HubView.jsx + hubPanels.jsx, components/LegacyRedirect.jsx (replace, keeps the query string), context/HubContext.jsx (views inside a hub shrink their PageHeader).
- A hub whose tabs are all hidden is hidden. Add a page by adding a tab entry in navConfig and a panel in hubPanels.jsx.
- Request History hides the Principal and Tenant columns by default (DataTable supports default-hidden columns) and truncates Path.
