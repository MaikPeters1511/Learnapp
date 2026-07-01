import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { User } from '../models/user';

@Injectable({
  providedIn: 'root'
})
export class UserService {

  // Mock user data for demonstration
  private mockUser: User = {
    id: '12345-abcde-67890-fghij',
    name: 'Maik',
    email: 'maik@example.com',
    points: 1337,
    currentStreak: 5,
    lastActivityDate: new Date(),
    userAchievements: [
      { 
        id: 1, 
        userId: '12345-abcde-67890-fghij', 
        achievementId: 1, 
        awardedAt: new Date(),
        achievement: { id: 1, name: 'First Step', description: 'You completed your first challenge!', iconName: 'fa-shoe-prints', pointsAwarded: 10 }
      },
      { 
        id: 2, 
        userId: '12345-abcde-67890-fghij', 
        achievementId: 2, 
        awardedAt: new Date(),
        achievement: { id: 2, name: 'Quick Learner', description: 'Finish a module in under an hour.', iconName: 'fa-bolt', pointsAwarded: 50 }
      }
    ]
  };

  constructor() { }

  // This would typically make an HTTP call to '/api/user/me' or similar
  getUserProfile(): Observable<User> {
    // Replace with: return this.http.get<User>('/api/user/me');
    return of(this.mockUser);
  }
}
