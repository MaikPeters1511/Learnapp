using Microsoft.EntityFrameworkCore;
using AILearningPlatform.Data.Models;

namespace AILearningPlatform.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<SkillProfile> SkillProfiles => Set<SkillProfile>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<User>()
            .HasOne(u => u.SkillProfile)
            .WithOne(p => p.User)
            .HasForeignKey<SkillProfile>(p => p.UserId);

        modelBuilder.Entity<LearningPath>()
            .HasMany(p => p.Modules)
            .WithOne(m => m.LearningPath)
            .HasForeignKey(m => m.LearningPathId);

        modelBuilder.Entity<CourseModule>()
            .HasMany(m => m.Challenges)
            .WithOne(c => c.CourseModule)
            .HasForeignKey(c => c.CourseModuleId);
        
        modelBuilder.Entity<UserAchievement>()
            .HasKey(ua => ua.Id);

        modelBuilder.Entity<UserAchievement>()
            .HasOne(ua => ua.User)
            .WithMany(u => u.UserAchievements)
            .HasForeignKey(ua => ua.UserId);

        modelBuilder.Entity<UserAchievement>()
            .HasOne(ua => ua.Achievement)
            .WithMany()
            .HasForeignKey(ua => ua.AchievementId);
    }
}
