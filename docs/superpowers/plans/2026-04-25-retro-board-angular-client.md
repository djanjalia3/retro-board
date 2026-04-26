# RetroBoard Angular Client Rewrite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Firebase RTDB-backed Angular client with one that talks to the new .NET + Postgres API: HTTP for writes, `@microsoft/signalr` for the realtime read stream.

**Architecture:** Single Angular build target. One `RetroBoardApiService` exposing the same surface the rest of the app already uses (`createBoard`, `addCard`, `voteCard`, `observeBoard`, `observePresence`, etc.). All Firebase code, config, and dependencies removed from the repo. Components depend directly on the new service — no abstraction layer or DI token gymnastics.

**Tech Stack:** Angular 21, RxJS, `@microsoft/signalr`, Angular Material/CDK (already present), xlsx (already present, used by import).

**Spec reference:** `docs/superpowers/specs/2026-04-25-retro-board-dotnet-rewrite-design.md`

**Prerequisite:** Plan 1 (backend) is complete. The .NET API runs locally at `http://localhost:5000` with a docker-composed Postgres.

---

## File Structure (Client changes)

```
src/
  app/
    services/
      retro-board-firebase.service.ts        DELETE
      retro-board-api.service.ts             CREATE
    pages/
      board/board.component.ts               MODIFY (swap injected service)
      board-list/board-list.component.ts     MODIFY (swap injected service)
      boards-all/boards-all.component.ts     MODIFY (swap injected service)
    models/
      retro-board.model.ts                   MODIFY (DTO shapes match API)
    app.config.ts                            MODIFY (provide HttpClient, drop Firebase)
  environments/
    environment.ts                           MODIFY
    environment.prod.ts                      MODIFY
firebase.json                                DELETE
database.rules.json                          DELETE
package.json                                 MODIFY (drop firebase, @angular/fire; add @microsoft/signalr)
```

---

## Task 1: Remove Firebase config files and dependencies

**Files:**
- Delete: `firebase.json`
- Delete: `database.rules.json`
- Modify: `package.json`

- [ ] **Step 1: Delete Firebase config files**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
rm firebase.json database.rules.json
```

- [ ] **Step 2: Remove Firebase deps and add SignalR**

Open `package.json`. In `dependencies`, remove the `firebase` and `@angular/fire` entries. Add `"@microsoft/signalr": "^9.0.0"`. The result for `dependencies` should look like:

```json
"dependencies": {
  "@angular/animations": "^21.1.5",
  "@angular/cdk": "^21.1.5",
  "@angular/common": "^21.1.0",
  "@angular/compiler": "^21.1.0",
  "@angular/core": "^21.1.0",
  "@angular/forms": "^21.1.0",
  "@angular/material": "^21.1.5",
  "@angular/platform-browser": "^21.1.0",
  "@angular/router": "^21.1.0",
  "@microsoft/signalr": "^9.0.0",
  "@tailwindcss/postcss": "^4.2.0",
  "rxjs": "~7.8.0",
  "tailwindcss": "^4.2.0",
  "tslib": "^2.3.0",
  "xlsx": "^0.18.5"
}
```

- [ ] **Step 3: Reinstall**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
npm install
```

Expected: completes without errors. `node_modules/firebase` and `node_modules/@angular/fire` no longer present.

- [ ] **Step 4: Commit**

```bash
git add package.json package-lock.json
git rm firebase.json database.rules.json
git commit -m "chore(client): remove Firebase config and deps; add @microsoft/signalr"
```

---

## Task 2: Update environment files

**Files:**
- Modify: `src/environments/environment.ts`
- Modify: `src/environments/environment.prod.ts`

- [ ] **Step 1: Replace dev environment**

`src/environments/environment.ts`:

```ts
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5000',
  hubUrl: 'http://localhost:5000/hubs/board',
};
```

- [ ] **Step 2: Replace prod environment**

`src/environments/environment.prod.ts`:

```ts
export const environment = {
  production: true,
  apiBaseUrl: '/api',
  hubUrl: '/hubs/board',
};
```

(In production both API and SPA are served from the same origin via the cluster ingress; relative URLs avoid CORS and hard-coded hostnames.)

- [ ] **Step 3: Commit**

```bash
git add src/environments
git commit -m "chore(client): point environments at new API and SignalR hub"
```

---

## Task 3: Update model DTOs to match API

**Files:**
- Modify: `src/app/models/retro-board.model.ts`

- [ ] **Step 1: Replace contents**

`src/app/models/retro-board.model.ts`:

