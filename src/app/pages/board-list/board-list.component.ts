import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { RetroBoardFirebaseService } from '../../services/retro-board-firebase.service';

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

  async createBoard(): Promise<void> {
    if (!this.boardName.trim()) return;
    const boardId = await this.retroService.createBoard(
      this.boardName.trim()
    );
    this.router.navigate(['/board', boardId]);
  }

  joinBoard(): void {
    if (!this.joinCode.trim()) return;
    this.router.navigate(['/board', this.joinCode.trim()]);
  }
}
