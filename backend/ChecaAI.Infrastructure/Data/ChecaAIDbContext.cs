using Microsoft.EntityFrameworkCore;
using ChecaAI.Domain.Entities;

namespace ChecaAI.Infrastructure.Data;

public class ChecaAIDbContext : DbContext
{
    public ChecaAIDbContext(DbContextOptions<ChecaAIDbContext> options) : base(options)
    {
    }

    public DbSet<Politician> Politicians { get; set; }
    public DbSet<PoliticianPhone> PoliticianPhones { get; set; }
    public DbSet<PoliticalBloc> PoliticalBlocs { get; set; }
    public DbSet<PoliticianMandate> PoliticianMandates { get; set; }
    public DbSet<Legislature> Legislatures { get; set; }
    public DbSet<MandateSubstitute> MandateSubstitutes { get; set; }
    public DbSet<MandateExercise> MandateExercises { get; set; }
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
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.FullName).IsRequired();
            entity.Property(e => e.PoliticalPosition).IsRequired();

            entity.HasOne(e => e.PoliticalBloc)
                .WithMany(b => b.Politicians)
                .HasForeignKey(e => e.PoliticalBlocId)
                .OnDelete(DeleteBehavior.SetNull);
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

        // PoliticianPhone configuration
        modelBuilder.Entity<PoliticianPhone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PhoneNumber).IsRequired();

            entity.HasOne(e => e.Politician)
                .WithMany(p => p.Phones)
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PoliticalBloc configuration
        modelBuilder.Entity<PoliticalBloc>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).IsRequired();
        });

        // PoliticianMandate configuration
        modelBuilder.Entity<PoliticianMandate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MandateCode);
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.ParticipationDescription).IsRequired();

            entity.HasOne(e => e.Politician)
                .WithMany(p => p.Mandates)
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TitularPolitician)
                .WithMany()
                .HasForeignKey(e => e.TitularPoliticianId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Legislature configuration
        modelBuilder.Entity<Legislature>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LegislatureType).IsRequired();

            entity.HasOne(e => e.Mandate)
                .WithMany(m => m.Legislatures)
                .HasForeignKey(e => e.MandateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MandateSubstitute configuration
        modelBuilder.Entity<MandateSubstitute>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ParticipationDescription).IsRequired();

            entity.HasOne(e => e.Mandate)
                .WithMany(m => m.Substitutes)
                .HasForeignKey(e => e.MandateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SubstitutePolitician)
                .WithMany()
                .HasForeignKey(e => e.SubstitutePoliticianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MandateExercise configuration
        modelBuilder.Entity<MandateExercise>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExerciseCode);

            entity.HasOne(e => e.Mandate)
                .WithMany(m => m.Exercises)
                .HasForeignKey(e => e.MandateId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}