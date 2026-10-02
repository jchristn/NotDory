---
type: "Project"
title: "C# code style used throughout src/"
description: "No `var`, usings inside namespace, #region blocks, XML docs on everything, ConfigureAwait(false), String.Equals with StringComparison."
timestamp: "2026-10-02T02:20:58.4561070Z"
created: "2026-10-02T02:20:58.0647830Z"
slug: "csharp-code-style"
category: "cat_muqc4mri_rCkFbqhlsLsZwNr1djg"
links: ["schema-migrations"]
version: "1"
salience: "0.5"
---
Follow the existing style exactly (see src/NotDory.Server/Services/MemoryService.cs):
- Never use `var`: there are zero occurrences in Core/Server. Write explicit types.
- `using` directives go inside the namespace block (file-scoped namespaces are not used).
- One public type per file; enums are suffixed Enum (e.g. StoreProviderEnum) and live in Core/Enums.
- Class members are grouped in #region blocks named Public-Members, Private-Members, Constructors-and-Factories, Public-Methods, Private-Methods (and Internal-Members where needed).
- XML doc comments (summary/param/returns/exception) on every public and private member, including test helpers.
- Every await uses .ConfigureAwait(false); async methods take a CancellationToken token = default.
- Compare strings with String.Equals(a, b, StringComparison.Ordinal or OrdinalIgnoreCase), using capital-S `String`.
- Validate constructor args with `?? throw new ArgumentNullException(nameof(x))`.
- IDs come from IdGenerator/PrettyId with type prefixes (ten_, scp_, cat_, mem_, ins_, rep_...).
