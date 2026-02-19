import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  RetroBoardFirebaseService,
  slugify,
} from '../../services/retro-board-firebase.service';

@Component({
  selector: 'app-board-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './board-list.component.html',
})
export class BoardListComponent {
  private router = inject(Router);
  private retroService = inject(RetroBoardFirebaseService);

  boardName = '';
  joinCode = '';
  createError = '';
  joinError = '';

  async createBoard(): Promise<void> {
    this.createError = '';
    if (!this.boardName.trim()) return;
    try {
      const boardId = await this.retroService.createBoard(
        this.boardName.trim()
      );
      this.router.navigate(['/board', boardId]);
    } catch (e: any) {
      this.createError = e.message || 'Failed to create board.';
    }
  }

  async joinBoard(): Promise<void> {
    this.joinError = '';
    if (!this.joinCode.trim()) return;
    const slug = slugify(this.joinCode.trim());
    if (!slug) {
      this.joinError = 'Invalid board name.';
      return;
    }
    const exists = await this.retroService.boardExists(slug);
    if (!exists) {
      this.joinError = 'Board not found.';
      return;
    }
    this.router.navigate(['/board', slug]);
  }
}
