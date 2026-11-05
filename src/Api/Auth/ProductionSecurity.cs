using Microsoft.Data.SqlClient;

namespace MarksBaseballCards.Api.Auth;

public static class ProductionSecurity
{
    public static void ValidateSqlConnection(string connectionString)
    {
        var sql = new SqlConnectionStringBuilder(connectionString);
        if (sql.Encrypt.Equals(SqlConnectionEncryptOption.Optional) || sql.TrustServerCertificate || sql.PersistSecurityInfo
            || string.Equals(sql.UserID, "sa", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production SQL requires certificate-validated encryption, Persist Security Info=False and a dedicated application identity (never sa).");
    }
}
