import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { RetroBoard, RetroCard, RetroParticipant } from '../models/retro-board.model';

const RTDB_REST_BASE = 'https://retro-board-75e44-default-rtdb.firebaseio.com';

export function slugify(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/\s+/g, '-')
    .replace(/[^a-z0-9-]/g, '')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
}

export function participantKey(displayName: string): string {
  const slug = slugify(displayName);
  return slug || `anon-${displayName.length}`;
}

async function restGet<T>(path: string): Promise<T | null> {
  const res = await fetch(`${RTDB_REST_BASE}/${path}.json`);
  if (!res.ok) throw new Error(`GET ${path} -> ${res.status}`);
  return (await res.json()) as T | null;
}

async function restPut<T>(path: string, value: T): Promise<void> {
  const res = await fetch(`${RTDB_REST_BASE}/${path}.json`, {
    method: 'PUT',
    body: JSON.stringify(value),
  });
  if (!res.ok) throw new Error(`PUT ${path} -> ${res.status}`);
}

async function restPatch<T>(path: string, value: T): Promise<void> {
  const res = await fetch(`${RTDB_REST_BASE}/${path}.json`, {
    method: 'PATCH',
    body: JSON.stringify(value),
  });
  if (!res.ok) throw new Error(`PATCH ${path} -> ${res.status}`);
}

async function restPost<T>(path: string, value: T): Promise<string> {
  const res = await fetch(`${RTDB_REST_BASE}/${path}.json`, {
    method: 'POST',
    body: JSON.stringify(value),
  });
  if (!res.ok) throw new Error(`POST ${path} -> ${res.status}`);
  const { name } = (await res.json()) as { name: string };
  return name;
}

async function restDelete(path: string): Promise<void> {
  const res = await fetch(`${RTDB_REST_BASE}/${path}.json`, { method: 'DELETE' });
  if (!res.ok) throw new Error(`DELETE ${path} -> ${res.status}`);
}

@Injectable({ providedIn: 'root' })
export class RetroBoardFirebaseService {
  async createBoard(name: string): Promise<string> {
    const slug = slugify(name);
    if (!slug) throw new Error('Invalid board name.');
    const existing = await restGet<RetroBoard>(`retro-boards/${slug}`);
    if (existing) throw new Error('Board name already taken, choose another.');
    const board: RetroBoard = {
      name,
      createdAt: Date.now(),
      columns: ['What went well', "What didn't go well", 'Shoutouts', 'Action items'],
    };
    await restPut(`retro-boards/${slug}`, board);
    return slug;
  }

  async boardExists(slug: string): Promise<boolean> {
    const data = await restGet<RetroBoard>(`retro-boards/${slug}`);
    return data !== null;
  }

  getBoard(boardId: string): Promise<RetroBoard | null> {
    return restGet<RetroBoard>(`retro-boards/${boardId}`);
  }

  observeBoard(boardId: string): Observable<RetroBoard | null> {
    return streamObservable<RetroBoard>(`retro-boards/${boardId}`);
  }

  async listBoards(): Promise<{ id: string; name: string; createdAt: number }[]> {
    const all = await restGet<Record<string, RetroBoard>>('retro-boards');
    if (!all) return [];
    return Object.entries(all)
      .map(([id, b]) => ({
        id,
        name: b?.name ?? id,
        createdAt: b?.createdAt ?? 0,
      }))
      .sort((a, b) => b.createdAt - a.createdAt);
  }

  async addCard(
    boardId: string,
    card: Omit<RetroCard, 'votes' | 'voters'>
  ): Promise<void> {
    await restPost(`retro-boards/${boardId}/cards`, { ...card, votes: 0 });
  }

  deleteCard(boardId: string, cardId: string): Promise<void> {
    return restDelete(`retro-boards/${boardId}/cards/${cardId}`);
  }

