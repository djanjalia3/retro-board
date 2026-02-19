import { inject, Injectable } from '@angular/core';
import {
  Database,
  ref,
  push,
  set,
  get,
  update,
  onValue,
} from '@angular/fire/database';
import { Observable } from 'rxjs';
import { RetroBoard, RetroCard } from '../models/retro-board.model';

@Injectable({ providedIn: 'root' })
export class RetroBoardFirebaseService {
  private db = inject(Database);

  createBoard(name: string): Promise<string> {
    const boardsRef = ref(this.db, 'retro-boards');
    const newBoardRef = push(boardsRef);
    const board: RetroBoard = {
      name,
      createdAt: Date.now(),
      columns: ['What went well', "What didn't go well", 'Action items'],
    };
    return set(newBoardRef, board).then(() => newBoardRef.key as string);
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

  addCard(boardId: string, card: Omit<RetroCard, 'votes'>): Promise<void> {
    const cardsRef = ref(this.db, `retro-boards/${boardId}/cards`);
    const newCardRef = push(cardsRef);
    return set(newCardRef, { ...card, votes: 0 });
  }

  voteCard(
    boardId: string,
    cardId: string,
    currentVotes: number
  ): Promise<void> {
    const cardRef = ref(
      this.db,
      `retro-boards/${boardId}/cards/${cardId}`
    );
    return update(cardRef, { votes: currentVotes + 1 });
  }
}
