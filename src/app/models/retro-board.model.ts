export interface RetroCard {
  text: string;
  author: string;
  columnIndex: number;
  createdAt: number;
  votes: number;
  voters?: { [sessionId: string]: boolean };
}

export interface RetroParticipant {
  displayName: string;
  joinedAt: number;
  lastSeen?: number;
  connections?: { [sessionId: string]: true };
}

export interface RetroBoard {
  name: string;
  createdAt: number;
  columns: string[];
  cards?: { [cardId: string]: RetroCard };
  participants?: { [participantKey: string]: RetroParticipant };
}
