import { inject, Injectable } from '@angular/core';
import { Database } from '@angular/fire/database';
import {
  ref,
  push,
  set,
  get,
  update,
  remove,
  onValue,
} from 'firebase/database';
import { Observable } from 'rxjs';

const RTDB_REST_BASE = 'https://retro-board-75e44-default-rtdb.firebaseio.com';
import { RetroBoard, RetroCard, RetroParticipant } from '../models/retro-board.model';

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

@Injectable({ providedIn: 'root' })
export class RetroBoardFirebaseService {
  private db = inject(Database);

  async createBoard(name: string): Promise<string> {
    const slug = slugify(name);
    if (!slug) throw new Error('Invalid board name.');
    const boardRef = ref(this.db, `retro-boards/${slug}`);
    const snapshot = await get(boardRef);
    if (snapshot.exists()) {
      throw new Error('Board name already taken, choose another.');
    }
    const board: RetroBoard = {
      name,
      createdAt: Date.now(),
      columns: ['What went well', "What didn't go well", 'Action items'],
    };
    await set(boardRef, board);
    return slug;
  }

  boardExists(slug: string): Promise<boolean> {
    const boardRef = ref(this.db, `retro-boards/${slug}`);
    return get(boardRef).then((snapshot) => snapshot.exists());
  }

  getBoard(boardId: string): Promise<RetroBoard | null> {
    const boardRef = ref(this.db, `retro-boards/${boardId}`);
    return get(boardRef).then((snapshot) =>
      snapshot.exists() ? snapshot.val() : null
    );
  }

  observeBoard(boardId: string): Observable<RetroBoard | null> {
    return new Observable((subscriber) => {
      const boardRef = ref(this.db, `retro-boards/${boardId}`);
      const unsubscribe = onValue(
        boardRef,
        (snapshot) => {
          subscriber.next(snapshot.exists() ? snapshot.val() : null);
        },
        (error) => {
          subscriber.error(error);
        }
      );
      return () => unsubscribe();
    });
  }

  addCard(boardId: string, card: Omit<RetroCard, 'votes' | 'voters'>): Promise<void> {
    const cardsRef = ref(this.db, `retro-boards/${boardId}/cards`);
    const newCardRef = push(cardsRef);
    return set(newCardRef, { ...card, votes: 0 });
  }

  deleteCard(boardId: string, cardId: string): Promise<void> {
    return remove(ref(this.db, `retro-boards/${boardId}/cards/${cardId}`));
  }

  observePresence(boardId: string): Observable<Record<string, RetroParticipant> | null> {
    return new Observable((subscriber) => {
      let cancelled = false;
      const poll = async () => {
        try {
          const res = await fetch(`${RTDB_REST_BASE}/presence/${boardId}.json`);
          if (cancelled) return;
          const data = (await res.json()) as Record<string, RetroParticipant> | null;
          if (!cancelled) subscriber.next(data);
        } catch {
          // swallow transient errors; next tick retries
        }
      };
      poll();
      const interval = setInterval(poll, 3000);
      return () => {
        cancelled = true;
        clearInterval(interval);
      };
    });
  }

  async joinPresence(
    boardId: string,
    sessionId: string,
    displayName: string
  ): Promise<void> {
    try {
      const key = participantKey(displayName);
      const base = `${RTDB_REST_BASE}/presence/${boardId}/${key}`;

      await fetch(`${base}.json`, {
        method: 'PATCH',
        body: JSON.stringify({ displayName, lastSeen: Date.now() }),
      });
      await fetch(`${base}/connections/${sessionId}.json`, {
        method: 'PUT',
        body: JSON.stringify(true),
      });

      const joinedAtRes = await fetch(`${base}/joinedAt.json`);
      const existing = await joinedAtRes.json();
      if (existing === null) {
        await fetch(`${base}/joinedAt.json`, {
          method: 'PUT',
          body: JSON.stringify(Date.now()),
        });
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
      await fetch(
        `${RTDB_REST_BASE}/presence/${boardId}/${key}/connections/${sessionId}.json`,
        { method: 'DELETE' }
      );
    } catch {
      // best-effort
    }
  }

  async voteCard(
    boardId: string,
    cardId: string,
    sessionId: string
  ): Promise<boolean> {
    const cardRef = ref(
      this.db,
      `retro-boards/${boardId}/cards/${cardId}`
    );
    const snapshot = await get(cardRef);
    if (!snapshot.exists()) return false;

    const card = snapshot.val() as RetroCard;
    if (card.voters && card.voters[sessionId]) {
      return false;
    }

    await update(cardRef, {
      votes: (card.votes || 0) + 1,
      [`voters/${sessionId}`]: true,
    });
    return true;
  }
}
