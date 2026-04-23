import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import {
  RetroBoardFirebaseService,
  slugify,
} from '../../services/retro-board-firebase.service';

@Component({
  selector: 'app-board-list',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatCardModule, MatIconModule],
  templateUrl: './board-list.component.html',
})
export class BoardListComponent {
  private router = inject(Router);
  private retroService = inject(RetroBoardFirebaseService);

  boardName = new FormControl('');
  joinCode = new FormControl('');
  importName = new FormControl('');
  importFile = signal<File | null>(null);
  createError = signal('');
  joinError = signal('');
  importError = signal('');
  importBusy = signal(false);

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

  onImportFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.importFile.set(input.files?.[0] ?? null);
    this.importError.set('');
  }

  async importBoard(): Promise<void> {
    this.importError.set('');
    const file = this.importFile();
    if (!file) {
      this.importError.set('Pick a JSON file first.');
      return;
    }
    this.importBusy.set(true);
    try {
      const raw = await file.text();
      const data = JSON.parse(raw) as {
        name?: string;
        columns?: string[];
        cards?: Array<{ text?: string; author?: string; columnIndex?: number; votes?: number }>;
      };

      if (!Array.isArray(data.columns) || data.columns.length === 0) {
        throw new Error('Invalid file: missing "columns" array.');
      }

      const name = this.importName.value?.trim() || data.name?.trim();
      if (!name) {
        throw new Error('Board name required (either in file or the name field).');
      }

      const cards = (data.cards ?? [])
        .map((c) => ({
          text: String(c?.text ?? '').trim(),
          author: String(c?.author ?? 'Anonymous'),
          columnIndex: Number.isFinite(c?.columnIndex) ? Number(c!.columnIndex) : 0,
          votes: Number.isFinite(c?.votes) ? Number(c!.votes) : 0,
        }))
        .filter((c) => c.text);

      const slug = await this.retroService.importBoard(name, data.columns, cards);
      this.router.navigate(['/board', slug]);
    } catch (e: any) {
      this.importError.set(e.message || 'Import failed.');
    } finally {
      this.importBusy.set(false);
    }
  }
}
