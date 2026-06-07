import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { TemplateDetail } from '../models/template.model';

@Injectable({ providedIn: 'root' })
export class TemplateService {
  constructor(private http: HttpClient) {}

  list(): Observable<string[]> {
    return this.http.get<string[]>('/api/templates');
  }

  get(path: string): Observable<TemplateDetail> {
    return this.http.get<TemplateDetail>(`/api/templates/${encodeURIComponent(path)}`);
  }

  update(path: string, content: string): Observable<{ path: string }> {
    return this.http.put<{ path: string }>(`/api/templates/${encodeURIComponent(path)}`, { content });
  }
}
