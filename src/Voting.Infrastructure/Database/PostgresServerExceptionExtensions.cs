using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Voting.Infrastructure.Database;

public static class PostgresExceptionExtensions
{
    public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
    }
}