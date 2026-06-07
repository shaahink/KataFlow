import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from './components/layout/sidebar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent],
  template: `
    <div class="flex h-screen bg-gray-100">
      <app-sidebar></app-sidebar>
      <main class="flex-1 flex flex-col overflow-hidden">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [':host { display: contents; }']
})
export class AppComponent {}
