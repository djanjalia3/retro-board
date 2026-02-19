import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { RetroBoardFirebaseService } from '../../services/retro-board-firebase.service';
import { RetroBoard } from '../../models/retro-board.model';

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './board.component.html',
})
export class BoardComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private retroService = inject(RetroBoardFirebaseService);
  private subscription?: Subscription;

  board: RetroBoard | null = null;
  boardId = '';
  displayName = '';
  namePromptVisible = true;
  newCardTexts: string[] = ['', '', ''];
  postAnonymously: boolean[] = [false, false, false];
  sessionId = '';
  loadError = '';

  readonly columnColors = ['#16a34a', '#dc2626', '#2563eb'];

  ngOnInit(): void {
    this.sessionId = this.getOrCreateSessionId();

    const stored = sessionStorage.getItem('retro-display-name');
    if (stored) {
      this.displayName = stored;
      this.namePromptVisible = false;
    }

    this.boardId = this.route.snapshot.paramMap.get('id') ?? '';
    if (this.boardId) {
      this.subscription = this.retroService
        .observeBoard(this.boardId)
        .subscribe({
          next: (board) => {
            this.board = board;
          },
          error: (err) => {
            this.loadError = err?.message || 'Failed to load board';
          },
        });
    }
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  private getOrCreateSessionId(): string {
    let id = sessionStorage.getItem('retro-session-id');
    if (!id) {
      id = crypto.randomUUID();
      sessionStorage.setItem('retro-session-id', id);
    }
    return id;
  }

  setDisplayName(): void {
    if (!this.displayName.trim()) return;
    sessionStorage.setItem('retro-display-name', this.displayName.trim());
    this.namePromptVisible = false;
  }

  getCardsForColumn(
    columnIndex: number
  ): { id: string; text: string; author: string; votes: number; hasVoted: boolean }[] {
    if (!this.board?.cards) return [];
    return Object.entries(this.board.cards)
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
    const text = this.newCardTexts[columnIndex];
    if (!text?.trim()) return;
    const author = this.postAnonymously[columnIndex] ? 'Anonymous' : this.displayName;
    await this.retroService.addCard(this.boardId, {
      text: text.trim(),
      author,
      columnIndex,
      createdAt: Date.now(),
    });
    this.newCardTexts[columnIndex] = '';
  }

  vote(cardId: string): void {
    this.retroService.voteCard(this.boardId, cardId, this.sessionId);
  }

  async exportToExcel(): Promise<void> {
    if (!this.board) return;

    const XLSX = await import('xlsx');

    const columns = this.board.columns;
    const rows: Record<string, string>[] = [];

    const cardsByColumn: string[][][] = columns.map(() => [] as string[][]);

    if (this.board.cards) {
      for (const [, card] of Object.entries(this.board.cards)) {
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
    XLSX.writeFile(wb, `${this.board.name.replace(/[^a-zA-Z0-9]/g, '_')}_retro.xlsx`);
  }
}
