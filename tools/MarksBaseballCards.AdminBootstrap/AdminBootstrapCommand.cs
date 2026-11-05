using System.Data;
using Microsoft.Data.SqlClient;

namespace MarksBaseballCards.AdminBootstrap;

public static class AdminBootstrapCommand
{
    public static SqlCommand Create(SqlConnection connection, string reviewedSql, AdminCredentials credentials)
    {
        credentials.Validate();
        var command = connection.CreateCommand();
        command.CommandText = reviewedSql;
        command.CommandTimeout = 15;
        command.Parameters.Add("@AdminEmail", SqlDbType.NVarChar, 64).Value = credentials.Email;
        command.Parameters.Add("@AdminPasswordHash", SqlDbType.NVarChar, 256).Value = credentials.PasswordHash;
        return command;
    }
}
