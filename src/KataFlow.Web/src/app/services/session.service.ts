import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import { SessionSummary, SessionDetail, StartRunRequest, StartRunResponse } from '../models/session.model';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private hubConnection?: signalR.HubConnection;
  private stepUpdateSubject = new Subject<any>();
  stepUpdates$ = this.stepUpdateSubject.asObservable();

  constructor(private http: HttpClient, private zone: NgZone) {}

  list(): Observable<SessionSummary[]> {
    return this.http.get<SessionSummary[]>('/api/sessions');
  }

  get(id: string): Observable<SessionDetail> {
    return this.http.get<SessionDetail>(`/api/sessions/${id}`);
  }

  getArtifact(sessionId: string, artifactName: string): Observable<{ name: string; content: string; path: string }> {
    return this.http.get<{ name: string; content: string; path: string }>(
      `/api/sessions/${sessionId}/artifacts/${artifactName}`);
  }

  approve(id: string, approve: boolean): Observable<{ sessionId: string; approved: boolean }> {
    return this.http.post<{ sessionId: string; approved: boolean }>(`/api/sessions/${id}/approve`, { approve });
  }

  start(req: StartRunRequest): Observable<StartRunResponse> {
    return this.http.post<StartRunResponse>('/api/runs', req);
  }

  async connectToSession(sessionId: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) return;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/session')
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('StepCompleted', (data: any) => {
      this.zone.run(() => this.stepUpdateSubject.next(data));
    });

    this.hubConnection.on('SessionCompleted', (data: any) => {
      this.zone.run(() => this.stepUpdateSubject.next(data));
    });

    this.hubConnection.on('SessionError', (data: any) => {
      this.zone.run(() => this.stepUpdateSubject.next(data));
    });

    await this.hubConnection.start();
    await this.hubConnection.invoke('JoinSession', sessionId);
  }

  async disconnectFromSession(sessionId: string): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.invoke('LeaveSession', sessionId);
      await this.hubConnection.stop();
      this.hubConnection = undefined;
    }
  }
}
