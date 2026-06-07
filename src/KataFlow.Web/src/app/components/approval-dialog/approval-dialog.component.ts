import { Component, Input, Output, EventEmitter } from '@angular/core';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-approval-dialog',
  standalone: true,
  imports: [NgIf],
  template: `
    <div class="fixed inset-0 bg-black/50 flex items-center justify-center z-50" *ngIf="visible" (click)="onCancel()">
      <div class="bg-white rounded-lg shadow-xl w-full max-w-lg mx-4 p-6" (click)="$event.stopPropagation()">
        <h3 class="text-lg font-semibold mb-2">Approve Step: {{ stepName }}</h3>
        <div class="bg-gray-50 rounded p-3 mb-4 max-h-48 overflow-y-auto text-sm">
          <pre class="whitespace-pre-wrap">{{ preview }}</pre>
        </div>
        <div class="flex gap-3 justify-end">
          <button (click)="onReject()" class="px-4 py-2 rounded border border-red-300 text-red-700 hover:bg-red-50">Reject</button>
          <button (click)="onApprove()" class="px-4 py-2 rounded bg-blue-600 text-white hover:bg-blue-700">Approve</button>
        </div>
      </div>
    </div>
  `
})
export class ApprovalDialogComponent {
  @Input() visible = false;
  @Input() stepName = '';
  @Input() preview = '';
  @Output() approve = new EventEmitter<void>();
  @Output() reject = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();

  onApprove() { this.approve.emit(); this.visible = false; }
  onReject() { this.reject.emit(); this.visible = false; }
  onCancel() { this.cancel.emit(); this.visible = false; }
}
