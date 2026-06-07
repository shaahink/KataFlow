import { Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgFor, NgIf } from '@angular/common';
import { TemplateService } from '../../services/template.service';
import { MarkdownEditorComponent } from '../../components/markdown-editor/markdown-editor.component';
import { TopbarComponent } from '../../components/layout/topbar.component';

@Component({
  selector: 'app-template-editor',
  standalone: true,
  imports: [NgFor, NgIf, MarkdownEditorComponent, TopbarComponent],
  template: `
    <app-topbar [title]="'Edit: ' + templatePath">
      <button (click)="save()" class="px-3 py-1.5 bg-blue-600 text-white text-sm rounded hover:bg-blue-700">Save</button>
    </app-topbar>
    <div class="flex h-[calc(100vh-3.5rem)]">
      <div class="flex-1 p-4 overflow-auto">
        <app-markdown-editor #editor [value]="content" (valueChange)="onContentChange($event)"></app-markdown-editor>
      </div>
      <div class="w-80 border-l bg-gray-50 p-4 overflow-auto">
        <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Variables</h3>
        <div *ngIf="variables.length; else noVars" class="space-y-1">
          <div *ngFor="let v of variables" class="font-mono text-xs bg-white px-2 py-1 rounded border">
            <span>&#123;&#123;</span> {{ v }} <span>&#125;&#125;</span>
          </div>
        </div>
        <ng-template #noVars><p class="text-sm text-gray-500">No variables found.</p></ng-template>
        <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mt-6 mb-3">Preview</h3>
        <div class="bg-white rounded border p-3 text-sm whitespace-pre-wrap">{{ content }}</div>
      </div>
    </div>
  `
})
export class TemplateEditorComponent implements OnInit {
  @ViewChild('editor') editor!: MarkdownEditorComponent;
  templatePath = '';
  content = '';
  variables: string[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private ts: TemplateService
  ) {}

  ngOnInit() {
    const path = this.route.snapshot.paramMap.get('path');
    if (path) {
      this.templatePath = path;
      this.ts.get(path).subscribe(detail => {
        this.content = detail.content;
        this.variables = detail.variables;
      });
    }
  }

  onContentChange(val: string) {
    this.content = val;
    this.variables = [...new Set(val.match(/\{\{(\w+)\}\}/g)?.map(m => m.slice(2, -2)) || [])].sort();
  }

  save() {
    this.ts.update(this.templatePath, this.content).subscribe(() => this.router.navigate(['/templates']));
  }
}
