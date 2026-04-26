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
