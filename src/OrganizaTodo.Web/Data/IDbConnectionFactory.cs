using System.Data;

namespace OrganizaTodo.Web.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
