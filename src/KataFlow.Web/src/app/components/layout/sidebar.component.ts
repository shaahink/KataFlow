import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="w-64 bg-gray-900 text-white flex flex-col h-screen">
      <div class="p-4 border-b border-gray-700">
        <h1 class="text-xl font-bold">KataFlow</h1>
        <p class="text-xs text-gray-400">Workflow Orchestrator</p>
      </div>
      <nav class="flex-1 p-2 space-y-1">
        <a routerLink="/" routerLinkActive="bg-blue-600" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-gray-700 transition">
          <span>📊</span> Dashboard
        </a>
        <a routerLink="/workflows" routerLinkActive="bg-blue-600" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-gray-700 transition">
          <span>⚙️</span> Workflows
        </a>
        <a routerLink="/templates" routerLinkActive="bg-blue-600" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-gray-700 transition">
          <span>📝</span> Templates
        </a>
        <a routerLink="/sessions" routerLinkActive="bg-blue-600" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-gray-700 transition">
          <span>📋</span> Sessions
        </a>
      </nav>
      <div class="p-2 border-t border-gray-700">
        <a routerLink="/settings" routerLinkActive="bg-blue-600" class="flex items-center gap-3 px-3 py-2 rounded hover:bg-gray-700 transition">
          <span>⚙️</span> Settings
        </a>
      </div>
    </aside>
  `
})
export class SidebarComponent {}
