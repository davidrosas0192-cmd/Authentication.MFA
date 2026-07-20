using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Authentication.Fido2.Data;

public class ApplicationDbContext : DbContext
{
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
        
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserFido2Credential> UserFido2Credentials => Set<UserFido2Credential>();
    
    public DbSet<Fido2Transaction> Fido2Transations => Set<Fido2Transaction>();
    public DbSet<UserMfaMethod> UserMfaMethods => Set<UserMfaMethod>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<MfaSession> MfaSessions => Set<MfaSession>();
    public DbSet<MfaManagementSession> MfaManagementSessions => Set<MfaManagementSession>();
    public DbSet<MfaLoginEnrollmentSession> MfaLoginEnrollmentSessions => Set<MfaLoginEnrollmentSession>();
    public DbSet<AccessTokenSession> AccessTokenSessions => Set<AccessTokenSession>();
    public DbSet<RefreshTokenSession> RefreshTokenSessions => Set<RefreshTokenSession>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<AuthenticationAuditEvent> AuthenticationAuditEvents => Set<AuthenticationAuditEvent>();
    public DbSet<UserRecoveryCodeBatch> UserRecoveryCodeBatches => Set<UserRecoveryCodeBatch>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }

        base.OnModelCreating(modelBuilder);
        
    }
}