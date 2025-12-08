using Microsoft.EntityFrameworkCore;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Infrastructure.Data;

public class ChecaAIDbContext : DbContext
{
    public ChecaAIDbContext(DbContextOptions<ChecaAIDbContext> options) : base(options)
    {
    }

    public DbSet<Politician> Politicians { get; set; }
    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<VotingSession> VotingSessions { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<PoliticianExpense> PoliticianExpenses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Politician configuration
        modelBuilder.Entity<Politician>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => e.Cpf);
            entity.Property(e => e.FullName).IsRequired();
            entity.Property(e => e.PoliticalPosition).IsRequired();
        });

        // Proposal configuration
        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Chamber).IsRequired();
        });

        // VotingSession configuration
        modelBuilder.Entity<VotingSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Result).IsRequired();

            entity.HasOne(e => e.Proposal)
                .WithMany(p => p.VotingSessions)
                .HasForeignKey(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Vote configuration
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VoteValue).IsRequired();

            // Composite index for unique politician vote per session
            entity.HasIndex(e => new { e.PoliticianId, e.VotingSessionId }).IsUnique();

            entity.HasOne(e => e.Politician)
                .WithMany(p => p.Votes)
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.VotingSession)
                .WithMany(v => v.Votes)
                .HasForeignKey(e => e.VotingSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PoliticianExpense configuration
        modelBuilder.Entity<PoliticianExpense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(15, 2);

            entity.HasOne(e => e.Politician)
                .WithMany(p => p.Expenses)
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}