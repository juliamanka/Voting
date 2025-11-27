using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;

namespace Voting.Infrastructure.Database;

public class VotingDbContext : DbContext
{
    public VotingDbContext(DbContextOptions<VotingDbContext> options) : base(options)
    {
    }
    
    public DbSet<VoteRecord> Votes { get; set; }
    public DbSet<Poll> Polls { get; set; }
    public DbSet<PollOption> PollOptions { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Poll>(entity =>
        {
            entity.ToTable("Polls"); 
            entity.HasKey(p => p.PollId);
            entity.Property(p => p.Question).IsRequired().HasMaxLength(500);
            
            entity.Property(p => p.IsActive).HasDefaultValue(true);
        });
        
        modelBuilder.Entity<VoteRecord>(entity =>
        {
            entity.HasIndex(v => v.PollId, "IX_Votes_PollId");

            entity.HasIndex(v => new { v.PollId, v.UserId }, "IX_Votes_PollId_UserId")
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");
            
            entity.HasOne(v => v.Poll)
                .WithMany(p => p.Votes)
                .HasForeignKey(v => v.PollId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(v => v.PollOption)
                .WithMany(po => po.Votes)
                .HasForeignKey(v => v.PollOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<PollOption>(entity =>
        {
            entity.ToTable("PollOptions");
            entity.HasKey(po => po.PollOptionId);
            entity.Property(po => po.Text).IsRequired().HasMaxLength(200);
    
            // Relacja: Jedna Ankieta -> Wiele Opcji
            entity.HasOne(po => po.Poll)
                .WithMany(p => p.Options)
                .HasForeignKey(po => po.PollId)
                .OnDelete(DeleteBehavior.Cascade); // Jak usuwasz ankietę, usuń też jej opcje
        });
    }
}