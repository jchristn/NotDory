---
type: "Project"
title: "Authentication and tenancy model"
description: "Users log in with email+password for a bearer session token; MCP/automation use a credential access key (x-access-key or Bearer)."
timestamp: "2026-10-02T02:20:18.0796990Z"
created: "2026-10-02T02:20:17.7824130Z"
slug: "auth-model"
category: "cat_muqc4lkf_GnQe0cOo5FhYaecgFm1"
links: ["system-overview"]
version: "1"
salience: "0.5"
---
Interactive users sign in with email + password and get a session token (Authorization: Bearer). Automation and MCP authenticate with a credential access key, sent as `x-access-key` or `Authorization: Bearer <accessKey>`; the access key works alone as a capability token, and `x-secret-key` is optional and validated only when present (single-header clients like Mux send just the access key). Admin authority comes from user IsAdmin / IsTenantAdmin flags. The local-dev default access key is `notdorydefaultkey` (settings auth.defaultAccessKey); the seed admin is admin@notdory.local, with its password supplied via env. Everything is tenant-scoped: the REST API is under /v1.0/api/..., and tenant isolation is covered by tests (tenant-isolation, http-auth-and-isolation). Code: Server/Services/AuthenticationService.cs, AuthorizationService.cs, Core/Security.
