using Computernewb.CollabVMAuthServer.Database.Schema;
using Microsoft.EntityFrameworkCore;

namespace Computernewb.CollabVMAuthServer.Database;

public partial class CollabVMAuthDbContext : DbContext
{
    #pragma warning disable CS8618
    public CollabVMAuthDbContext(DbContextOptions<CollabVMAuthDbContext> options)
        : base(options)
    {
    }
    #pragma warning restore CS8618

    public virtual DbSet<Bot> Bots { get; set; }

    public virtual DbSet<IpBan> IpBans { get; set; }
    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Bot>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("bots");

            entity.HasIndex(e => e.Owner, "owner");

            entity.HasIndex(e => e.Token, "token").IsUnique();

            entity.HasIndex(e => e.Username, "username").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created");
            entity.Property(e => e.CvmRank)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("cvm_rank");
            entity.Property(e => e.Owner)
                .HasColumnName("owner");
            entity.Property(e => e.Token)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasColumnName("token");
            entity.Property(e => e.Username)
                .HasMaxLength(20)
                .HasColumnName("username");

            entity.HasOne(d => d.OwnerNavigation).WithMany(p => p.Bots)
                .HasPrincipalKey(p => p.Id)
                .HasForeignKey(d => d.Owner)
                .HasConstraintName("owner");
        });

        modelBuilder.Entity<IpBan>(entity =>
        {
            entity.HasKey(e => e.Ip).HasName("PRIMARY");

            entity.ToTable("ip_bans");

            entity.Property(e => e.Ip)
                .HasMaxLength(16)
                .HasColumnName("ip");
            entity.Property(e => e.BannedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("banned_at");
            entity.Property(e => e.BannedBy)
                .HasMaxLength(20)
                .HasColumnName("banned_by");
            entity.Property(e => e.Reason)
                .HasColumnType("text")
                .HasColumnName("reason");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Token).HasName("PRIMARY");

            entity.ToTable("sessions");

            entity.HasIndex(e => e.UserId, "user");

            entity.Property(e => e.Token)
                .HasMaxLength(32)
                .IsFixedLength()
                .HasColumnName("token");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created");
            entity.Property(e => e.LastIp)
                .HasMaxLength(16)
                .HasColumnName("last_ip");
            entity.Property(e => e.LastUsed)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("last_used");
            entity.Property(e => e.UserId)
                .HasColumnName("user");

            entity.HasOne(d => d.UserNavigation).WithMany(p => p.Sessions)
                .HasPrincipalKey(p => p.Id)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "email").IsUnique();

            entity.HasIndex(e => e.Username, "username").IsUnique();

            entity.Property(e => e.Id)
                .HasColumnType("int(10) unsigned")
                .HasColumnName("id");
            entity.Property(e => e.BanReason)
                .HasColumnType("text")
                .HasColumnName("ban_reason");
            entity.Property(e => e.Banned).HasColumnName("banned");
            entity.Property(e => e.Created)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("timestamp")
                .HasColumnName("created");
            entity.Property(e => e.CvmRank)
                .HasDefaultValueSql("'1'")
                .HasColumnType("int(10) unsigned")
                .HasColumnName("cvm_rank");
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.Developer).HasColumnName("developer");
            entity.Property(e => e.Email)
                .HasColumnType("text")
                .HasColumnName("email");
            entity.Property(e => e.EmailVerificationCode)
                .HasMaxLength(8)
                .IsFixedLength()
                .HasColumnName("email_verification_code");
            entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
            entity.Property(e => e.Password)
                .HasColumnType("text")
                .HasColumnName("password");
            entity.Property(e => e.PasswordResetCode)
                .HasMaxLength(8)
                .IsFixedLength()
                .HasColumnName("password_reset_code");
            entity.Property(e => e.RegistrationIp)
                .HasMaxLength(16)
                .HasColumnName("registration_ip");
            entity.Property(e => e.Username)
                .HasMaxLength(20)
                .HasColumnName("username");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
