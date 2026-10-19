using AICustomerSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AICustomerSupport.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageSource> MessageSources => Set<MessageSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DocumentVersion>().HasKey(v => v.VersionId);
        modelBuilder.Entity<DocumentChunk>().HasKey(c => c.ChunkId);

        modelBuilder.Entity<Role>().HasData(
            new Role 
            { 
                RoleId = 1, 
                RoleName = "System Administrator", 
                Description = "Manages entire platform, users, and system config" 
            },
            new Role 
            { 
                RoleId = 2, 
                RoleName = "Knowledge Base Administrator", 
                Description = "Manages document uploads and processing status" 
            },
            new Role 
            { 
                RoleId = 3, 
                RoleName = "Customer Support Agent", 
                Description = "Uses RAG AI assistant to answer customer queries" 
            }
        );

        // Configure relationships & constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Cấu hình bảng Document và thêm ràng buộc Check Constraint cho Category
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.DocumentId);

            entity.Property(d => d.Category)
                  .IsRequired()
                  .HasMaxLength(50);

            // Ràng buộc cứng trong SQL Server: chỉ cho phép 2 giá trị này
            entity.HasCheckConstraint("CK_Document_Category", "[Category] IN ('Thông báo', 'Gíao trình')");
        });

        modelBuilder.Entity<DocumentVersion>()
            .HasOne(v => v.Document)
            .WithMany(d => d.Versions)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DocumentChunk>()
            .HasOne(c => c.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Conversation>().HasKey(c => c.ConversationId);
        modelBuilder.Entity<Message>().HasKey(m => m.MessageId);
        modelBuilder.Entity<MessageSource>().HasKey(ms => ms.MessageSourceId);
    }
}