```ts
export interface RetroColumn {
  id: number;
  position: number;
  title: string;
}

export interface RetroCard {
  id: string;
  columnId: number;
  columnIndex: number;
  text: string;
  author: string;
  createdAt: string;
  votes: number;
}

export interface RetroBoard {
  id: number;
  slug: string;
  name: string;
  createdAt: string;
  columns: RetroColumn[];
  cards: RetroCard[];
}

export interface RetroBoardSummary {
  id: number;
  slug: string;
  name: string;
  createdAt: string;
}

export interface RetroParticipant {
  participantKey: string;
  displayName: string;
  joinedAt: string;
  lastSeenAt: string;
  connectionCount: number;
}

export interface VoteResult {
  voted: boolean;
  votes: number;
}
```

- [ ] **Step 2: Commit**

```bash
git add src/app/models/retro-board.model.ts
git commit -m "feat(client): align model DTOs with API contract"
```

---

## Task 4: Create `RetroBoardApiService`

**Files:**
- Create: `src/app/services/retro-board-api.service.ts`

- [ ] **Step 1: Write the service**

`src/app/services/retro-board-api.service.ts`:

```ts
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Observable, ReplaySubject, firstValueFrom, lastValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  RetroBoard,
  RetroBoardSummary,
  RetroCard,
  RetroParticipant,
  VoteResult,
} from '../models/retro-board.model';

interface BoardStream {
  board$: ReplaySubject<RetroBoard | null>;
  presence$: ReplaySubject<RetroParticipant[]>;
  hub: HubConnection;
  current: RetroBoard | null;
  refCount: number;
  heartbeat: ReturnType<typeof setInterval> | null;
}

@Injectable({ providedIn: 'root' })
export class RetroBoardApiService {
  private http = inject(HttpClient);
  private streams = new Map<string, BoardStream>();

  // ----- HTTP writes -----

  async createBoard(name: string, columns?: string[]): Promise<string> {
    const dto = await firstValueFrom(this.http.post<RetroBoard>(
      `${environment.apiBaseUrl}/api/boards`,
      { name, columns: columns ?? null }));
    return dto.slug;
  }

  async importBoard(
    name: string,
    columns: string[],
    cards: Array<{ text: string; author: string; columnIndex: number; votes: number }>,
  ): Promise<string> {
    const dto = await firstValueFrom(this.http.post<RetroBoard>(
      `${environment.apiBaseUrl}/api/boards/import`,
      { name, columns, cards }));
    return dto.slug;
  }

  async boardExists(slug: string): Promise<boolean> {
    try {
      await lastValueFrom(this.http.head(
        `${environment.apiBaseUrl}/api/boards/${slug}`,
        { observe: 'response' }));
      return true;
    } catch {
      return false;
    }
  }

  getBoard(slug: string): Promise<RetroBoard | null> {
    return firstValueFrom(this.http.get<RetroBoard>(
      `${environment.apiBaseUrl}/api/boards/${slug}`)).catch(() => null);
  }

  listBoards(): Promise<RetroBoardSummary[]> {
    return firstValueFrom(this.http.get<RetroBoardSummary[]>(
      `${environment.apiBaseUrl}/api/boards`));
  }

  async addCard(slug: string, card: { text: string; author: string; columnIndex: number }): Promise<void> {
    await firstValueFrom(this.http.post(
      `${environment.apiBaseUrl}/api/boards/${slug}/cards`, card));
  }

  async deleteCard(slug: string, cardId: string): Promise<void> {
    await firstValueFrom(this.http.delete(
      `${environment.apiBaseUrl}/api/boards/${slug}/cards/${cardId}`));
  }

  async voteCard(slug: string, cardId: string, sessionId: string): Promise<boolean> {
    const result = await firstValueFrom(this.http.post<VoteResult>(
      `${environment.apiBaseUrl}/api/boards/${slug}/cards/${cardId}/votes`,
      { sessionId }));
    return result.voted;
  }

  // ----- SignalR realtime -----

  observeBoard(slug: string): Observable<RetroBoard | null> {
    return new Observable<RetroBoard | null>(sub => {
      let inner: { unsubscribe: () => void } | null = null;
      this.attach(slug).then(s => {
        inner = s.board$.subscribe(v => sub.next(v));
      });
      return () => {
        inner?.unsubscribe();
        this.detach(slug);
      };
    });
  }

  observePresence(slug: string): Observable<RetroParticipant[]> {
    return new Observable<RetroParticipant[]>(sub => {
      let inner: { unsubscribe: () => void } | null = null;
      this.attach(slug).then(s => {
        inner = s.presence$.subscribe(v => sub.next(v));
      });
      return () => {
        inner?.unsubscribe();
        this.detach(slug);
      };
    });
  }

  async joinPresence(slug: string, sessionId: string, displayName: string): Promise<void> {
    const stream = await this.attach(slug);
    if (stream.hub.state === HubConnectionState.Connected) {
      const participants = await stream.hub.invoke<RetroParticipant[]>(
        'JoinBoard', slug, sessionId, displayName);
      stream.presence$.next(participants);
    }
  }

  async leavePresence(slug: string): Promise<void> {
    const stream = this.streams.get(slug);
    if (!stream) return;
    if (stream.hub.state === HubConnectionState.Connected) {
      await stream.hub.invoke('LeaveBoard', slug);
    }
  }

  // ----- internal: connection lifecycle -----

  private async attach(slug: string): Promise<BoardStream> {
    let stream = this.streams.get(slug);
    if (stream) {
      stream.refCount++;
      return stream;
    }
    const hub = new HubConnectionBuilder()
      .withUrl(environment.hubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    stream = {
      board$: new ReplaySubject<RetroBoard | null>(1),
      presence$: new ReplaySubject<RetroParticipant[]>(1),
      hub,
      current: null,
      refCount: 1,
      heartbeat: null,
    };
    this.streams.set(slug, stream);

    hub.on('CardAdded', (card: RetroCard) => {
      if (!stream!.current) return;
      stream!.current = { ...stream!.current, cards: [...stream!.current.cards, card] };
      stream!.board$.next(stream!.current);
    });
    hub.on('CardDeleted', (cardId: string) => {
      if (!stream!.current) return;
      stream!.current = {
        ...stream!.current,
        cards: stream!.current.cards.filter(c => c.id !== cardId),
      };
      stream!.board$.next(stream!.current);
    });
    hub.on('VoteCast', (cardId: string, votes: number) => {
      if (!stream!.current) return;
      stream!.current = {
        ...stream!.current,
        cards: stream!.current.cards.map(c => c.id === cardId ? { ...c, votes } : c),
      };
      stream!.board$.next(stream!.current);
    });
    hub.on('PresenceChanged', (participants: RetroParticipant[]) => {
      stream!.presence$.next(participants);
    });

    hub.onreconnected(async () => {
      const board = await this.getBoard(slug);
      stream!.current = board;
      stream!.board$.next(board);
    });

    await hub.start();

    const board = await this.getBoard(slug);
    stream.current = board;
    stream.board$.next(board);

    stream.heartbeat = setInterval(() => {
      if (hub.state === HubConnectionState.Connected) hub.invoke('Heartbeat').catch(() => {});
    }, 60_000);

    return stream;
  }

  private async detach(slug: string): Promise<void> {
    const stream = this.streams.get(slug);
    if (!stream) return;
    stream.refCount--;
    if (stream.refCount > 0) return;
    this.streams.delete(slug);
    if (stream.heartbeat) clearInterval(stream.heartbeat);
    try { await stream.hub.stop(); } catch { /* ignore */ }
  }
}

// Convenience export of the slug helper used by board-list create form.
export function slugify(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/\s+/g, '-')
    .replace(/[^a-z0-9-]/g, '')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
}
```

