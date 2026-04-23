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
      const presenceRef = ref(this.db, `presence/${boardId}`);
      const unsubscribe = onValue(
        presenceRef,
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

  async joinPresence(
    boardId: string,
    sessionId: string,
    displayName: string
  ): Promise<void> {
    try {
      const key = participantKey(displayName);
      const base = `presence/${boardId}/${key}`;
      const participantRef = ref(this.db, base);
      const connectionRef = ref(this.db, `${base}/connections/${sessionId}`);
      const joinedAtRef = ref(this.db, `${base}/joinedAt`);

      onDisconnect(connectionRef).remove();

      await update(participantRef, {
        displayName,
        lastSeen: Date.now(),
      });
      await set(connectionRef, true);

      try {
        const snap = await get(joinedAtRef);
        if (!snap.exists()) {
          await set(joinedAtRef, Date.now());
        }
      } catch {
        // joinedAt read failed, skip
      }
    } catch (e) {
      console.warn('[retro] joinPresence failed:', e);
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
