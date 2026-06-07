import { Component, OnInit, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { NgIf } from '@angular/common';
import { WorkflowService } from '../../services/workflow.service';
import { YamlEditorComponent } from '../../components/yaml-editor/yaml-editor.component';
import { WorkflowGraphComponent } from '../../components/workflow-graph/workflow-graph.component';
import { TopbarComponent } from '../../components/layout/topbar.component';

@Component({
  selector: 'app-workflow-editor',
  standalone: true,
  imports: [NgIf, YamlEditorComponent, WorkflowGraphComponent, TopbarComponent],
  template: `
    <app-topbar [title]="isNew ? 'New Workflow' : 'Edit: ' + workflowName">
      <button (click)="save()" class="px-3 py-1.5 bg-blue-600 text-white text-sm rounded hover:bg-blue-700">Save</button>
      <button *ngIf="!isNew" (click)="delete()" class="px-3 py-1.5 border border-red-300 text-red-700 text-sm rounded hover:bg-red-50">Delete</button>
    </app-topbar>
    <div class="flex h-[calc(100vh-3.5rem)]">
      <div class="flex-1 p-4 overflow-auto">
        <app-yaml-editor #editor [value]="yaml" (valueChange)="onYamlChange($event)"></app-yaml-editor>
      </div>
      <div class="w-96 border-l bg-gray-50 p-4 overflow-auto">
        <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Graph Preview</h3>
        <app-workflow-graph [steps]="graphSteps"></app-workflow-graph>
      </div>
    </div>
  `
})
export class WorkflowEditorComponent implements OnInit {
  @ViewChild('editor') editor!: YamlEditorComponent;
  workflowName = '';
  isNew = false;
  yaml = '';
  graphSteps: { name: string }[] = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private wf: WorkflowService
  ) {}

  ngOnInit() {
    const name = this.route.snapshot.paramMap.get('name');
    if (name === 'new') {
      this.isNew = true;
      this.yaml = 'workflow:\n  name: my-workflow\n  description: ""\n  default_mode: dev\n  steps: []';
    } else if (name) {
      this.workflowName = name;
      this.wf.get(name).subscribe(detail => {
        this.yaml = detail.yaml;
        this.parseSteps(detail.yaml);
      });
    }
  }

  onYamlChange(val: string) {
    this.yaml = val;
    this.parseSteps(val);
  }

  private parseSteps(yaml: string) {
    try {
      const lines = yaml.split('\n');
      const steps: { name: string }[] = [];
      let inSteps = false;
      for (const line of lines) {
        if (line.trim().startsWith('steps:')) inSteps = true;
        else if (inSteps && /^\s+\w+/.test(line) && !line.trim().startsWith('-') && !line.includes(':')) inSteps = false;
        else if (inSteps && line.trim().startsWith('- name:')) {
          steps.push({ name: line.split(':')[1].trim() });
        }
      }
      this.graphSteps = steps;
    } catch {
      this.graphSteps = [];
    }
  }

  save() {
    if (this.isNew) {
      const name = this.extractName(this.yaml);
      this.wf.create({ name, yaml: this.yaml }).subscribe(() => this.router.navigate(['/workflows']));
    } else {
      this.wf.update(this.workflowName, { yaml: this.yaml }).subscribe(() => this.router.navigate(['/workflows']));
    }
  }

  delete() {
    this.wf.delete(this.workflowName).subscribe(() => this.router.navigate(['/workflows']));
  }

  private extractName(yaml: string): string {
    const m = yaml.match(/name:\s*(\S+)/);
    return m ? m[1] : 'unnamed';
  }
}
