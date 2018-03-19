using System;
using System.Linq;
using System.Collections.Generic;

using Rocket.API;
using Rocket.API.Serialisation;

using ChubbyQuokka.LoonePermissions.Managers;
using ChubbyQuokka.LoonePermissions.Extensions;

namespace ChubbyQuokka.LoonePermissions.Providers
{
    public sealed class MySqlPermissionProvider : IRocketPermissionsProvider
    {
        public RocketPermissionsProviderResult AddGroup(RocketPermissionsGroup group)
        {
            return MySqlManager.AddGroup(group);
        }

        public RocketPermissionsProviderResult DeleteGroup(string groupId)
        {
            return MySqlManager.DeleteGroup(groupId);
        }

        public RocketPermissionsProviderResult SaveGroup(RocketPermissionsGroup group)
        {
            return MySqlManager.SaveGroup(group);
        }

        //READY
        public RocketPermissionsProviderResult AddPlayerToGroup(string groupId, IRocketPlayer player)
        {
            return MySqlManager.AddPlayerToGroup(groupId, player);
        }

        //READY
        public RocketPermissionsProviderResult RemovePlayerFromGroup(string groupId, IRocketPlayer player)
        {
            return MySqlManager.RemovePlayerFromGroup(groupId, player);
        }

        //READY
        public RocketPermissionsGroup GetGroup(string groupId)
        {
            return MySqlManager.GetGroup(groupId);
        }

        //READY
        public List<RocketPermissionsGroup> GetGroups(IRocketPlayer player, bool includeParentGroups)
        {
            return MySqlManager.GetGroups(player, includeParentGroups);
        }

        //READY
        public List<Permission> GetPermissions(IRocketPlayer player)
        {
            List<RocketPermissionsGroup> groups = MySqlManager.GetGroups(player, true) ?? new List<RocketPermissionsGroup>();
            groups.Reverse();

            List<Permission> perms = new List<Permission>();

            foreach (RocketPermissionsGroup g in groups)
            {
                if (g != null)
                {
                    foreach (Permission p in g.Permissions)
                    {
                        if (p != null)
                        {
                            if (p.Name.StartsWith("-", StringComparison.InvariantCulture))
                            {
                                perms.RemoveAll(x => string.Equals(x.Name, p.Name.Replace("-", ""), StringComparison.InvariantCultureIgnoreCase));
                            }
                            else
                            {
                                perms.RemoveAll(x => x.Name.Equals(p.Name, StringComparison.InvariantCultureIgnoreCase));
                                perms.Add(p);
                            }
                        }
                    }
                }
            }

            return perms;
        }

        //READY
        public List<Permission> GetPermissions(IRocketPlayer player, List<string> requestedPermissions)
        {
            List<string> newPerms = requestedPermissions?.Where(x => !string.IsNullOrEmpty(x))?.Select(x => x)?.Distinct()?.ToList() ?? new List<string>();

            return GetPermissions(player)?.Where(x => newPerms.ContainsIgnoreCase(x.Name))?.ToList() ?? new List<Permission>();
        }

        //READY
        public bool HasPermission(IRocketPlayer player, List<string> requestedPermissions)
        {
            if (player.IsAdmin)
            {
                return true;
            }

            return GetPermissions(player, requestedPermissions).Count != 0;
        }

        public void Reload()
        {
            LoonePermissionsPlugin.Log("It is not necessary to reload permissions with this plugin!", ConsoleColor.Yellow);
        }
    }
}
