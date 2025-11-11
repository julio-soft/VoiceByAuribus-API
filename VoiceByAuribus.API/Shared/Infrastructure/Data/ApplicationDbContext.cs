using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VoiceByAuribus_API.Shared.Interfaces;
using VoiceByAuribus_API.Shared.Domain;
using VoiceByAuribus_API.Features.Voices.Domain;
using VoiceByAuribus_API.Features.AudioFiles.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider) : base(options)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public DbSet<VoiceModel> VoiceModels => Set<VoiceModel>();
    public DbSet<AudioFile> AudioFiles => Set<AudioFile>();
    public DbSet<AudioPreprocessing> AudioPreprocessings => Set<AudioPreprocessing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all entity configurations from Shared/Infrastructure/Data/Configurations/
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        
        modelBuilder.ApplyGlobalFilters(_currentUserService);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        ApplyUserOwnership();

        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ApplyAuditing()
    {
        var utcNow = _dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }

    private void ApplyUserOwnership()
    {
        if (_currentUserService.UserId is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IHasUserId>().Where(e => e.State == EntityState.Added))
        {
            entry.Entity.UserId = _currentUserService.UserId;
        }
    }
}
