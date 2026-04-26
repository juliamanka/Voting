using MassTransit;
using Microsoft.EntityFrameworkCore;
using Voting.Domain.Entities;
using Voting.Domain.Enums;

namespace Voting.Infrastructure.Database;

public class VotingDbContext : DbContext
{
    public VotingDbContext(DbContextOptions<VotingDbContext> options) : base(options)
    {
    }

    public DbSet<VoteRecord> Votes { get; set; }
    public DbSet<VoteSubmission> VoteSubmissions { get; set; }
    public DbSet<Poll> Polls { get; set; }
    public DbSet<PollOption> PollOptions { get; set; }
    public DbSet<VoterEligibility> VoterEligibilities { get; set; }
    public DbSet<VoteAuditLog> VoteAuditLogs { get; set; }
    public DbSet<PollResultsProjection> PollResultsProjections { get; set; }
    public DbSet<PollOptionResultsProjection> PollOptionResultsProjections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Poll>(entity =>
        {
            entity.ToTable("Polls");
            entity.HasKey(p => p.PollId);
            entity.Property(p => p.Question).IsRequired().HasMaxLength(500);

            entity.Property(p => p.IsActive).HasDefaultValue(true);
            entity.Property(p => p.RequiresEligibilityCheck).HasDefaultValue(true);
        });