- [ ] **Step 2: Build to verify imports compile**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
npx ng build --configuration=development
```

Expected: build succeeds. (May fail at component level until Task 6 is done — that's OK, just verify the service compiles by itself first.)

If the build fails because components still import from `retro-board-firebase.service`, defer this verification step — it will pass after Task 6.

- [ ] **Step 3: Commit**

```bash
git add src/app/services/retro-board-api.service.ts
git commit -m "feat(client): add RetroBoardApiService (HTTP + SignalR)"
```

---

## Task 5: Wire up `HttpClient` in `app.config.ts`

**Files:**
- Modify: `src/app/app.config.ts`

- [ ] **Step 1: Update config**

Read the current `src/app/app.config.ts` first. Replace its contents with:

```ts
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withFetch()),
    provideAnimationsAsync(),
  ],
};
```

(Adapt to existing imports — the goal is to provide `HttpClient` and remove any Firebase providers.)

- [ ] **Step 2: Build**

```bash
npx ng build --configuration=development
```

Expected: SPA still has component-level errors (next task) but `app.config.ts` itself is fine.

- [ ] **Step 3: Commit**

```bash
git add src/app/app.config.ts
git commit -m "chore(client): provide HttpClient; drop Firebase providers"
```

---

## Task 6: Migrate page components to `RetroBoardApiService`

**Files:**
- Modify: `src/app/pages/board-list/board-list.component.ts`
- Modify: `src/app/pages/boards-all/boards-all.component.ts`
- Modify: `src/app/pages/board/board.component.ts`
- Delete: `src/app/services/retro-board-firebase.service.ts`

> Each file currently injects `RetroBoardFirebaseService` and imports `slugify`/`participantKey` from it. The migration is mechanical: change the import path and the injected type. Field/method names exposed by `RetroBoardApiService` mirror the Firebase service one-for-one (`createBoard`, `importBoard`, `listBoards`, `boardExists`, `getBoard`, `addCard`, `deleteCard`, `voteCard`, `observeBoard`, `observePresence`, `joinPresence`, `leavePresence`). The `leavePresence` no longer takes `(sessionId, displayName)`; it takes only `(slug)`. Adjust call sites accordingly.

- [ ] **Step 1: Read each component file to confirm injection pattern**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
grep -n "RetroBoardFirebaseService\|retro-board-firebase\.service\|leavePresence" \
  src/app/pages/board-list/board-list.component.ts \
  src/app/pages/boards-all/boards-all.component.ts \
  src/app/pages/board/board.component.ts
```

