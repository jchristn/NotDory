---
type: "Reference"
title: "Docker Desktop \"storage device attachment is invalid\" = Docker.raw held open by another process"
description: "If Docker Desktop won't start (VM error \"storage device attachment is invalid\"), lsof Docker.raw; on 2026-10-01 it was a debug Armor.Agent holding it."
timestamp: "2026-10-02T04:11:20.2506490Z"
created: "2026-10-02T04:11:17.4284300Z"
slug: "docker-desktop-vm-disk-locked"
category: "cat_muqc4o7d_rLatiiemnWQTDAX9VoT"
links: ["publish-images", "test-commands"]
version: "1"
salience: "0.5"
---
Symptom (2026-10-01): Docker Desktop reported "unable to start" and API calls returned 500, so the live DB tests (PostgreSQL, MySQL, SQL Server) and the local pull in build-*.sh failed. ~/Library/Containers/com.docker.docker/Data/log/host/com.docker.virtualization.log showed "VM has stopped: Invalid virtual machine configuration. The storage device attachment is invalid."
Cause: another process had the VM disk open. `lsof ~/Library/Containers/com.docker.docker/Data/vms/0/data/Docker.raw` showed the maintainer's Debug build of Armor.Agent (~/Code/Armor).
Fix: stop that process, quit or kill any stuck Docker Desktop instances, then run `open -a Docker`. The engine came up in about 10 s. `docker desktop restart` hung while the disk was locked and left extra Docker icons in the dock.
Also: build-*.sh push through the cloud buildx builder even with the local engine down; only the final `docker pull` needs the local daemon. The NotDory MCP the maintainer uses runs on a remote host (view.homedns.org:8720), not in local Docker.