        modelBuilder.Entity<VoteRecord>(entity =>
        {
            entity.ToTable("Votes");
            entity.HasIndex(v => v.PollId, "IX_Votes_PollId");

            entity.HasIndex(v => new { v.PollId, v.UserId }, "IX_Votes_PollId_UserId")
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL");

            entity.Property(v => v.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(VoteStatus.Counted);

            entity.HasOne(v => v.Poll)
                .WithMany(p => p.Votes)
                .HasForeignKey(v => v.PollId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.PollOption)
                .WithMany(po => po.Votes)
                .HasForeignKey(v => v.PollOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VoteSubmission>(entity =>
        {
            entity.ToTable("VoteSubmissions");
            entity.HasKey(v => v.SubmissionId);

            entity.Property(v => v.Architecture)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(v => v.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(v => v.FailureReason)
                .HasMaxLength(512);

            entity.HasIndex(v => v.Status, "IX_VoteSubmissions_Status");
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

        modelBuilder.Entity<VoterEligibility>(entity =>
        {
            entity.ToTable("VoterEligibilities");
            entity.HasKey(v => v.UserId);
            entity.Property(v => v.UserId).HasMaxLength(256);
            entity.Property(v => v.IsEligible).HasDefaultValue(true);
            entity.Property(v => v.EligibilitySource).HasMaxLength(64);
        });

        modelBuilder.Entity<VoteAuditLog>(entity =>
        {
            entity.ToTable("VoteAuditLogs");
            entity.HasKey(v => v.AuditLogId);
            entity.Property(v => v.UserId).HasMaxLength(256);
            entity.Property(v => v.Architecture).HasMaxLength(32);
            entity.Property(v => v.Action).HasMaxLength(64);
            entity.HasIndex(v => v.PollId, "IX_VoteAuditLogs_PollId");
            entity.HasIndex(v => v.LoggedAtUtc, "IX_VoteAuditLogs_LoggedAtUtc");
            entity.HasIndex(v => v.VoteId, "IX_VoteAuditLogs_VoteId").IsUnique();
        });

        modelBuilder.Entity<PollResultsProjection>(entity =>
        {
            entity.ToTable("PollResultsProjections");
            entity.HasKey(p => p.PollId);
            entity.Property(p => p.PollTitle).HasMaxLength(500);
            entity.HasMany(p => p.Options)
                .WithOne(o => o.PollResultsProjection)
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollOptionResultsProjection>(entity =>
        {
            entity.ToTable("PollOptionResultsProjections");
            entity.HasKey(p => new { p.PollId, p.PollOptionId });
            entity.Property(p => p.OptionText).HasMaxLength(200);
        });

        modelBuilder.AddTransactionalOutboxEntities();

        var pollId = Guid.Parse("A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D");
        var poll2Id = Guid.Parse("B1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D");
        var poll3Id = Guid.Parse("C1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D");
        var opt1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var opt2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var opt3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var poll2Opt1Id = Guid.Parse("21111111-1111-1111-1111-111111111111");
        var poll2Opt2Id = Guid.Parse("22221111-1111-1111-1111-111111111111");
        var poll2Opt3Id = Guid.Parse("22222211-1111-1111-1111-111111111111");
        var poll3Opt1Id = Guid.Parse("31111111-1111-1111-1111-111111111111");
        var poll3Opt2Id = Guid.Parse("33311111-1111-1111-1111-111111111111");
        var poll3Opt3Id = Guid.Parse("33333111-1111-1111-1111-111111111111");

        modelBuilder.Entity<Poll>().HasData(
            new Poll
            {
                PollId = pollId,
                Question = "Która architektura jest najwydajniejsza dla systemu głosowania?",
                IsActive = true,
                RequiresEligibilityCheck = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Poll
            {
                PollId = poll2Id,
                Question = "Który kanał aktualizacji wyników najlepiej wspiera aplikacje czasu rzeczywistego?",
                IsActive = true,
                RequiresEligibilityCheck = true,
                CreatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            new Poll
            {
                PollId = poll3Id,
                Question = "Która strategia obsługi przeciążenia jest najbardziej akceptowalna dla użytkownika?",
                IsActive = true,
                RequiresEligibilityCheck = true,
                CreatedAt = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            });

        modelBuilder.Entity<PollOption>().HasData(
            new PollOption { PollOptionId = opt1Id, PollId = pollId, Text = "Synchroniczna (API)", OrderIndex = 1 },
            new PollOption { PollOptionId = opt2Id, PollId = pollId, Text = "Asynchroniczna (Event-Driven)", OrderIndex = 2 },
            new PollOption { PollOptionId = opt3Id, PollId = pollId, Text = "Hybrydowa (Core Sync + Async Side Effects)", OrderIndex = 3 },
            new PollOption { PollOptionId = poll2Opt1Id, PollId = poll2Id, Text = "Natychmiastowy odczyt z bazy", OrderIndex = 1 },
            new PollOption { PollOptionId = poll2Opt2Id, PollId = poll2Id, Text = "SignalR po aktualizacji projekcji", OrderIndex = 2 },
            new PollOption { PollOptionId = poll2Opt3Id, PollId = poll2Id, Text = "Odświeżanie okresowe", OrderIndex = 3 },
            new PollOption { PollOptionId = poll3Opt1Id, PollId = poll3Id, Text = "Kolejkowanie i późniejsze przetworzenie", OrderIndex = 1 },
            new PollOption { PollOptionId = poll3Opt2Id, PollId = poll3Id, Text = "Odrzucenie nadmiaru żądań", OrderIndex = 2 },
            new PollOption { PollOptionId = poll3Opt3Id, PollId = poll3Id, Text = "Spowolnienie odpowiedzi synchronicznej", OrderIndex = 3 }
        );

        modelBuilder.Entity<PollResultsProjection>().HasData(
            new PollResultsProjection
            {
                PollId = pollId,
                PollTitle = "Która architektura jest najwydajniejsza dla systemu głosowania?",
                TotalVotes = 0,
                LastUpdatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PollResultsProjection
            {
                PollId = poll2Id,
                PollTitle = "Który kanał aktualizacji wyników najlepiej wspiera aplikacje czasu rzeczywistego?",
                TotalVotes = 0,
                LastUpdatedAtUtc = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            new PollResultsProjection
            {
                PollId = poll3Id,
                PollTitle = "Która strategia obsługi przeciążenia jest najbardziej akceptowalna dla użytkownika?",
                TotalVotes = 0,
                LastUpdatedAtUtc = new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc)
            });

        modelBuilder.Entity<PollOptionResultsProjection>().HasData(
            new PollOptionResultsProjection
            {
                PollId = pollId,
                PollOptionId = opt1Id,
                OptionText = "Synchroniczna (API)",
                OrderIndex = 1,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = pollId,
                PollOptionId = opt2Id,
                OptionText = "Asynchroniczna (Event-Driven)",
                OrderIndex = 2,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = pollId,
                PollOptionId = opt3Id,
                OptionText = "Hybrydowa (Core Sync + Async Side Effects)",
                OrderIndex = 3,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = poll2Id,
                PollOptionId = poll2Opt1Id,
                OptionText = "Natychmiastowy odczyt z bazy",
                OrderIndex = 1,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = poll2Id,
                PollOptionId = poll2Opt2Id,
                OptionText = "SignalR po aktualizacji projekcji",
                OrderIndex = 2,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = poll2Id,
                PollOptionId = poll2Opt3Id,
                OptionText = "Odświeżanie okresowe",
                OrderIndex = 3,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = poll3Id,
                PollOptionId = poll3Opt1Id,
                OptionText = "Kolejkowanie i późniejsze przetworzenie",
                OrderIndex = 1,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = poll3Id,
                PollOptionId = poll3Opt2Id,
                OptionText = "Odrzucenie nadmiaru żądań",
                OrderIndex = 2,
                VoteCount = 0
            },
            new PollOptionResultsProjection
            {
                PollId = poll3Id,
                PollOptionId = poll3Opt3Id,
                OptionText = "Spowolnienie odpowiedzi synchronicznej",
                OrderIndex = 3,
                VoteCount = 0
            });
    }
}
