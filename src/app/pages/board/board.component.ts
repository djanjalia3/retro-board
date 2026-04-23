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

  exportToJson(): void {
    const board = this.board();
    if (!board) return;

    const cards = Object.values(board.cards ?? {}).map((c) => ({
      text: c.text,
      author: c.author,
      columnIndex: c.columnIndex,
      votes: c.votes ?? 0,
    }));

    const payload = {
      name: board.name,
      columns: board.columns,
      cards,
      exportedAt: Date.now(),
      version: 1,
    };

    const blob = new Blob([JSON.stringify(payload, null, 2)], {
      type: 'application/json',
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${board.name.replace(/[^a-zA-Z0-9]/g, '_')}_retro.json`;
    a.click();
    URL.revokeObjectURL(url);
  }
}
