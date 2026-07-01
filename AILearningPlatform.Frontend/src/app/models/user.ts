import { UserAchievement } from './user-achievement';

export interface User {
  id: string; // Guid
  name: string;
  email: string;
  
  // Gamification properties
  points: number;
  currentStreak: number;
  lastActivityDate: Date;
  userAchievements: UserAchievement[];

  // Other existing properties
  // skillProfile?: any; 
  // learningPaths?: any[];
}
