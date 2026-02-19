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
    path: 'board/:id',
    loadComponent: () =>
      import('./pages/board/board.component').then((m) => m.BoardComponent),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
