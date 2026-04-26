# Retro Board — User Guide

A practical walk-through of the app at **https://retroboard.win**. Aimed at end users (anyone you share the link with), not developers.

> **Maintenance:** this guide is a living document. If you add or change a user-facing feature, update the relevant section here in the same change.

## Contents

1. [Getting started](#1-getting-started)
2. [The home page](#2-the-home-page)
3. [The board page](#3-the-board-page)
4. [Realtime presence](#4-realtime-presence)
5. [Cards and voting](#5-cards-and-voting)
6. [Import / export](#6-import--export)
7. [The "all boards" page](#7-the-all-boards-page)
8. [Limits & known quirks](#8-limits--known-quirks)
9. [FAQ](#9-faq)

---

## 1. Getting started

### What this is

A single-page web app for running a team retrospective. You create a board with four columns, share the URL with the team, and everyone adds cards and votes in real time.

The default columns are:

- **What went well**
- **What didn't go well**
- **Shoutouts**
- **Action items**

### Accessing it

Open https://retroboard.win in any modern browser (Chrome, Firefox, Safari, Edge). No sign-up, no account, no email. Anyone with the link to a specific board can see and edit it — treat the slug as a soft secret if you care about privacy.

You'll be prompted for a **display name** the first time you open a board. The name is stored in the browser's `sessionStorage` (cleared when you close the tab) — every new tab/window asks again.

### What gets stored

- **Server-side** (Postgres): boards, columns, cards, vote counts, presence (display name + connection count).
- **Browser-side** (`sessionStorage`): your display name and a per-tab session id. Both are cleared on tab close.
- **Browser-side** (in-memory): which cards you've voted on this session — this is **not** persisted across refreshes (see [Limits & known quirks](#8-limits--known-quirks)).

---

## 2. The home page

The home page (`https://retroboard.win/`) has three things:

### Create a new board

Type a name → **Create board**. The app derives a URL slug from the name (lowercase, dashes, ASCII-only). Example: `Sprint 47 retro` → `sprint-47-retro`. Slugs must be unique — if the slug is already taken you'll see "Failed to create board" and need to pick a different name.

You're redirected to `/board/<slug>` immediately.

### Find an existing board

Type a board name (or slug) → **Find board**. The app re-slugifies your input and checks the server. If the board exists, you're redirected; if not, you see "Board not found".

### Import a board from a JSON file

Pick a JSON file (the format produced by **Export JSON** on a board page; see [Import / export](#6-import--export)). The file must have at minimum a `columns` string array. The board name comes from the **Name** field if you fill it, otherwise from the file's `name` field.

Cards in the file with empty `text` are dropped silently. `votes` are imported as raw counts; per-user voter information is **not** preserved across export/import.

### Browse all boards

Click **All boards** in the header to go to the directory page (see [The "all boards" page](#7-the-all-boards-page)).

---

## 3. The board page

URL shape: `/board/<slug>` (e.g. `https://retroboard.win/board/sprint-47-retro`).

### Display name prompt

If `sessionStorage` has no display name yet, a modal appears asking for one. You can type any name and press **Join Board** (or hit Enter). Anonymous-style names like `🤐` work fine. The name is then visible to everyone else viewing the same board.

### Header

Shows the board name, the slug (in a code-styled pill), the display name you joined with, and an **Export JSON** button.

### Participants strip

Below the header, a row of pill-shaped chips shows everyone who has joined this board. A green dot = currently online (at least one open browser tab). A grey dot = previously joined but currently offline.

The list updates live as people open and close the page.

### Columns grid

Four columns, color-coded along the top edge (emerald / rose / amber / sky). Each column has:

- A column title.
- An **Add a card…** input with an **Add** (`+`) button. Press Enter or click the button to add the card.
- A **Post anonymously** checkbox under the input — if checked, the card's author is shown as `Anonymous` instead of your display name. The toggle is per-column and per-tab; it doesn't reset after each post.
- A list of cards already in the column.

### A card

A card shows:

- The card text.
- The author's display name (italic if `Anonymous`).
- A vote button on the right with the current vote count and an upward arrow icon.
- A small **×** delete button in the top right that appears on hover.

---

## 4. Realtime presence

The board page maintains a live SignalR connection to the server (`wss://retroboard.win/hubs/board`). Through it:

- **Card adds, deletes, and votes** by anyone in the board are pushed to all connected tabs immediately.
- **Presence** (who's currently joined) is recomputed and broadcast whenever someone joins, leaves, or fully disconnects.
- A heartbeat is sent every 60 seconds to keep your "online" status fresh and clean up stale entries from people who closed their laptop without disconnecting cleanly.

You don't have to do anything to enable this — it just works while the board page is open.

If your network blinks and the connection drops, the SignalR client auto-reconnects and re-fetches the full board state on success. You may briefly see stale data during a reconnect.

---

## 5. Cards and voting

### Adding a card

Type into a column's input → Enter (or click the **+** button). The card appears immediately for you and within a second or two for everyone else watching.

If you have **Post anonymously** checked for that column, the card author shows as `Anonymous`.

### Voting

Click the vote button on a card. The count goes up by one for everyone, and the button is disabled (greyed out) for **you in this tab**.

Behind the scenes, votes are tracked server-side per browser-tab session id (a UUID stored in `sessionStorage`). The same session can't vote twice on the same card — a second click returns "already voted" silently and the count doesn't change.

> **Quirk:** if you refresh the page, the local "you've already voted on this card" state is lost (the server still has it, and a re-vote will be no-op'd, but the button won't appear greyed-out until you click it once and the server tells you it's a duplicate). See [Limits & known quirks](#8-limits--known-quirks).

### Deleting a card

Hover a card → click the **×** in the top right → confirm in the dialog. The card disappears for everyone immediately. There is no undo.

---

## 6. Import / export

### Export JSON

The **Export JSON** button in the board header downloads a file named `<BoardName>_retro.json` containing:

```json
{
  "name": "Sprint 47 retro",
  "columns": ["What went well", "What didn't go well", "Shoutouts", "Action items"],
  "cards": [
    { "text": "...", "author": "...", "columnIndex": 0, "votes": 3 }
  ],
  "exportedAt": 1714138923000,
  "version": 1
}
```

Notes:
- `columns` is an array of column **titles** (strings), not the database column objects.
- `cards` is the cards array, with `columnIndex` referring to position in the `columns` array (0-based).
- `votes` is the raw count; **no per-voter info is exported**.

### Import JSON

On the home page, the import section accepts a JSON file in the format above. You can override the board name in the **Name** field. Cards with empty `text` are dropped. `votes` are preserved as raw counts.

A new board is created (slug derived from the new name); the import does **not** merge into an existing board.

---

## 7. The "all boards" page

`https://retroboard.win/boards` (linked from the home page).

A flat list of every board on the server, with name, slug, and creation date. Click any row to jump into that board.

This page is essentially a directory — anyone who can reach the home page can also see the full board list. There's no per-board access control.

---

## 8. Limits & known quirks

- **No accounts, no auth.** Anyone with a board's URL can view, add, delete, and vote. Slugs are derived from board names so they're guessable. If you need privacy, name your board with a random string (e.g. `q3-retro-7f4a2c`) — but this is security-by-obscurity at best.
- **All boards visible to all visitors.** The `/boards` directory page lists every board on the server. There's no way to mark a board "private".
- **No edit on cards.** You can delete and re-add, but there's no in-place edit.
- **No threaded comments / replies** on cards. The card text and the author's name are the entirety of the card content.
- **No drag-and-drop.** Cards stay in the column they were created in.
- **No grouping / merging** of similar cards.
- **Vote button "already voted" UI does not survive a page refresh.** The server enforces idempotency (you can't double-vote across refreshes), but the local indicator that you've voted is lost. If you click again, the server says "already counted" and the count stays the same — UX-wise it just looks like nothing happens.
- **Display name is per-tab.** Open in two browser windows and you'll be prompted twice. This is intentional — you can join the same board as two different participants from one machine for testing.
- **Card text is plain text.** No Markdown, no HTML, no links rendered as links.
- **No board deletion in the UI.** A board, once created, stays forever. Removal requires direct database access on the server.
- **Anonymous mode is per-tab and resets when you close.** It's not "your default" — re-check the box every session you want to use it.

## 9. FAQ

**Can I rename a board?** No. Create a new one and delete the old (admin-only) if needed.

**Can I rename or reorder columns?** No, the four columns are baked into the schema. (Custom columns can be passed at create time via `POST /api/boards { columns: [...] }`, but the UI doesn't expose that yet.)

**Where is my data stored?** Postgres on the home-server `drascula`. See `~/docs/home-server-operations.md` (operator only).

**Is it backed up?** Not yet. Backups are a known TODO across all home-server services.

**How do I delete a board?** Direct DB access only — `DELETE FROM boards WHERE slug = 'xxx';`. There's no UI or API for it.

**Can I run my own copy?** Yes — the repo at https://github.com/djanjalia3/retro-board is public. See the README and `docs/DEPLOYMENT.md` for the production stack, or just `docker compose -f docker-compose.prod.yml --env-file .env up --build` locally.

**Why does the page sometimes show "Loading board..." for a few seconds after a network blip?** The SignalR connection is reconnecting and re-fetching state. Should resolve in a couple of seconds.

**The realtime updates stopped working.** Reload the page. The SignalR auto-reconnect should re-establish the connection. If it doesn't, the tunnel may be down — check https://retroboard.win/api/boards in another tab; if that's also failing, the server is offline.
