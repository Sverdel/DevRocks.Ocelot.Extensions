using System.Collections.Generic;
using System.Linq;
using PolicyServer.Local;

namespace DevRocks.Ocelot.PolicyServer
{
    public class PolicyOptions
    {
        public Dictionary<string, string[]> Roles { get; set; }

        public Dictionary<string, string[]> Permissions { get; set; }
        
        public Policy ToPolicy()
        {
            var policy = new Policy();
            policy.Roles.AddRange(
                Roles.Select(x =>
                {
                    var role = new Role
                    {
                        Name = x.Key
                    };
                    role.IdentityRoles.AddRange(x.Value);
                    return role;
                }));
            
            policy.Permissions.AddRange(
                Permissions.Select(x =>
                {
                    var permission = new Permission
                    {
                        Name = x.Key
                    };
                    permission.Roles.AddRange(x.Value);
                    return permission;
                }));

            return policy;
        }
    }
}