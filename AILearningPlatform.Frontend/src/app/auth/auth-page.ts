import { Component, computed, inject, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { AuthService } from './auth.service';

type Mode = 'login' | 'register';

/** Login- und Registrierungsseite. Meldet über `authenticated`, wenn die Anmeldung geklappt hat. */
@Component({
  selector: 'app-auth-page',
  imports: [FormsModule],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css'
})
export class AuthPage {
  private readonly auth = inject(AuthService);

  readonly authenticated = output<void>();

  // Muss zu AuthEndpoints im ApiServer passen.
  readonly minPasswordLength = 8;
  readonly maxPasswordLength = 128;

  readonly mode = signal<Mode>('login');
  readonly name = signal('');
  readonly email = signal('');
  readonly password = signal('');
  readonly confirm = signal('');
  readonly showPassword = signal(false);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly error = signal('');

  readonly isRegister = computed(() => this.mode() === 'register');

  /** Passwortstärke von 0 (leer) bis 4. Nur eine Orientierung, der Server erzwingt nur die Länge. */
  readonly strength = computed(() => {
    const p = this.password();
    if (p.length === 0) return 0;
    if (p.length < this.minPasswordLength) return 1;

    let score = 1; // Länge erfüllt
    if (p.length >= 12) score++;
    if (/[a-z]/.test(p) && /[A-Z]/.test(p)) score++;
    if (/\d/.test(p)) score++;
    if (/[^A-Za-z0-9]/.test(p)) score++;
    return Math.max(1, Math.min(4, score - 1)); // 1 schwach ... 4 sehr stark
  });

  readonly strengthLabel = computed(
    () => ['', 'Zu schwach', 'Okay', 'Stark', 'Sehr stark'][this.strength()]
  );

  readonly passwordsMatch = computed(() => this.password() === this.confirm());

  readonly errors = computed(() => {
    const register = this.isRegister();
    const email = this.email().trim();
    const password = this.password();
    return {
      name: register && (this.name().trim().length < 1 || this.name().trim().length > 50)
        ? 'Bitte gib einen Namen mit 1 bis 50 Zeichen ein.' : '',
      email: !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 254
        ? 'Bitte gib eine gültige E-Mail-Adresse ein.' : '',
      password: register
        ? (password.length < this.minPasswordLength || password.length > this.maxPasswordLength
            ? `Das Passwort braucht ${this.minPasswordLength} bis ${this.maxPasswordLength} Zeichen.` : '')
        : (password.length === 0 ? 'Bitte gib dein Passwort ein.' : ''),
      confirm: register && !this.passwordsMatch() ? 'Die Passwörter stimmen nicht überein.' : ''
    };
  });

  setMode(mode: Mode): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    this.error.set('');
    this.submitted.set(false);
    this.confirm.set('');
    this.showPassword.set(false);
  }

  submit(): void {
    if (this.submitting()) return;
    this.submitted.set(true);
    this.error.set('');

    if (Object.values(this.errors()).some(Boolean)) return;

    this.submitting.set(true);
    const email = this.email().trim();
    const request$ = this.isRegister()
      ? this.auth.register(this.name().trim(), email, this.password())
      : this.auth.login(email, this.password());

    request$.subscribe({
      next: () => {
        this.submitting.set(false);
        this.password.set('');
        this.confirm.set('');
        this.authenticated.emit();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(this.messageFor(err));
      }
    });
  }

  private messageFor(err: HttpErrorResponse): string {
    if (err.status === 0) return 'Der Server ist nicht erreichbar. Bitte versuche es später erneut.';
    const serverMessage = err.error?.error;
    if (typeof serverMessage === 'string' && serverMessage) return serverMessage;
    if (err.status === 401) return 'E-Mail oder Passwort ist falsch.';
    if (err.status === 429) return 'Zu viele Versuche. Bitte warte eine Minute und versuche es dann erneut.';
    return 'Etwas ist schiefgelaufen. Bitte versuche es erneut.';
  }
}
