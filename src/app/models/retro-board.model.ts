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
