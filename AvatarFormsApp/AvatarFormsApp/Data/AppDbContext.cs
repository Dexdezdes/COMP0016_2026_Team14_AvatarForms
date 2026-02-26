using Microsoft.EntityFrameworkCore;
using AvatarFormsApp.Models;

namespace AvatarFormsApp.Data;

public class AppDbContext : DbContext
{
    // DbSets represent tables in your database
    public DbSet<Questionnaire> Questionnaires { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<ResponseSession> ResponseSessions { get; set; }
    public DbSet<Response> Responses { get; set; }

    // Constructor - allows passing configuration options
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // Configure the database connection and relationships
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Get path to store database in AppData/Local
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbPath = Path.Combine(appDataPath, "AvatarFormsApp", "questionnaires.db");
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            
            // Configure SQLite connection
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    // Configure entity relationships and constraints
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Questionnaire configuration
        modelBuilder.Entity<Questionnaire>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Name).IsRequired().HasMaxLength(200);
            entity.Property(q => q.Status).HasMaxLength(50);
            
            // One Questionnaire has many Questions
            entity.HasMany(q => q.Questions)
                  .WithOne(q => q.Questionnaire)
                  .HasForeignKey(q => q.QuestionnaireId)
                  .OnDelete(DeleteBehavior.Cascade); // Delete questions when questionnaire is deleted
            
            // One Questionnaire has many ResponseSessions
            entity.HasMany(q => q.ResponseSessions)
                  .WithOne(rs => rs.Questionnaire)
                  .HasForeignKey(rs => rs.QuestionnaireId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Question configuration
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Text).IsRequired();
            entity.Property(q => q.Type).IsRequired()
                  .HasConversion<string>(); // Store enum as string in database
            
            // One Question has many QuestionOptions
            entity.HasMany(q => q.Options)
                  .WithOne(o => o.Question)
                  .HasForeignKey(o => o.QuestionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // One Question has many Responses
            entity.HasMany(q => q.Responses)
                  .WithOne(r => r.Question)
                  .HasForeignKey(r => r.QuestionId)
                  .OnDelete(DeleteBehavior.Restrict); // Don't delete responses if question deleted
        });

        // QuestionOption configuration
        modelBuilder.Entity<QuestionOption>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Text).IsRequired().HasMaxLength(500);
        });

        // ResponseSession configuration
        modelBuilder.Entity<ResponseSession>(entity =>
        {
            entity.HasKey(rs => rs.Id);

            // One ResponseSession has many Responses
            entity.HasMany(rs => rs.Responses)
                  .WithOne(r => r.ResponseSession)
                  .HasForeignKey(r => r.ResponseSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Response configuration
        modelBuilder.Entity<Response>(entity =>
        {
            entity.HasKey(r => r.Id);
        });
    }
}
