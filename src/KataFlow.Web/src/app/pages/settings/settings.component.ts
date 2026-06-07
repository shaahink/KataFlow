import { Component } from '@angular/core';
import { TopbarComponent } from '../../components/layout/topbar.component';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [TopbarComponent],
  template: `
    <app-topbar title="Settings"></app-topbar>
    <div class="p-6 max-w-2xl space-y-6">
      <div class="bg-white rounded-lg border p-4">
        <h3 class="font-semibold mb-2">Configuration</h3>
        <p class="text-sm text-gray-500 mb-3">Set API keys in <code class="bg-gray-100 px-1 rounded">.env</code> or user config at <code class="bg-gray-100 px-1 rounded">~/.kataflow/config.json</code></p>
        <div class="space-y-2 text-sm">
          <div><span class="font-medium">Workflows path:</span> <code class="bg-gray-100 px-1 rounded">./workflows</code></div>
          <div><span class="font-medium">Templates path:</span> <code class="bg-gray-100 px-1 rounded">./templates</code></div>
          <div><span class="font-medium">Sessions path:</span> <code class="bg-gray-100 px-1 rounded">./sessions</code></div>
        </div>
      </div>
      <div class="bg-white rounded-lg border p-4">
        <h3 class="font-semibold mb-2">About</h3>
        <p class="text-sm text-gray-500">KataFlow — Multi-agent AI workflow orchestrator</p>
        <p class="text-sm text-gray-500 mt-1">Version 1.0.0</p>
      </div>
    </div>
  `
})
export class SettingsComponent {}
