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

  readonly columnColors = ['#16a34a', '#dc2626', '#2563eb'];

  ngOnInit(): void {
    const stored = sessionStorage.getItem('retro-display-name');
    if (stored) {
      this.displayName = stored;
      this.namePromptVisible = false;
    }

    this.boardId = this.route.snapshot.paramMap.get('id') ?? '';
    if (this.boardId) {
      this.subscription = this.retroService
        .observeBoard(this.boardId)
        .subscribe((board) => {
          this.board = board;
        });
    }
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  setDisplayName(): void {
    if (!this.displayName.trim()) return;
    sessionStorage.setItem('retro-display-name', this.displayName.trim());
    this.namePromptVisible = false;
  }

  getCardsForColumn(
    columnIndex: number
  ): { id: string; text: string; author: string; votes: number }[] {
    if (!this.board?.cards) return [];
    return Object.entries(this.board.cards)
      .filter(([, card]) => card.columnIndex === columnIndex)
      .map(([id, card]) => ({
        id,
        text: card.text,
        author: card.author,
        votes: card.votes,
      }))
      .sort((a, b) => b.votes - a.votes);
  }

  async addCard(columnIndex: number): Promise<void> {
    const text = this.newCardTexts[columnIndex];
    if (!text?.trim()) return;
    await this.retroService.addCard(this.boardId, {
      text: text.trim(),
      author: this.displayName,
      columnIndex,
      createdAt: Date.now(),
    });
    this.newCardTexts[columnIndex] = '';
  }

  vote(cardId: string, currentVotes: number): void {
    this.retroService.voteCard(this.boardId, cardId, currentVotes);
  }
}
