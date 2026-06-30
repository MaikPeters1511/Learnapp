import { Component, inject, signal, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private http = inject(HttpClient);
  
  // State
  currentView = signal<'assessment' | 'dashboard' | 'challenge' | 'quiz'>('assessment');
  userId = '11111111-1111-1111-1111-111111111111';
  
  // Assessment Form State
  dotnetLevel = signal<number>(1);
  angularLevel = signal<number>(1);
  architectureLevel = signal<number>(1);
  testingLevel = signal<number>(1);
  cloudLevel = signal<number>(1);
  assessmentStep = signal<number>(1);

  // Learning Path Data
  learningPath = signal<any>(null);
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

  checkExistingPath() {
    this.loading.set(true);
    this.http.get(`http://localhost:5000/api/learningpath/${this.userId}`).subscribe({
      next: (path: any) => {
        this.learningPath.set(path);
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
    
    this.http.post('http://localhost:5000/api/assessment', body).subscribe({
      next: () => {
        this.checkExistingPath();
      },
      error: (err) => {
        console.error('Assessment failed', err);
        this.loading.set(false);
      }
    });
  }

  loadProjectSuggestions() {
    this.http.get<any[]>(`http://localhost:5000/api/projects/suggestions/${this.userId}`).subscribe({
      next: (projects) => {
        this.projectSuggestions.set(projects);
      }
    });
  }

  startQuiz(topic: string, level: number) {
    this.loading.set(true);
    this.quizTopic.set(topic);
    this.http.post<any[]>('http://localhost:5000/api/quiz/generate', { topic, level }).subscribe({
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

    this.http.post(`http://localhost:5000/api/challenges/${this.selectedChallenge().id}/submit`, body).subscribe({
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
      this.http.get(`http://localhost:5000/api/challenges/${this.selectedChallenge().id}/submissions/${this.userId}`).subscribe({
        next: (submissions: any) => {
          const latest = submissions.find((s: any) => s.id === submissionId);
          if (latest && latest.status !== 'Pending' && latest.status !== 'Evaluating') {
            this.submissionStatus.set(latest.status);
            this.submissionFeedback.set(latest.feedback);
            this.isSubmitting.set(false);
            clearInterval(interval);
            
            this.reloadLearningPath();
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
    this.http.get(`http://localhost:5000/api/learningpath/${this.userId}`).subscribe((path: any) => {
      this.learningPath.set(path);
    });
  }

  completeModule(module: any) {
    this.http.post(`http://localhost:5000/api/modules/${module.id}/complete`, {}).subscribe({
      next: () => {
        this.reloadLearningPath();
      }
    });
  }
}