  async voteCard(
    boardId: string,
    cardId: string,
    sessionId: string
  ): Promise<boolean> {
    const card = await restGet<RetroCard>(
      `retro-boards/${boardId}/cards/${cardId}`
    );
    if (!card) return false;
    if (card.voters && card.voters[sessionId]) return false;
    await restPatch(`retro-boards/${boardId}/cards/${cardId}`, {
      votes: (card.votes || 0) + 1,
      [`voters/${sessionId}`]: true,
    });
    return true;
  }

  observePresence(
    boardId: string
  ): Observable<Record<string, RetroParticipant> | null> {
    return streamObservable<Record<string, RetroParticipant>>(`presence/${boardId}`);
  }

  async joinPresence(
    boardId: string,
    sessionId: string,
    displayName: string
  ): Promise<void> {
    try {
      const key = participantKey(displayName);
      const base = `presence/${boardId}/${key}`;
      await restPatch(base, { displayName, lastSeen: Date.now() });
      await restPut(`${base}/connections/${sessionId}`, true);
      const joinedAt = await restGet<number>(`${base}/joinedAt`);
      if (joinedAt === null) {
        await restPut(`${base}/joinedAt`, Date.now());
      }
    } catch (e) {
      console.warn('[retro] joinPresence failed:', e);
    }
  }

  async leavePresence(
    boardId: string,
    sessionId: string,
    displayName: string
  ): Promise<void> {
    try {
      const key = participantKey(displayName);
      await restDelete(`presence/${boardId}/${key}/connections/${sessionId}`);
    } catch {
      // best-effort
    }
  }

}

function applyPut(tree: unknown, path: string, data: unknown): unknown {
  if (path === '/' || path === '') return data;
  const parts = path.split('/').filter(Boolean);
  const root: Record<string, unknown> =
    tree && typeof tree === 'object' ? { ...(tree as Record<string, unknown>) } : {};
  let cursor: Record<string, unknown> = root;
  for (let i = 0; i < parts.length - 1; i++) {
    const seg = parts[i];
    const child = cursor[seg];
    cursor[seg] = child && typeof child === 'object' ? { ...(child as Record<string, unknown>) } : {};
    cursor = cursor[seg] as Record<string, unknown>;
  }
  const leaf = parts[parts.length - 1];
  if (data === null || data === undefined) {
    delete cursor[leaf];
  } else {
    cursor[leaf] = data;
  }
  return Object.keys(root).length === 0 ? null : root;
}

function applyPatch(tree: unknown, path: string, data: unknown): unknown {
  if (!data || typeof data !== 'object') return tree;
  let result = tree;
  for (const [key, value] of Object.entries(data as Record<string, unknown>)) {
    const childPath = path === '/' || path === '' ? `/${key}` : `${path}/${key}`;
    result = applyPut(result, childPath, value);
  }
  return result;
}

function streamObservable<T>(path: string): Observable<T | null> {
  return new Observable((subscriber) => {
    let current: unknown = null;
    let closed = false;
    const es = new EventSource(`${RTDB_REST_BASE}/${path}.json`);

    const emit = () => {
      if (!closed) subscriber.next(current as T | null);
    };

    es.addEventListener('put', (e: MessageEvent) => {
      try {
        const { path: p, data } = JSON.parse(e.data);
        current = applyPut(current, p, data);
        emit();
      } catch (err) {
        console.warn('[retro] SSE put parse failed', err);
      }
    });

    es.addEventListener('patch', (e: MessageEvent) => {
      try {
        const { path: p, data } = JSON.parse(e.data);
        current = applyPatch(current, p, data);
        emit();
      } catch (err) {
        console.warn('[retro] SSE patch parse failed', err);
      }
    });

    es.addEventListener('cancel', () => {
      if (!closed) subscriber.error(new Error('stream cancelled by server'));
    });

    es.onerror = () => {
      // EventSource auto-reconnects on transient errors; swallow
    };

    return () => {
      closed = true;
      es.close();
    };
  });
}
