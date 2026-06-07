import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { WorkflowSummary, WorkflowDetail, CreateWorkflowRequest, UpdateWorkflowRequest } from '../models/workflow.model';

@Injectable({ providedIn: 'root' })
export class WorkflowService {
  constructor(private http: HttpClient) {}

  list(): Observable<WorkflowSummary[]> {
    return this.http.get<WorkflowSummary[]>('/api/workflows');
  }

  get(name: string): Observable<WorkflowDetail> {
    return this.http.get<WorkflowDetail>(`/api/workflows/${encodeURIComponent(name)}`);
  }

  create(req: CreateWorkflowRequest): Observable<{ name: string }> {
    return this.http.post<{ name: string }>('/api/workflows', req);
  }

  update(name: string, req: UpdateWorkflowRequest): Observable<{ name: string }> {
    return this.http.put<{ name: string }>(`/api/workflows/${encodeURIComponent(name)}`, req);
  }

  delete(name: string): Observable<void> {
    return this.http.delete<void>(`/api/workflows/${encodeURIComponent(name)}`);
  }
}
