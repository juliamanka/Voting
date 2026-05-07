using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Voting.Infrastructure.Database;

public static class SqlServerExceptionExtensions
{
    public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }
}
