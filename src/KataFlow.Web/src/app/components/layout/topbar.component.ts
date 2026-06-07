import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-topbar',
  standalone: true,
  template: `
    <header class="h-14 bg-white border-b border-gray-200 flex items-center px-6 sticky top-0 z-10">
      <h2 class="text-lg font-semibold text-gray-800">{{ title }}</h2>
      <div class="ml-auto flex items-center gap-4">
        <ng-content></ng-content>
      </div>
    </header>
  `
})
export class TopbarComponent {
  @Input() title = '';
}
