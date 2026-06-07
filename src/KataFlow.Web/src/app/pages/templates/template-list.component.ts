import { Component, OnInit } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { TemplateService } from '../../services/template.service';
import { TopbarComponent } from '../../components/layout/topbar.component';

@Component({
  selector: 'app-template-list',
  standalone: true,
  imports: [NgFor, NgIf, TopbarComponent],
  template: `
    <app-topbar title="Templates"></app-topbar>
    <div class="p-6">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4" *ngIf="templates.length; else empty">
        <div *ngFor="let t of templates" class="bg-white rounded-lg border hover:shadow-md transition cursor-pointer p-4" (click)="edit(t)">
          <h3 class="font-semibold text-sm font-mono">{{ t }}</h3>
        </div>
      </div>
      <ng-template #empty><p class="text-gray-500">No templates found.</p></ng-template>
    </div>
  `
})
export class TemplateListComponent implements OnInit {
  templates: string[] = [];

  constructor(private ts: TemplateService) {}

  ngOnInit() { this.ts.list().subscribe(data => this.templates = data); }

  edit(path: string) { window.location.href = `/templates/${encodeURIComponent(path)}`; }
}
