import { inject, Injectable } from '@angular/core';
import { Database } from '@angular/fire/database';
import {
  ref,
  push,
  set,
  get,
  update,
  remove,
  onDisconnect,
  onValue,
} from 'firebase/database';
import { Observable } from 'rxjs';
import { RetroBoard, RetroCard } from '../models/retro-board.model';

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
          const val = snapshot.exists() ? snapshot.val() : null;
          console.log('[retro] onValue fired, cards:', val?.cards ? Object.keys(val.cards).length : 0);
          subscriber.next(val);
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

  async joinPresence(
    boardId: string,
    sessionId: string,
    displayName: string
  ): Promise<void> {
    const key = participantKey(displayName);
    const base = `retro-boards/${boardId}/participants/${key}`;
    const participantRef = ref(this.db, base);
    const connectionRef = ref(this.db, `${base}/connections/${sessionId}`);

    let joinedAt = Date.now();
    const cleanConnections: Record<string, true> = { [sessionId]: true };

    try {
      const snap = await get(participantRef);
      const existing = snap.val() as {
        joinedAt?: unknown;
        connections?: Record<string, unknown>;
      } | null;
      if (typeof existing?.joinedAt === 'number') {
        joinedAt = existing.joinedAt;
      }
      if (existing?.connections && typeof existing.connections === 'object') {
        for (const [sid, v] of Object.entries(existing.connections)) {
          if (v === true) cleanConnections[sid] = true;
        }
      }
    } catch (e) {
      console.warn('[retro] participant read failed, overwriting:', e);
    }

    await set(participantRef, {
      displayName,
      joinedAt,
      lastSeen: Date.now(),
      connections: cleanConnections,
    });

    onDisconnect(connectionRef).remove();
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
