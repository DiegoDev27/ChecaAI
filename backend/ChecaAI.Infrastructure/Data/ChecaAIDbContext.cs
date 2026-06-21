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

    // Backlog entities — transparency & institutional data
    public DbSet<PoliticianSalary> PoliticianSalaries { get; set; }
    public DbSet<CampaignExpense> CampaignExpenses { get; set; }
    public DbSet<AssetDeclaration> AssetDeclarations { get; set; }
    public DbSet<ElectionResult> ElectionResults { get; set; }
    public DbSet<SessionAttendance> SessionAttendances { get; set; }
    public DbSet<Committee> Committees { get; set; }
    public DbSet<CommitteeMembership> CommitteeMemberships { get; set; }
    public DbSet<VotingAlert> VotingAlerts { get; set; }

    // New entities — parties, cabinet staff, allowances
    public DbSet<Party> Parties { get; set; }
    public DbSet<CabinetStaff> CabinetStaff { get; set; }
    public DbSet<Allowance> Allowances { get; set; }

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
            entity.Property(e => e.ExternalId).HasMaxLength(50);

            entity.HasOne(e => e.PoliticalBloc)
                .WithMany(b => b.Politicians)
                .HasForeignKey(e => e.PoliticalBlocId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PartyEntity)
                .WithMany(p => p.Politicians)
                .HasForeignKey(e => e.PartyId)
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

        // PoliticianSalary configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<PoliticianSalary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => new { e.PoliticianId, e.Year, e.Month });
            entity.Property(e => e.GrossSalary).HasPrecision(15, 2);
            entity.Property(e => e.NetSalary).HasPrecision(15, 2);
            entity.Property(e => e.Allowances).HasPrecision(15, 2);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CampaignExpense configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<CampaignExpense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => e.PoliticianId);
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(15, 2);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AssetDeclaration configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<AssetDeclaration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => e.PoliticianId);
            entity.Property(e => e.AssetType).IsRequired();
            entity.Property(e => e.DeclaredValue).HasPrecision(15, 2);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ElectionResult configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<ElectionResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => new { e.PoliticianId, e.ElectionYear });
            entity.Property(e => e.Position).IsRequired();
            entity.Property(e => e.VoteShare).HasPrecision(8, 4);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SessionAttendance configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<SessionAttendance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => new { e.PoliticianId, e.SessionDate });
            entity.Property(e => e.Chamber).IsRequired();

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Committee configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<Committee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.CommitteeType).IsRequired();
            entity.Property(e => e.Chamber).IsRequired();
        });

        // CommitteeMembership configuration — indexes match AddBacklogEntities migration
        modelBuilder.Entity<CommitteeMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CommitteeId, e.PoliticianId });
            entity.HasIndex(e => e.PoliticianId);
            entity.Property(e => e.Role).IsRequired();

            entity.HasOne(e => e.Committee)
                .WithMany(c => c.Members)
                .HasForeignKey(e => e.CommitteeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Party configuration
        modelBuilder.Entity<Party>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Acronym).IsUnique();
            entity.HasIndex(e => e.Number);
            entity.HasIndex(e => e.ExternalId);
            entity.Property(e => e.Acronym).IsRequired();
            entity.Property(e => e.FullName).IsRequired();
        });

        // CabinetStaff configuration
        modelBuilder.Entity<CabinetStaff>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => new { e.PoliticianId, e.Year, e.Month });
            entity.Property(e => e.FullName).IsRequired();
            entity.Property(e => e.GrossSalary).HasPrecision(15, 2);
            entity.Property(e => e.NetSalary).HasPrecision(15, 2);

            entity.Property(e => e.PoliticianId).IsRequired(false);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Allowance configuration
        modelBuilder.Entity<Allowance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => new { e.PoliticianId, e.Year, e.Month, e.AllowanceType });
            entity.Property(e => e.AllowanceType).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(15, 2);

            entity.HasOne(e => e.Politician)
                .WithMany()
                .HasForeignKey(e => e.PoliticianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // VotingAlert configuration — indexes match AddVotingAlert migration (non-unique session index)
        modelBuilder.Entity<VotingAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VotingSessionId);
            entity.HasIndex(e => e.DetectedAt);
            entity.Property(e => e.AlertLevel).IsRequired();

            entity.HasOne(e => e.VotingSession)
                .WithMany(vs => vs.Alerts)
                .HasForeignKey(e => e.VotingSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}