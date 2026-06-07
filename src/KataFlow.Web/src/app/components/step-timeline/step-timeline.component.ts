import { Component, Input } from '@angular/core';
import { SessionStep } from '../../models/session.model';
import { NgFor, NgIf } from '@angular/common';

@Component({
  selector: 'app-step-timeline',
  standalone: true,
  imports: [NgFor, NgIf],
  template: `
    <div class="space-y-0">
      <div *ngFor="let step of steps; let i = index" class="flex items-start gap-4">
        <div class="flex flex-col items-center">
          <div [class]="getCircleClass(step)"></div>
          <div *ngIf="i < steps.length - 1" class="w-0.5 h-8 bg-gray-300"></div>
        </div>
        <div class="flex-1 pb-4">
          <div class="flex items-center gap-2">
            <span class="font-medium text-sm">{{ step.stepName }}</span>
            <span [class]="getBadgeClass(step)">{{ step.status }}</span>
          </div>
          <p *ngIf="step.errorMessage" class="text-xs text-red-600 mt-1">{{ step.errorMessage }}</p>
          <p *ngIf="step.outputArtifactPath" class="text-xs text-gray-500 mt-0.5">{{ step.outputArtifactPath }}</p>
        </div>
      </div>
    </div>
  `
})
export class StepTimelineComponent {
  @Input() steps: SessionStep[] = [];

  getCircleClass(step: SessionStep): string {
    const base = 'w-4 h-4 rounded-full border-2 flex-shrink-0 mt-1';
    const colors: Record<string, string> = {
      complete: 'bg-green-500 border-green-600',
      failed: 'bg-red-500 border-red-600',
      running: 'bg-blue-500 border-blue-600 animate-pulse',
      'waiting-approval': 'bg-yellow-500 border-yellow-600',
      pending: 'bg-gray-200 border-gray-300',
    };
    return `${base} ${colors[step.status.toLowerCase()] || colors['pending']}`;
  }

  getBadgeClass(step: SessionStep): string {
    const base = 'text-xs px-2 py-0.5 rounded-full font-medium';
    const colors: Record<string, string> = {
      complete: 'bg-green-100 text-green-800',
      failed: 'bg-red-100 text-red-800',
      running: 'bg-blue-100 text-blue-800',
      'waiting-approval': 'bg-yellow-100 text-yellow-800',
      pending: 'bg-gray-100 text-gray-500',
    };
    return `${base} ${colors[step.status.toLowerCase()] || colors['pending']}`;
  }
}
