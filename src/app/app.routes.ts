import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/board-list/board-list.component').then(
        (m) => m.BoardListComponent
      ),
  },
  {
    path: 'boards',
    loadComponent: () =>
      import('./pages/boards-all/boards-all.component').then(
        (m) => m.BoardsAllComponent
      ),
  },
  {
    path: 'board/:id',
    loadComponent: () =>
      import('./pages/board/board.component').then((m) => m.BoardComponent),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
