import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe, DatePipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MarkdownComponent } from 'ngx-markdown';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [FormsModule, DecimalPipe, DatePipe, NgClass, MarkdownComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private http = inject(HttpClient);
  
  // State
  currentView = signal<'assessment' | 'dashboard' | 'challenge' | 'quiz' | 'profile'>('assessment');
  userId = '11111111-1111-1111-1111-111111111111';
  
  // User Profile Data
  userProfile = signal<any>(null);

  // Assessment Form State
  dotnetLevel = signal<number>(1);
  angularLevel = signal<number>(1);
  architectureLevel = signal<number>(1);
  testingLevel = signal<number>(1);
  cloudLevel = signal<number>(1);
  assessmentStep = signal<number>(1);

  // Anzahl abgeschlossener Quizze, pro Browser in localStorage gespeichert.
  quizzesTaken = signal<number>(this.readQuizzesTaken());

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

  ngOnInit() {
    this.checkExistingPath();
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
      this.quizzesTaken.update(n => n + 1);
      try {
        localStorage.setItem('quizzesTaken', String(this.quizzesTaken()));
      } catch { /* localStorage nicht verfügbar */ }

      // XP für das Quiz vergeben lassen
      const before = this.xp();
      this.http.post<any>(`${environment.apiUrl}/api/quiz/complete`, {
        userId: this.userId,
        correct: this.quizScore(),
        total: this.quizQuestions().length
      }).subscribe({
        next: (res) => this.applyXp(res.points, before)
      });

      this.currentView.set('dashboard');
    }
  }

  private readQuizzesTaken(): number {
    try {
      const n = Number(localStorage.getItem('quizzesTaken'));
      return Number.isFinite(n) && n > 0 ? n : 0;
    } catch {
      return 0;
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

