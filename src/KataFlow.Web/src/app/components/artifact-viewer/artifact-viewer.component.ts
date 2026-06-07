import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-artifact-viewer',
  standalone: true,
  template: `
    <div class="bg-white border rounded-lg p-4">
      <h4 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-2">{{ title }}</h4>
      <div class="prose prose-sm max-w-none">
        <pre class="whitespace-pre-wrap font-mono text-sm bg-gray-50 p-3 rounded">{{ content }}</pre>
      </div>
    </div>
  `
})
export class ArtifactViewerComponent {
  @Input() title = 'Artifact';
  @Input() content = '';
}
