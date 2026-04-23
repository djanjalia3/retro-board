import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import {
  RetroBoardFirebaseService,
  slugify,
} from '../../services/retro-board-firebase.service';

@Component({
  selector: 'app-board-list',
  standalone: true,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatCardModule],
  templateUrl: './board-list.component.html',
})
export class BoardListComponent {
  private router = inject(Router);
  private retroService = inject(RetroBoardFirebaseService);

  boardName = new FormControl('');
  joinCode = new FormControl('');
  createError = signal('');
  joinError = signal('');

  async createBoard(): Promise<void> {
    this.createError.set('');
    const name = this.boardName.value?.trim();
    if (!name) return;
    try {
      const boardId = await this.retroService.createBoard(name);
      this.router.navigate(['/board', boardId]);
    } catch (e: any) {
      this.createError.set(e.message || 'Failed to create board.');
    }
  }

  async joinBoard(): Promise<void> {
    this.joinError.set('');
    const code = this.joinCode.value?.trim();
    if (!code) return;
    const slug = slugify(code);
    if (!slug) {
      this.joinError.set('Invalid board name.');
      return;
    }
    const exists = await this.retroService.boardExists(slug);
    if (!exists) {
      this.joinError.set('Board not found.');
      return;
    }
    this.router.navigate(['/board', slug]);
  }
}
