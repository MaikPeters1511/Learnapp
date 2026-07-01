import { Achievement } from './achievement';

export interface UserAchievement {
  id: number;
  userId: string; // Guid is string in TS
  achievementId: number;
  achievement: Achievement;
  awardedAt: Date;
}
