import { inject, Injectable, NgZone } from '@angular/core';
import {
  Database,
  ref,
  push,
  set,
  get,
  update,
} from '@angular/fire/database';
import { onValue } from 'firebase/database';
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

@Injectable({ providedIn: 'root' })
export class RetroBoardFirebaseService {
  private db = inject(Database);
  private ngZone = inject(NgZone);

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

      // Initial fetch to ensure data loads on refresh
      get(boardRef).then((snapshot) => {
        this.ngZone.run(() => {
          subscriber.next(snapshot.exists() ? snapshot.val() : null);
        });
      }).catch((err) => {
        this.ngZone.run(() => subscriber.error(err));
      });

      // Realtime updates for live changes
      const unsubscribe = onValue(
        boardRef,
        (snapshot) => {
          this.ngZone.run(() => {
            subscriber.next(snapshot.exists() ? snapshot.val() : null);
          });
        },
        (error) => {
          this.ngZone.run(() => {
            subscriber.error(error);
          });
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
