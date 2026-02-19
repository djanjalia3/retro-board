export interface RetroCard {
  text: string;
  author: string;
  columnIndex: number;
  createdAt: number;
  votes: number;
  voters?: { [sessionId: string]: boolean };
}

export interface RetroBoard {
  name: string;
  createdAt: number;
  columns: string[];
  cards?: { [cardId: string]: RetroCard };
}
