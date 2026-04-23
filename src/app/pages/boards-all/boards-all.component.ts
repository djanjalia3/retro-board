import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RetroBoardFirebaseService } from '../../services/retro-board-firebase.service';

interface BoardListItem {
  id: string;
  name: string;
  createdAt: number;
}

@Component({
  selector: 'app-boards-all',
  standalone: true,
  imports: [CommonModule, RouterLink, MatCardModule, MatButtonModule, MatIconModule, DatePipe],
  templateUrl: './boards-all.component.html',
})
export class BoardsAllComponent implements OnInit {
  private retroService = inject(RetroBoardFirebaseService);

  boards = signal<BoardListItem[] | null>(null);
  error = signal('');

  async ngOnInit(): Promise<void> {
    try {
      const list = await this.retroService.listBoards();
      this.boards.set(list);
    } catch (e: any) {
      this.error.set(e?.message || 'Failed to load boards.');
    }
  }
}
