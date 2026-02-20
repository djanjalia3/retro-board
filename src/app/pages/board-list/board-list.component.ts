import { Component, inject, ChangeDetectorRef } from '@angular/core';
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
  private cdr = inject(ChangeDetectorRef);

  boardName = new FormControl('');
  joinCode = new FormControl('');
  createError = '';
  joinError = '';

  async createBoard(): Promise<void> {
    this.createError = '';
    const name = this.boardName.value?.trim();
    if (!name) return;
    try {
      const boardId = await this.retroService.createBoard(name);
      this.router.navigate(['/board', boardId]);
    } catch (e: any) {
      this.createError = e.message || 'Failed to create board.';
      this.cdr.detectChanges();
    }
  }

  async joinBoard(): Promise<void> {
    this.joinError = '';
    const code = this.joinCode.value?.trim();
    if (!code) return;
    const slug = slugify(code);
    if (!slug) {
      this.joinError = 'Invalid board name.';
      return;
    }
    const exists = await this.retroService.boardExists(slug);
    if (!exists) {
      this.joinError = 'Board not found.';
      this.cdr.detectChanges();
      return;
    }
    this.router.navigate(['/board', slug]);
  }
}
