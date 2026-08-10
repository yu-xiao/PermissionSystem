using System.Security.Cryptography;
using PermissionSystem.Domain.Common;
using PermissionSystem.Shared.Constants;
using PermissionSystem.Shared.Exceptions;

namespace PermissionSystem.Application.Common;

public static class ConcurrencyTokenGuard
{
    public static void EnsureMatches(BaseEntity entity, byte[]? expectedToken)
    {
        if (expectedToken is null || expectedToken.Length == 0)
        {
            return;
        }

        if (entity.RowVersion.Length == 0 ||
            entity.RowVersion.Length != expectedToken.Length ||
            !CryptographicOperations.FixedTimeEquals(entity.RowVersion, expectedToken))
        {
            throw new BusinessException(
                ErrorCode.Conflict,
                "The resource was modified by another request. Reload it before saving again.");
        }
    }
}
