using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VoiceByAuribus_API.Shared.Interfaces;
using VoiceByAuribus_API.Shared.Domain;

namespace VoiceByAuribus_API.Shared.Infrastructure.Data;

public static class ModelBuilderExtensions
{
    public static void ApplyGlobalFilters(this ModelBuilder modelBuilder, ICurrentUserService currentUserService)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var filter = ComposeFilter(entityType, currentUserService);
            if (filter is not null)
            {
                entityType.SetQueryFilter(filter);
            }
        }
    }

    private static LambdaExpression? ComposeFilter(IMutableEntityType entityType, ICurrentUserService currentUserService)
    {
        var entityClrType = entityType.ClrType;
        var parameter = Expression.Parameter(entityClrType, "entity");
        Expression? body = null;

        if (typeof(ISoftDelete).IsAssignableFrom(entityClrType))
        {
            var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var notDeleted = Expression.Equal(isDeletedProperty, Expression.Constant(false));
            body = notDeleted;
        }

        if (typeof(IHasUserId).IsAssignableFrom(entityClrType))
        {
            var userIdProperty = Expression.Property(parameter, nameof(IHasUserId.UserId));
            var serviceConstant = Expression.Constant(currentUserService);
            var serviceUserIdProperty = Expression.Property(serviceConstant, nameof(ICurrentUserService.UserId));

            var userIdNull = Expression.Equal(serviceUserIdProperty, Expression.Constant(null, typeof(Guid?)));
            var userMatches = Expression.Equal(userIdProperty, serviceUserIdProperty);
            var userFilter = Expression.OrElse(userIdNull, userMatches);

            body = body is null ? userFilter : Expression.AndAlso(body, userFilter);
        }

        return body is null ? null : Expression.Lambda(body, parameter);
    }
}
