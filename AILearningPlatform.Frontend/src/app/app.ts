import { Component, computed, effect, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe, DatePipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MarkdownComponent } from 'ngx-markdown';
import { environment } from '../environments/environment';
import { AuthPage } from './auth/auth-page';
import { AuthService } from './auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule, DecimalPipe, DatePipe, NgClass, MarkdownComponent, AuthPage],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private http = inject(HttpClient);
  readonly auth = inject(AuthService);
  
  // State
  currentView = signal<'assessment' | 'dashboard' | 'challenge' | 'quiz' | 'profile'>('assessment');
  // Die ID des angemeldeten Benutzers (kommt aus der Anmeldung, nicht mehr fest im Code).
  get userId(): string {
    return this.auth.user()?.id ?? '';
  }

  // Kürzel für den Avatar in der Navigationsleiste, z. B. "JD" für "John Doe".
  initials = computed(() => {
    const parts = (this.auth.user()?.name ?? '').trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    const first = parts[0][0] ?? '';
    const last = parts.length > 1 ? parts[parts.length - 1][0] : '';
    return (first + last).toUpperCase();
  });
  
  // User Profile Data
  userProfile = signal<any>(null);

  // Assessment Form State
  dotnetLevel = signal<number>(1);
  angularLevel = signal<number>(1);
  architectureLevel = signal<number>(1);
  testingLevel = signal<number>(1);
  cloudLevel = signal<number>(1);
  assessmentStep = signal<number>(1);

  // Anzahl abgeschlossener Quizze, serverseitig gespeichert.
  quizzesTaken = signal<number>(0);

  // Learning Path Data
  learningPath = signal<any>(null);

  // Abgeleitete Dashboard-Werte (statt fest eingetragener Zahlen)
  totalModules = computed(() => this.learningPath()?.modules?.length ?? 0);
  completedModules = computed(
    () => (this.learningPath()?.modules ?? []).filter((m: any) => m.isCompleted).length
  );
  totalChallenges = computed(
    () => (this.learningPath()?.modules ?? []).reduce((sum: number, m: any) => sum + (m.challenges?.length ?? 0), 0)
  );
  progressPercent = computed(() =>
    this.totalModules() === 0 ? 0 : Math.round((this.completedModules() / this.totalModules()) * 100)
  );

  // XP und Level (alle 100 XP ein Level)
  readonly xpPerLevel = 100;
  xp = signal<number>(0);
  xpToast = signal<string>('');
  level = computed(() => Math.floor(this.xp() / this.xpPerLevel) + 1);
  xpIntoLevel = computed(() => this.xp() % this.xpPerLevel);
  private xpToastTimer: ReturnType<typeof setTimeout> | undefined;
  selectedChallenge = signal<any>(null);
  
  // Project Suggestions State
  projectSuggestions = signal<any[]>([]);
  
  // Quiz State
  quizQuestions = signal<any[]>([]);
  quizTopic = signal<string>('');
  currentQuestionIndex = signal<number>(0);
  selectedQuizOption = signal<number | null>(null);
  quizScore = signal<number>(0);
  quizSubmitted = signal<boolean>(false);

  // Submission State
  challengeCode = signal<string>('');
  submissionStatus = signal<string>('');
  submissionFeedback = signal<string>('');
  isSubmitting = signal<boolean>(false);
  
  // General
  loading = signal<boolean>(false);

  constructor() {
    // Wird die Anmeldung beendet (Abmelden, Token abgelaufen), bleiben keine Daten des Benutzers im Speicher.
    effect(() => {
      if (!this.auth.isLoggedIn()) {
        this.resetState();
      }
    });
  }

  ngOnInit() {
    if (this.auth.isLoggedIn()) {
      this.auth.verify();
      this.checkExistingPath();
    }
  }

  /** Nach erfolgreicher Anmeldung oder Registrierung: Daten des Benutzers laden. */
  onAuthenticated() {
    this.resetState();
    this.checkExistingPath();
  }

  logout() {
    this.auth.logout();
  }

  private resetState() {
    this.currentView.set('assessment');
    this.userProfile.set(null);
    this.learningPath.set(null);
    this.projectSuggestions.set([]);
    this.dotnetLevel.set(1);
    this.angularLevel.set(1);
    this.architectureLevel.set(1);
    this.testingLevel.set(1);
    this.cloudLevel.set(1);
    this.assessmentStep.set(1);
    this.xp.set(0);
    this.xpToast.set('');
    this.quizzesTaken.set(0);
    this.selectedChallenge.set(null);
    this.quizQuestions.set([]);
    this.quizTopic.set('');
    this.currentQuestionIndex.set(0);
    this.selectedQuizOption.set(null);
    this.quizScore.set(0);
    this.quizSubmitted.set(false);
    this.challengeCode.set('');
    this.submissionStatus.set('');
    this.submissionFeedback.set('');
    this.isSubmitting.set(false);
    this.loading.set(false);
    this.editingProfile.set(false);
    this.profileSaving.set(false);
    this.profileError.set('');
  }

  showProfile() {
    this.currentView.set('profile');
    // Immer frisch laden, damit XP, Streak und Abzeichen aktuell sind.
    this.http.get<any>(`${environment.apiUrl}/api/users/${this.userId}/profile`).subscribe({
      next: (profile) => {
        this.userProfile.set(profile);
        this.xp.set(profile.points ?? 0);
      }
    });
  }

  // Profil bearbeiten (Name und E-Mail)
  editingProfile = signal<boolean>(false);
  profileSaving = signal<boolean>(false);
  profileError = signal<string>('');
  editName = signal<string>('');
  editEmail = signal<string>('');

  startEditProfile() {
    const p = this.userProfile();
    if (!p) return;
    this.editName.set(p.name ?? '');
    this.editEmail.set(p.email ?? '');
    this.profileError.set('');
    this.editingProfile.set(true);
  }

  cancelEditProfile() {
    this.editingProfile.set(false);
    this.profileError.set('');
  }

  saveProfile() {
    this.profileSaving.set(true);
    this.profileError.set('');
    this.http.put<any>(`${environment.apiUrl}/api/users/${this.userId}/profile`, {
      name: this.editName(),
      email: this.editEmail()
    }).subscribe({
      next: (res) => {
        this.userProfile.update(p => ({ ...p, name: res.name, email: res.email }));
        this.auth.updateUser({ name: res.name, email: res.email });
        this.editingProfile.set(false);
        this.profileSaving.set(false);
      },
      error: (err) => {
        this.profileError.set(err?.error?.error ?? 'Speichern fehlgeschlagen. Bitte versuche es erneut.');
        this.profileSaving.set(false);
      }
    });
  }

  checkExistingPath() {
    this.loading.set(true);
    this.http.get(`${environment.apiUrl}/api/learningpath/${this.userId}`).subscribe({
      next: (path: any) => {
        this.learningPath.set(path);
        this.loadSkillProfile();
        this.loadProjectSuggestions();
        this.currentView.set('dashboard');
        this.loading.set(false);
      },
      error: () => {
        this.currentView.set('assessment');
        this.loading.set(false);
      }
    });
  }

  submitAssessment() {
    this.loading.set(true);
    const body = {
      userId: this.userId,
      dotnetLevel: this.dotnetLevel(),
      angularLevel: this.angularLevel(),
      architectureLevel: this.architectureLevel(),
      testingLevel: this.testingLevel(),
      cloudLevel: this.cloudLevel()
    };
    
    this.http.post(`${environment.apiUrl}/api/assessment`, body).subscribe({
      next: () => {
        this.checkExistingPath();
      },
      error: (err) => {
        console.error('Assessment failed', err);
        this.loading.set(false);
      }
    });
  }

  // Gespeicherte Einstufung laden, damit das Dashboard nicht bei 1/10 stehen bleibt.
  loadSkillProfile() {
    this.http.get<any>(`${environment.apiUrl}/api/users/${this.userId}`).subscribe({
      next: (user) => {
        this.xp.set(user?.points ?? 0);
        this.quizzesTaken.set(user?.quizzesCompleted ?? 0);
        const p = user?.skillProfile;
        if (!p) return;
        this.dotnetLevel.set(p.dotNetLevel ?? 1);
        this.angularLevel.set(p.angularLevel ?? 1);
        this.architectureLevel.set(p.architectureLevel ?? 1);
        this.testingLevel.set(p.testingLevel ?? 1);
        this.cloudLevel.set(p.cloudLevel ?? 1);
      }
    });
  }

  // Holt den XP-Stand vom Backend und zeigt einen Hinweis, wenn XP dazugekommen sind.
  refreshXp() {
    const before = this.xp();
    this.http.get<any>(`${environment.apiUrl}/api/users/${this.userId}`).subscribe({
      next: (user) => this.applyXp(user?.points ?? 0, before)
    });
  }

  private applyXp(points: number, before: number) {
    this.xp.set(points);
    const gained = points - before;
    if (gained > 0) {
      this.xpToast.set(`+${gained} XP`);
      clearTimeout(this.xpToastTimer);
      this.xpToastTimer = setTimeout(() => this.xpToast.set(''), 4000);
    }
  }

  loadProjectSuggestions() {
    this.http.get<any[]>(`${environment.apiUrl}/api/projects/suggestions/${this.userId}`).subscribe({
      next: (projects) => {
        this.projectSuggestions.set(projects);
      }
    });
  }

  startQuiz(topic: string, level: number) {
    this.loading.set(true);
    this.quizTopic.set(topic);
    this.http.post<any[]>(`${environment.apiUrl}/api/quiz/generate`, { topic, level }).subscribe({
      next: (questions) => {
        this.quizQuestions.set(questions);
        this.currentQuestionIndex.set(0);
        this.selectedQuizOption.set(null);
        this.quizScore.set(0);
        this.quizSubmitted.set(false);
        this.currentView.set('quiz');
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  submitQuizAnswer() {
    if (this.selectedQuizOption() === null) return;

    const currentQuestion = this.quizQuestions()[this.currentQuestionIndex()];
    if (this.selectedQuizOption() === currentQuestion.correctOptionIndex) {
      this.quizScore.set(this.quizScore() + 1);
    }
    
    this.quizSubmitted.set(true);
  }

  nextQuizQuestion() {
    if (this.currentQuestionIndex() < this.quizQuestions().length - 1) {
      this.currentQuestionIndex.set(this.currentQuestionIndex() + 1);
      this.selectedQuizOption.set(null);
      this.quizSubmitted.set(false);
    } else {
      // Completed, redirect to dashboard
      // Quiz-Ergebnis melden: das Backend vergibt XP und zählt das Quiz.
      const before = this.xp();
      this.http.post<any>(`${environment.apiUrl}/api/quiz/complete`, {
        userId: this.userId,
        correct: this.quizScore(),
        total: this.quizQuestions().length
      }).subscribe({
        next: (res) => {
          this.applyXp(res.points, before);
          this.quizzesTaken.set(res.quizzesCompleted ?? this.quizzesTaken());
        }
      });

      this.currentView.set('dashboard');
    }
  }

  selectChallenge(challenge: any) {
    this.selectedChallenge.set(challenge);
    this.challengeCode.set(challenge.startingCode);
    this.submissionStatus.set('');
    this.submissionFeedback.set('');
    this.currentView.set('challenge');
  }

  submitChallenge() {
    this.isSubmitting.set(true);
    this.submissionStatus.set('Evaluating');
    this.submissionFeedback.set('Code wird vom EvaluationWorker bewertet...');

    const body = {
      userId: this.userId,
      code: this.challengeCode()
    };

    this.http.post(`${environment.apiUrl}/api/challenges/${this.selectedChallenge().id}/submit`, body).subscribe({
      next: (submission: any) => {
        this.pollSubmissionResult(submission.id);
      },
      error: (err) => {
        this.submissionStatus.set('Error');
        this.submissionFeedback.set('Fehler beim Einreichen der Aufgabe.');
        this.isSubmitting.set(false);
      }
    });
  }

  pollSubmissionResult(submissionId: string) {
    const interval = setInterval(() => {
      this.http.get(`${environment.apiUrl}/api/challenges/${this.selectedChallenge().id}/submissions/${this.userId}`).subscribe({
        next: (submissions: any) => {
          const latest = submissions.find((s: any) => s.id === submissionId);
          if (latest && latest.status !== 'Pending' && latest.status !== 'Evaluating') {
            this.submissionStatus.set(latest.status);
            this.submissionFeedback.set(latest.feedback);
            this.isSubmitting.set(false);
            clearInterval(interval);

            this.reloadLearningPath();
            this.refreshXp();
          }
        },
        error: (err) => {
          console.error(err);
          clearInterval(interval);
          this.isSubmitting.set(false);
        }
      });
    }, 2000);
  }

  reloadLearningPath() {
    this.http.get(`${environment.apiUrl}/api/learningpath/${this.userId}`).subscribe((path: any) => {
      this.learningPath.set(path);
    });
  }

  completeModule(module: any) {
    this.http.post(`${environment.apiUrl}/api/modules/${module.id}/complete`, {}).subscribe({
      next: () => {
        this.reloadLearningPath();
        this.refreshXp();
      }
    });
  }
}

