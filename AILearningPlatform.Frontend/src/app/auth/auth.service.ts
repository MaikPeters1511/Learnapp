import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthUser {
  id: string;
  name: string;
  email: string;
}

interface AuthResponse {
  token: string;
  expiresAt: string;
  user: AuthUser;
}

interface Session {
  token: string;
  /** Ablaufzeitpunkt in Millisekunden seit 1970. */
  expiresAt: number;
  user: AuthUser;
}

const STORAGE_KEY = 'ailp.session';

/**
 * Verwaltet die Anmeldung: Token und Benutzer liegen im localStorage, damit man nach einem
 * Neuladen angemeldet bleibt. Das Token läuft serverseitig nach 12 Stunden ab.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  private readonly session = signal<Session | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);

  constructor() {
    this.scheduleExpiry();
  }

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  register(name: string, email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/api/auth/register`, { name, email, password })
      .pipe(tap((response) => this.start(response)));
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/api/auth/login`, { email, password })
      .pipe(tap((response) => this.start(response)));
  }

  logout(): void {
    clearTimeout(this.expiryTimer);
    this.session.set(null);
    this.persist(null);
  }

  /** Übernimmt geänderte Stammdaten (z. B. nach dem Bearbeiten des Profils). */
  updateUser(patch: Partial<AuthUser>): void {
    const current = this.session();
    if (!current) return;
    const next = { ...current, user: { ...current.user, ...patch } };
    this.session.set(next);
    this.persist(next);
  }

  /**
   * Prüft nach einem Neuladen, ob das gespeicherte Token noch gilt. Bei 401 meldet der
   * Interceptor automatisch ab.
   */
  verify(): void {
    this.http.get<AuthUser>(`${environment.apiUrl}/api/auth/me`).subscribe({
      next: (user) => this.updateUser(user),
      error: () => { /* 401 behandelt der Interceptor, andere Fehler sind hier egal */ }
    });
  }

  private start(response: AuthResponse): void {
    const session: Session = {
      token: response.token,
      expiresAt: new Date(response.expiresAt).getTime(),
      user: response.user
    };
    this.session.set(session);
    this.persist(session);
    this.scheduleExpiry();
  }

  private scheduleExpiry(): void {
    clearTimeout(this.expiryTimer);
    const session = this.session();
    if (!session) return;
    const remaining = session.expiresAt - Date.now();
    // setTimeout verträgt höchstens ca. 24 Tage; unser Token lebt 12 Stunden.
    this.expiryTimer = setTimeout(() => this.logout(), Math.min(Math.max(remaining, 0), 2_000_000_000));
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const session = JSON.parse(raw) as Session;
      if (!session?.token || !session.user?.id || session.expiresAt <= Date.now()) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return session;
    } catch {
      return null; // localStorage nicht verfügbar oder beschädigt
    }
  }

  private persist(session: Session | null): void {
    try {
      if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* localStorage nicht verfügbar: Anmeldung gilt dann nur bis zum Neuladen */
    }
  }
}
