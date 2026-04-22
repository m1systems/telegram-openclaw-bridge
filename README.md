# Telegram → OpenClaw Bridge

A lightweight bridge that connects a Telegram bot to a local OpenClaw gateway, allowing you to interact with your AI agent from anywhere via Telegram.

---

## Overview

This project runs as a small, self-contained .NET service on a Linux host and forwards Telegram messages to an OpenClaw gateway using the OpenAI-compatible API.

It is designed for:

* **secure, local-first operation**
* **minimal dependencies**
* **systemd-based deployment**
* **clean integration with OpenClaw agent workflows**

---

## Architecture

```
Telegram
  ↓
Telegram Bot API (polling)
  ↓
TelegramOpenClaw (this app)
  ↓
OpenClaw Gateway (localhost)
  ↓
LLM / tools / agent workspace
```

Key design decisions:

* OpenClaw is bound to `127.0.0.1` (not exposed to LAN)
* Bridge runs on the same host
* Access is controlled via:

  * Telegram allow-list
  * rate limiting
* No external webhooks or public endpoints required

---

## Features

* Telegram bot polling (no webhook setup required)
* Allow-list enforcement (only approved users can interact)
* One-time unauthorized response + optional silent ignore
* Rate limiting to prevent abuse or loops
* Persistent OpenClaw sessions per chat, with generation-based reset support:

```
x-openclaw-session-key: telegram:<chatId>        (initial session)
x-openclaw-session-key: telegram:<chatId>:<N>    (after N resets)
```

* Local slash commands:

  * `/help`
  * `/status`
  * `/reset` — increments the session generation and starts a fresh OpenClaw context for the current chat
    (replies: `Session reset. Starting fresh.`)
* Natural chat (no command prefix required)
* Photo message support with optional caption forwarding
* Console logging via `journalctl` (no log files)

---

## Why `/reset` exists

LLMs have finite context windows. Over time, long conversations can degrade or fail due to context overflow.

The `/reset` command allows you to explicitly start a fresh session while preserving chat identity using generation-based session keys.

---

## Requirements

* Linux host (Ubuntu recommended)
* OpenClaw gateway running locally
* .NET publish environment (for building)
* Telegram bot token

---

## Configuration

The application loads configuration from:

```
~/.config/telegram-openclaw/appsettings.json
```

### Example

```bash
mkdir -p ~/.config/telegram-openclaw
cp TelegramOpenClaw/appsettings.example.json ~/.config/telegram-openclaw/appsettings.json
nano ~/.config/telegram-openclaw/appsettings.json
```

### Required Settings

```json
{
  "Telegram": {
    "BotToken": "YOUR_BOT_TOKEN",
    "AllowedUserIds": [123456789]
  },
  "OpenClaw": {
    "BaseUrl": "http://127.0.0.1:18789",
    "ChatEndpoint": "/v1/chat/completions"
  }
}
```

### Notes

* Do **not** commit your real config file
* The repo includes `appsettings.example.json` as a template only
* The application will fail to start if required settings are missing

---

## Build & Publish

Build a self-contained Linux binary:

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish/linux-x64
```

---

## Deployment

### 1. Create directories

```bash
mkdir -p /home/<user>/apps/telegram-openclaw
mkdir -p /home/<user>/.config/telegram-openclaw
```

### 2. Copy published output

```bash
scp -r ./publish/linux-x64/* <user>@<host>:/home/<user>/apps/telegram-openclaw/
```

### 3. Create config

```bash
nano ~/.config/telegram-openclaw/appsettings.json
```

---

## systemd Service

Create:

```
~/.config/systemd/user/telegram-openclaw.service
```

```ini
[Unit]
Description=Telegram OpenClaw Bridge
After=network-online.target openclaw-gateway.service
Wants=network-online.target
Requires=openclaw-gateway.service

[Service]
Type=simple
WorkingDirectory=/home/<user>/apps/telegram-openclaw
ExecStart=/home/<user>/apps/telegram-openclaw/TelegramOpenClaw
Restart=always
RestartSec=5

[Install]
WantedBy=default.target
```

### Enable and start

```bash
systemctl --user daemon-reload
systemctl --user enable telegram-openclaw
systemctl --user start telegram-openclaw
```

---

## Logging

Logs are handled via systemd:

```bash
journalctl --user -u telegram-openclaw -f
```

No log files are created by the application.

---

## Usage

Once running, send messages to your Telegram bot:

* normal messages → forwarded to OpenClaw
* photo messages → forwarded to OpenClaw, with caption included if present
* `/help` → shows available commands
* `/status` → shows bridge and OpenClaw status
* `/reset` → increments session generation and starts a fresh OpenClaw session
  (replies: `Session reset. Starting fresh.`)

---

## Session State

Session state (chat generation numbers) is persisted to:

```
~/.config/telegram-openclaw/session-state.json
```

Behavior:

* Lightweight JSON storage
* Each chat tracks its own generation counter
* Deleting the file resets all sessions
* Restarting the service does **not** reset sessions

---

## Operational Notes

* Very long conversations may require `/reset` due to LLM context limits
* Telegram photo messages include multiple size variants (largest is used)
* The bridge uses polling — no inbound ports required
* Session state persists independently of service lifecycle

---

## Security Model

* OpenClaw is bound to `localhost` only
* No public HTTP endpoints are exposed
* Telegram access is restricted via allow-list
* Unauthorized users:

  * receive one response
  * then are ignored (configurable)
* Rate limiting prevents abuse

This follows a **controlled access model**:

> Telegram → trusted bridge → localhost OpenClaw → agent system

---

## Development

Run locally:

```bash
dotnet run --project TelegramOpenClaw
```

Ensure your config file exists at:

```
~/.config/telegram-openclaw/appsettings.json
```

---

## Agent Workflow Compatibility

This repository includes an `AGENTS.md` file that defines strict rules for autonomous agent contributions.

Key guarantees:

* agents work only on feature branches
* `main` remains protected
* all changes go through pull requests
* deterministic git workflow is enforced

This makes the repo safe for OpenClaw-driven development workflows.

---

## Future Enhancements

* additional control commands
* session reset / fork behavior
* richer status output
* broader media support beyond photos
* multi-agent routing

---

## Summary

This project provides a minimal, production-ready bridge between Telegram and a local OpenClaw agent:

* secure (localhost-only)
* simple (no webhooks, no public endpoints)
* controllable (allow-list + rate limiting)

```
Telegram → OpenClaw → your agent system
```

---

## License

MIT
