import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NgIf, NgFor, DecimalPipe } from '@angular/common';
import { SessionService } from '../../services/session.service';
import { SessionDetail, SessionStep, BudgetStep } from '../../models/session.model';
import { WorkflowGraphComponent } from '../../components/workflow-graph/workflow-graph.component';
import { StepTimelineComponent } from '../../components/step-timeline/step-timeline.component';
import { ArtifactViewerComponent } from '../../components/artifact-viewer/artifact-viewer.component';
import { TopbarComponent } from '../../components/layout/topbar.component';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-session-detail',
  standalone: true,
  imports: [NgIf, NgFor, DecimalPipe, WorkflowGraphComponent, StepTimelineComponent, ArtifactViewerComponent, TopbarComponent],
  template: `
    <app-topbar [title]="'Session: ' + sessionId">
      <span class="text-sm px-2 py-0.5 rounded-full font-medium" [class]="statusBadge">{{ detail?.status }}</span>
    </app-topbar>
    <div class="p-6 space-y-6" *ngIf="detail">
      <app-workflow-graph [steps]="graphSteps"></app-workflow-graph>

      <div *ngIf="detail.budget" class="bg-white border rounded-lg p-4">
        <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Budget</h3>
        <div class="grid grid-cols-3 gap-4 mb-4">
          <div class="text-center">
            <div class="text-2xl font-bold text-gray-900">\${{ detail.budget.totalCostUsd | number:'1.4-4' }}</div>
            <div class="text-xs text-gray-500">Total Cost</div>
          </div>
          <div class="text-center">
            <div class="text-2xl font-bold text-gray-900">{{ detail.budget.totalInputTokens | number }}</div>
            <div class="text-xs text-gray-500">Input Tokens</div>
          </div>
          <div class="text-center">
            <div class="text-2xl font-bold text-gray-900">{{ detail.budget.totalOutputTokens | number }}</div>
            <div class="text-xs text-gray-500">Output Tokens</div>
          </div>
        </div>
        <table class="w-full text-sm text-left" *ngIf="detail.budget.steps.length">
          <thead class="text-xs text-gray-500 uppercase border-b">
            <tr>
              <th class="py-2">Step</th>
              <th class="py-2">Model</th>
              <th class="py-2 text-right">Input Tokens</th>
              <th class="py-2 text-right">Output Tokens</th>
              <th class="py-2 text-right">Cost</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let b of detail.budget.steps" class="border-b border-gray-100">
              <td class="py-1.5">{{ b.stepName }}</td>
              <td class="py-1.5 text-gray-500">{{ b.model }}</td>
              <td class="py-1.5 text-right">{{ b.inputTokens | number }}</td>
              <td class="py-1.5 text-right">{{ b.outputTokens | number }}</td>
              <td class="py-1.5 text-right text-yellow-600">\${{ b.costUsd | number:'1.4-4' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2">
          <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Steps</h3>
          <app-step-timeline [steps]="detail.steps"></app-step-timeline>
        </div>
        <div>
          <h3 class="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Artifacts</h3>
          <div *ngIf="detail.artifacts.length; else noArtifacts" class="space-y-3">
            <app-artifact-viewer *ngFor="let a of detail.artifacts" [title]="a.name" [content]="a.content ?? ''"></app-artifact-viewer>
          </div>
          <ng-template #noArtifacts><p class="text-sm text-gray-500">No artifacts yet.</p></ng-template>
        </div>
      </div>

      <div *ngIf="currentStep?.status === 'WaitingApproval' || currentStep?.status === 'waiting-approval'" class="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
        <p class="text-sm font-medium text-yellow-800">Approval required for step: {{ currentStep?.stepName }}</p>
        <div class="flex gap-3 mt-3">
          <button (click)="approve(true)" class="px-4 py-2 rounded bg-blue-600 text-white text-sm hover:bg-blue-700">Approve</button>
          <button (click)="approve(false)" class="px-4 py-2 rounded border border-red-300 text-red-700 text-sm hover:bg-red-50">Reject</button>
        </div>
      </div>
    </div>
  `
})
export class SessionDetailComponent implements OnInit, OnDestroy {
  sessionId = '';
  detail: SessionDetail | null = null;

  constructor(private route: ActivatedRoute, private sess: SessionService) {}

  get graphSteps(): { name: string; status: string }[] {
    return this.detail?.steps?.map(s => ({ name: s.stepName, status: s.status })) || [];
  }

  get currentStep(): SessionStep | null {
    const idx = this.detail?.currentStepIndex ?? 0;
    return this.detail?.steps?.[idx] || null;
  }

  get statusBadge(): string {
    if (!this.detail) return 'bg-gray-100 text-gray-500';
    const colors: Record<string, string> = {
      Complete: 'bg-green-100 text-green-800', Failed: 'bg-red-100 text-red-800',
      Running: 'bg-blue-100 text-blue-800', WaitingApproval: 'bg-yellow-100 text-yellow-800'
    };
    return colors[this.detail.status] || 'bg-gray-100 text-gray-500';
  }

  async ngOnInit() {
    this.sessionId = this.route.snapshot.paramMap.get('id') || '';
    if (this.sessionId) {
      await this.loadDetail();
      await this.loadArtifacts();
      this.sess.connectToSession(this.sessionId);
      this.sess.stepUpdates$.subscribe(async () => {
        await this.loadDetail();
        await this.loadArtifacts();
      });
    }
  }

  ngOnDestroy() {
    if (this.sessionId) this.sess.disconnectFromSession(this.sessionId);
  }

  approve(approved: boolean) {
    this.sess.approve(this.sessionId, approved).subscribe(async () => {
      await this.loadDetail();
    });
  }

  private async loadDetail() {
    this.detail = await firstValueFrom(this.sess.get(this.sessionId));
  }

  private async loadArtifacts() {
    if (!this.detail?.artifacts) return;
    for (const artifact of this.detail.artifacts) {
      try {
        const resp = await firstValueFrom(
          this.sess.getArtifact(this.sessionId, artifact.name));
        artifact.content = resp.content;
      } catch { }
    }
  }
}