- [ ] **Step 2: For each component, swap the import and injected type**

In each component file, change:

```ts
import { RetroBoardFirebaseService, slugify } from '../../services/retro-board-firebase.service';
// ...
private retro = inject(RetroBoardFirebaseService);
```

to:

```ts
import { RetroBoardApiService, slugify } from '../../services/retro-board-api.service';
// ...
private retro = inject(RetroBoardApiService);
```

(Use `RetroBoardApiService` everywhere `RetroBoardFirebaseService` appeared.)

- [ ] **Step 3: Adjust `leavePresence` call sites**

Find every `leavePresence(...)` call. Old shape: `leavePresence(boardId, sessionId, displayName)`. New shape: `leavePresence(slug)`. Pass only the slug. Example:

```ts
// Before
this.retro.leavePresence(this.boardId, this.sessionId, this.displayName);
// After
this.retro.leavePresence(this.boardId);
```

- [ ] **Step 4: Adjust any field references that changed shape**

The new `RetroBoard` has `slug`, `id`, `columns: RetroColumn[]`, `cards: RetroCard[]`. The Firebase shape used `cards: { [cardId: string]: RetroCard }`. If a template or component iterates `Object.entries(board.cards)`, change to iterate the array directly. Cards now have `id`, `columnIndex`, `votes` as properties — no need to `Object.keys` or compute anything.

Run a grep to find adapters:

```bash
grep -rn "Object\.entries\|Object\.keys\|board\.cards\[" src/app/pages
```

Update any matches to use the array.

- [ ] **Step 5: Delete the Firebase service**

```bash
rm src/app/services/retro-board-firebase.service.ts
```

- [ ] **Step 6: Build**

```bash
npx ng build --configuration=development
```

Expected: build succeeds with zero errors.

- [ ] **Step 7: Commit**

```bash
git add src/app/pages
git rm src/app/services/retro-board-firebase.service.ts
git commit -m "feat(client): switch components to RetroBoardApiService; remove Firebase service"
```

---

## Task 7: End-to-end smoke against local API

- [ ] **Step 1: Bring up backend (from Plan 1)**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
docker compose up -d postgres
cd server
dotnet run --project src/RetroBoard.Api &
APIPID=$!
sleep 5
```

- [ ] **Step 2: Start Angular dev server in another shell**

```bash
cd /Users/davitjanjalia/Desktop/retro-board
npx ng serve --port 4200
```

Open `http://localhost:4200`.

- [ ] **Step 3: Manual checklist**

Verify in the browser:
- Home page loads without console errors. The "find board" form is present.
- Create a new board ("Smoke Test") → redirects to the board view, shows default columns.
- Add a card to "What went well" → appears immediately.
- Open the same board URL in a second browser/tab with a different display name → both tabs see each other in presence; adding a card in one tab appears in the other within ~1 second.
- Vote on a card → votes count goes up; voting again from the same tab doesn't double-count.
- Delete a card → disappears from both tabs.
- Refresh the page → board state restored from `GET /api/boards/{slug}` and live updates resume.
- Check the Network tab: no 404s, no failed requests, WebSocket (or SSE/long-poll fallback) connection to `/hubs/board` stays open.

- [ ] **Step 4: Stop the API**

```bash
kill $APIPID
```

- [ ] **Step 5: If anything is broken, debug and commit fixes**

Use `superpowers:systematic-debugging` if issues arise. Commit fixes with:

```bash
git commit -am "fix(client): <description>"
```

If everything passes, no commit needed for this task.
