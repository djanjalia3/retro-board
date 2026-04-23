import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormArray, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { RetroBoardFirebaseService, participantKey } from '../../services/retro-board-firebase.service';
import { RetroBoard, RetroParticipant } from '../../models/retro-board.model';
import { ConfirmDialogComponent } from '../../components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatIconModule,
    MatDialogModule,
  ],
  templateUrl: './board.component.html',
})
export class BoardComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private retroService = inject(RetroBoardFirebaseService);
  private dialog = inject(MatDialog);
  private subscription?: Subscription;
  private presenceSubscription?: Subscription;

  board = signal<RetroBoard | null>(null);
  presence = signal<Record<string, RetroParticipant> | null>(null);
  boardId = '';
  displayNameControl = new FormControl('');
  displayName = signal('');
  namePromptVisible = signal(true);
  sessionId = '';
  loadError = signal('');

  newCardTexts = new FormArray<FormControl<string | null>>([]);
  postAnonymously = new FormArray<FormControl<boolean | null>>([]);

  readonly columnAccents = [
    '!border-t-emerald-500',
    '!border-t-rose-500',
    '!border-t-amber-500',
    '!border-t-sky-500',
  ];

  readonly participants = computed(() => {
    const map = this.presence();
    if (!map) return [];
    return Object.values(map)
      .map((p) => ({
        displayName: p.displayName,
        joinedAt: p.joinedAt ?? 0,
        online: !!p.connections && Object.keys(p.connections).length > 0,
      }))
      .sort((a, b) => {
        if (a.online !== b.online) return a.online ? -1 : 1;
        return a.joinedAt - b.joinedAt;
      });
  });

  ngOnInit(): void {
    this.sessionId = this.getOrCreateSessionId();

    const stored = sessionStorage.getItem('retro-display-name');
    if (stored) {
      this.displayName.set(stored);
      this.displayNameControl.setValue(stored);
      this.namePromptVisible.set(false);
    }

    this.boardId = this.route.snapshot.paramMap.get('id') ?? '';
    if (this.boardId) {
      this.subscription = this.retroService
        .observeBoard(this.boardId)
        .subscribe({
          next: (board) => {
            const cols = board?.columns?.length ?? 0;
            while (this.newCardTexts.length < cols) {
              this.newCardTexts.push(new FormControl(''));
              this.postAnonymously.push(new FormControl(false));
            }
            this.board.set(board);
          },
          error: (err) => {
            this.loadError.set(err?.message || 'Failed to load board');
          },
        });
      this.presenceSubscription = this.retroService
        .observePresence(this.boardId)
        .subscribe({
          next: (presence) => {
            this.presence.set(presence);
          },
        });
      if (this.displayName()) {
        this.joinPresence();
      }
      window.addEventListener('beforeunload', this.onBeforeUnload);
    }
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.presenceSubscription?.unsubscribe();
    const name = this.displayName();
    if (this.boardId && name) {
      this.retroService.leavePresence(this.boardId, this.sessionId, name);
    }
    window.removeEventListener('beforeunload', this.onBeforeUnload);
  }

  private onBeforeUnload = (): void => {
    const name = this.displayName();
    if (!this.boardId || !name) return;
    const url = `https://retro-board-75e44-default-rtdb.firebaseio.com/presence/${this.boardId}/${participantKey(name)}/connections/${this.sessionId}.json`;
    try {
      fetch(url, { method: 'DELETE', keepalive: true });
    } catch {
      // best-effort
    }
  };

  private getOrCreateSessionId(): string {
    let id = sessionStorage.getItem('retro-session-id');
    if (!id) {
      id = crypto.randomUUID();
      sessionStorage.setItem('retro-session-id', id);
    }
    return id;
  }

  setDisplayName(): void {
    const name = this.displayNameControl.value?.trim();
    if (!name) return;
    this.displayName.set(name);
    sessionStorage.setItem('retro-display-name', name);
    this.namePromptVisible.set(false);
    this.joinPresence();
  }

  private joinPresence(): void {
    const name = this.displayName();
    if (!this.boardId || !name) return;
    this.retroService.joinPresence(this.boardId, this.sessionId, name);
  }

  getCardsForColumn(
    columnIndex: number
  ): { id: string; text: string; author: string; votes: number; hasVoted: boolean }[] {
    const board = this.board();
    if (!board?.cards) return [];
    return Object.entries(board.cards)
      .filter(([, card]) => card.columnIndex === columnIndex)
      .map(([id, card]) => ({
        id,
        text: card.text,
        author: card.author,
        votes: card.votes,
        hasVoted: !!(card.voters && card.voters[this.sessionId]),
      }));
  }

  async addCard(columnIndex: number): Promise<void> {
    const control = this.newCardTexts.at(columnIndex);
    const text = control.value?.trim();
    if (!text) return;
    const isAnonymous = this.postAnonymously.at(columnIndex).value;
    const author = isAnonymous ? 'Anonymous' : this.displayName();
    await this.retroService.addCard(this.boardId, {
      text,
      author,
      columnIndex,
      createdAt: Date.now(),
    });
    control.reset('');
  }

  vote(cardId: string): void {
    this.retroService.voteCard(this.boardId, cardId, this.sessionId);
  }

  removeCard(cardId: string): void {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete card?',
        message: 'This will permanently remove the card for everyone.',
        confirmText: 'Delete',
      },
      width: '360px',
    });
    ref.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.retroService.deleteCard(this.boardId, cardId);
      }
    });
  }

  async exportToExcel(): Promise<void> {
    const board = this.board();
    if (!board) return;

    const XLSX = await import('xlsx');

    const columns = board.columns;
    const rows: Record<string, string>[] = [];

    const cardsByColumn: string[][][] = columns.map(() => [] as string[][]);

    if (board.cards) {
      for (const [, card] of Object.entries(board.cards)) {
        cardsByColumn[card.columnIndex].push([
          card.text,
          card.author,
          String(card.votes),
        ]);
      }
    }

    const maxRows = Math.max(...cardsByColumn.map((c) => c.length), 0);

    for (let r = 0; r < maxRows; r++) {
      const row: Record<string, string> = {};
      for (let c = 0; c < columns.length; c++) {
        const entry = cardsByColumn[c][r];
        row[`${columns[c]} - Card`] = entry ? entry[0] : '';
        row[`${columns[c]} - Author`] = entry ? entry[1] : '';
        row[`${columns[c]} - Votes`] = entry ? entry[2] : '';
      }
      rows.push(row);
    }

    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Retro Board');
    XLSX.writeFile(wb, `${board.name.replace(/[^a-zA-Z0-9]/g, '_')}_retro.xlsx`);
  }
}
