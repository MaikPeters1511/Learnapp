using Microsoft.EntityFrameworkCore;
using AILearningPlatform.ApiServer.Models;

namespace AILearningPlatform.ApiServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<SkillProfile> SkillProfiles => Set<SkillProfile>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<CourseModule> CourseModules => Set<CourseModule>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<Submission> Submissions => Set<Submission>();

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
    }
}
