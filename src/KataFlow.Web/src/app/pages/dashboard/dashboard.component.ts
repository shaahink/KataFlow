import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgFor, NgIf, DatePipe, SlicePipe } from '@angular/common';
import { WorkflowService } from '../../services/workflow.service';
import { SessionService } from '../../services/session.service';
import { WorkflowSummary } from '../../models/workflow.model';
import { SessionSummary } from '../../models/session.model';
import { TopbarComponent } from '../../components/layout/topbar.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, NgFor, NgIf, DatePipe, SlicePipe, TopbarComponent],
  template: `
    <app-topbar title="Dashboard"></app-topbar>
    <div class="p-6 space-y-6">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div class="bg-white rounded-lg border p-4">
          <p class="text-sm text-gray-500">Workflows</p>
          <p class="text-3xl font-bold">{{ workflows.length }}</p>
        </div>
        <div class="bg-white rounded-lg border p-4">
          <p class="text-sm text-gray-500">Sessions</p>
          <p class="text-3xl font-bold">{{ sessions.length }}</p>
        </div>
        <div class="bg-white rounded-lg border p-4">
          <p class="text-sm text-gray-500">Active Runs</p>
          <p class="text-3xl font-bold">{{ activeCount }}</p>
        </div>
      </div>

      <div class="bg-white rounded-lg border">
        <div class="flex items-center justify-between px-4 py-3 border-b">
          <h3 class="font-semibold">Recent Sessions</h3>
          <a routerLink="/sessions" class="text-sm text-blue-600 hover:underline">View all</a>
        </div>
        <div class="p-4">
          <table class="w-full text-sm" *ngIf="sessions.length; else noSessions">
            <thead><tr class="text-left text-gray-500"><th class="pb-2">Session</th><th class="pb-2">Workflow</th><th class="pb-2">Status</th><th class="pb-2">Created</th></tr></thead>
            <tbody>
              <tr *ngFor="let s of sessions | slice:0:5" class="border-t">
                <td class="py-2 font-mono text-xs">{{ s.id }}</td>
                <td class="py-2">{{ s.workflowName }}</td>
                <td class="py-2"><span class="text-xs px-2 py-0.5 rounded-full" [class]="statusColor(s.status)">{{ s.status }}</span></td>
                <td class="py-2 text-gray-500">{{ s.createdAt | date:'short' }}</td>
              </tr>
            </tbody>
          </table>
          <ng-template #noSessions><p class="text-gray-500 text-sm">No sessions yet.</p></ng-template>
        </div>
      </div>
    </div>
  `
})
export class DashboardComponent implements OnInit {
  workflows: WorkflowSummary[] = [];
  sessions: SessionSummary[] = [];
  activeCount = 0;

  constructor(private wf: WorkflowService, private sess: SessionService) {}

  ngOnInit() {
    this.wf.list().subscribe(data => this.workflows = data);
    this.sess.list().subscribe(data => {
      this.sessions = data;
      this.activeCount = data.filter(s => s.status === 'Running' || s.status === 'WaitingApproval').length;
    });
  }

  statusColor(s: string) {
    const colors: Record<string, string> = { Running: 'bg-blue-100 text-blue-800', Complete: 'bg-green-100 text-green-800', Failed: 'bg-red-100 text-red-800' };
    return colors[s] || 'bg-gray-100 text-gray-500';
  }
}
