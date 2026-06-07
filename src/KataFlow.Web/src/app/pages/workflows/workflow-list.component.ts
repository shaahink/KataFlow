import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgFor, NgIf } from '@angular/common';
import { WorkflowService } from '../../services/workflow.service';
import { SessionService } from '../../services/session.service';
import { WorkflowSummary } from '../../models/workflow.model';
import { TopbarComponent } from '../../components/layout/topbar.component';

@Component({
  selector: 'app-workflow-list',
  standalone: true,
  imports: [RouterLink, NgFor, NgIf, TopbarComponent],
  template: `
    <app-topbar title="Workflows">
      <a routerLink="/workflows/new" class="px-3 py-1.5 bg-blue-600 text-white text-sm rounded hover:bg-blue-700">+ New</a>
    </app-topbar>
    <div class="p-6">
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4" *ngIf="workflows.length; else empty">
        <div *ngFor="let w of workflows" class="bg-white rounded-lg border hover:shadow-md transition cursor-pointer" (click)="edit(w.name)">
          <div class="p-4">
            <h3 class="font-semibold text-lg">{{ w.name }}</h3>
            <p class="text-sm text-gray-500 mt-1">{{ w.description || 'No description' }}</p>
          </div>
          <div class="border-t px-4 py-2 bg-gray-50 rounded-b-lg flex justify-end gap-2">
            <button (click)="run(w.name); $event.stopPropagation()" class="text-xs px-2 py-1 bg-green-600 text-white rounded hover:bg-green-700">Run</button>
          </div>
        </div>
      </div>
      <ng-template #empty><p class="text-gray-500">No workflows available.</p></ng-template>
    </div>
  `
})
export class WorkflowListComponent implements OnInit {
  workflows: WorkflowSummary[] = [];

  constructor(private wf: WorkflowService, private sess: SessionService) {}

  ngOnInit() { this.wf.list().subscribe(data => this.workflows = data); }

  edit(name: string) { window.location.href = `/workflows/${name}`; }

  run(name: string) {
    this.sess.start({ workflow: name, autoApprove: true }).subscribe((res: any) => {
      window.location.href = `/sessions/${res.sessionId}`;
    });
  }
}
