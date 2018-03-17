﻿using System;
using System.Collections.Generic;

using Rocket.API;
using Rocket.API.Serialisation;

using MySql.Data.MySqlClient;
using System.Text;
using System.Linq;
using System.Diagnostics;
using Rocket.Core.Assets;
using SDG.Unturned;

namespace ChubbyQuokka.LoonePermissions.Managers
{
    internal static class MySqlManager
    {
        static MySqlConnection UnthreadedConnection;
        static MySqlConnection ThreadedConnection;

        static LoonePermissionsConfig._DatabaseSettings Settings => LoonePermissionsConfig.DatabaseSettings;

        static RocketPermissions internalPerms;
        static object internalPermsLock = new object();

        static bool IsCacheMode => LoonePermissionsConfig.CacheModeSettings.Enabled;
        
        public static void Initialize()
        {
            UnthreadedConnection = new MySqlConnection(Queries.Connection);
            ThreadedConnection = new MySqlConnection(Queries.Connection);

            MySqlCommand cmd1 = CreateCommand();
            MySqlCommand cmd2 = CreateCommand();
            MySqlCommand cmd3 = CreateCommand();

            cmd1.CommandText = Queries.ShowTablesLikeGroupTable;
            cmd2.CommandText = Queries.ShowTablesLikePermissionTable;
            cmd3.CommandText = Queries.ShowTablesLikePlayerTable;

            OpenConnection();

            object obj1 = cmd1.ExecuteScalar();
            object obj2 = cmd2.ExecuteScalar();
            object obj3 = cmd3.ExecuteScalar();

            CloseConnection();

            if (obj1 == null)
            {
                OpenConnection();

                MySqlCommand cmd = UnthreadedConnection.CreateCommand();
                cmd.CommandText = Queries.CreateGroupTable;

                cmd.ExecuteNonQuery();

                UnthreadedConnection.Close();

                LoonePermissionsPlugin.Log($"Generating table: {Settings.GroupsTableName}", ConsoleColor.Yellow);

                //CreateGroup("default");
            }

            if (obj2 == null)
            {
                OpenConnection();

                MySqlCommand cmd = UnthreadedConnection.CreateCommand();
                cmd.CommandText = Queries.CreatePermissionTable;

                cmd.ExecuteNonQuery();

                CloseConnection();

                LoonePermissionsPlugin.Log($"Generating table: {Settings.PermissionsTableName}", ConsoleColor.Yellow);

                //AddPermission("default", "p", 0);
                //AddPermission("default", "rocket", 0);
                //AddPermission("default", "compass", 60);
            }

            if (obj3 == null)
            {
                OpenConnection();

                MySqlCommand cmd = CreateCommand();
                cmd.CommandText = Queries.CreatePlayerTable;

                cmd.ExecuteNonQuery();

                CloseConnection();

                LoonePermissionsPlugin.Log($"Generating table: {Settings.PlayerTableName}", ConsoleColor.Yellow);
            }

            if (obj1 == null && obj2 == null && obj3 == null)
            {
                Migrate();
            }
        }

        public static void Destroy()
        {
            if (UnthreadedConnection != null)
            {
                UnthreadedConnection.Dispose();
                UnthreadedConnection = null;
            }

            if (ThreadedConnection != null)
            {
                ThreadedConnection.Dispose();
                ThreadedConnection = null;
            }
        }
        
        internal static void Refresh()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            RocketPermissions temp = new RocketPermissions();

            var cmd1 = CreateCommand();
            var cmd2 = CreateCommand();
            var cmd3 = CreateCommand();

            cmd1.CommandText = Queries.SelectAllGroups;
            cmd2.CommandText = Queries.SelectAllPermissions;
            cmd3.CommandText = Queries.SelectAllPlayers;

            Dictionary<string, RocketPermissionsGroup> dict = new Dictionary<string, RocketPermissionsGroup>();

            OpenConnection();

            var dr1 = cmd1.ExecuteReader();

            while (dr1.Read())
            {
                RocketPermissionsGroup g = new RocketPermissionsGroup
                {
                    Members = new List<string>(),
                    Permissions = new List<Permission>(),
                    Id = dr1.GetString(0).ToLowerInvariant(),
                    DisplayName = dr1.GetString(1),
                    ParentGroup = dr1.GetString(2).ToLowerInvariant(),
                    Prefix = dr1.GetString(3),
                    Suffix = dr1.GetString(4),
                    Color = dr1.GetString(5),
                    Priority = dr1.GetInt16(6)
                };

                dict.Add(g.Id, g);
            }

            dr1.Close();
            dr1.Dispose();

            var dr2 = cmd2.ExecuteReader();

            while (dr2.Read())
            {
                string group = dr2.GetString(0);

                Permission p = new Permission(dr2.GetString(1), dr2.GetUInt32(2));

                if (dict.TryGetValue(group, out RocketPermissionsGroup g))
                {
                    g.Permissions.Add(p);
                }
            }

            dr2.Close();
            dr2.Dispose();

            var dr3 = cmd3.ExecuteReader();

            while (dr3.Read())
            {
                ulong cSteamId = dr3.GetUInt64(0);
                string group = dr3.GetString(1);

                if (dict.TryGetValue(group, out RocketPermissionsGroup g))
                {
                    g.Members.Add(cSteamId.ToString());
                }
            }

            dr3.Close();
            dr3.Dispose();

            CloseConnection();

            lock (internalPermsLock)
            {
                internalPerms = temp;
            }

            watch.Stop();

            string a = (ThreadedWorkManager.IsWorkerThread) ? "Worker" : "Main";

            LoonePermissionsPlugin.Log($"Reloading the cache took {watch.ElapsedMilliseconds} milliseconds on the {a} Thread!", ConsoleColor.Yellow);
        }

        static MySqlCommand CreateCommand()
        {
            if (ThreadedWorkManager.IsWorkerThread)
            {
                return ThreadedConnection.CreateCommand();
            }

            return UnthreadedConnection.CreateCommand();
        }

        static void OpenConnection()
        {
            if (ThreadedWorkManager.IsWorkerThread)
            {
                ThreadedConnection.Open();
            }
            else
            {
                UnthreadedConnection.Open();
            }
        }

        static void CloseConnection()
        {
            if (ThreadedWorkManager.IsWorkerThread)
            {
                ThreadedConnection.Close();
            }
            else
            {
                UnthreadedConnection.Close();
            }
        }

        //TODO LATER :D
        public static RocketPermissionsProviderResult AddGroup(RocketPermissionsGroup group)
        {
            throw new NotImplementedException();
        }

        //TODO LATER :D
        public static RocketPermissionsProviderResult DeleteGroup(string groupId)
        {
            throw new NotImplementedException();
        }

        //TODO LATER :D
        public static RocketPermissionsProviderResult SaveGroup(RocketPermissionsGroup group)
        {
            throw new NotImplementedException();
        }

        //READY
        public static RocketPermissionsGroup GetGroup(string groupId)
        {
            if (IsCacheMode)
            {
                RocketPermissionsGroup g;
                groupId = groupId.ToLowerInvariant();

                lock (internalPermsLock)
                {
                    g =  internalPerms.Groups.Where(x => x.Id == groupId).FirstOrDefault();
                }

                return g;
            }

            if (!GroupExists(groupId))
            {
                return null;
            }

            List<RocketPermissionsGroup> groups = new List<RocketPermissionsGroup>();

            GetGroups(groups, new string[] { groupId }, false, false);

            return groups.FirstOrDefault();
        }

        //READY
        public static RocketPermissionsProviderResult AddPlayerToGroup(string groupId, IRocketPlayer player)
        {
            if (!GroupExists(groupId))
            {
                return RocketPermissionsProviderResult.GroupNotFound;
            }

            if (IsPlayerInGroup(player, groupId))
            {
                return RocketPermissionsProviderResult.DuplicateEntry;
            }

            var cmd1 = CreateCommand();
            cmd1.CommandText = Queries.InsertPlayerToPlayers(player.Id, groupId);

            OpenConnection();
            cmd1.ExecuteNonQuery();
            CloseConnection();

            if (IsCacheMode)
            {
                GetGroup(groupId).Members.Add(player.Id);
            }

            return RocketPermissionsProviderResult.Success;
        }

        //READY
        public static List<RocketPermissionsGroup> GetGroups(IRocketPlayer player, bool includeParentGroups)
        {
            List<RocketPermissionsGroup> groups;

            if (IsCacheMode)
            {
                lock (internalPerms)
                {
                    groups = internalPerms.Groups.Where(x => x.Members.Contains(player.Id)).ToList();
                }

                groups.Add(GetGroup(LoonePermissionsConfig.DefaultGroup));

                if (includeParentGroups)
                {
                    string[] args = groups.Select(x => x.ParentGroup).ToArray();

                    GetGroups(groups, args, true, false);
                }

                return groups.Distinct().OrderBy(x => x.Priority).ToList();
            }

            groups = new List<RocketPermissionsGroup>();
            List<string> ids = new List<string>();

            var cmd1 = CreateCommand();
            cmd1.CommandText = Queries.SelectGroupIdsByPlayer(player.Id);

            OpenConnection();

            var dr1 = cmd1.ExecuteReader();

            while (dr1.Read())
            {
                ids.Add(dr1.GetString(0));
            }

            dr1.Close();
            dr1.Dispose();

            CloseConnection();

            GetGroups(groups, ids.Distinct().Where(x => !string.IsNullOrEmpty(x)).ToArray(), includeParentGroups, false);

            return groups.Distinct().OrderBy(x => x.Priority).ToList();
        }

        //READY
        public static void GetGroups(List<RocketPermissionsGroup> groups, string[] ids, bool includeParent, bool reorder)
        {
            ids = ids.Where(x => !string.IsNullOrEmpty(x)).Select(x => x.ToLowerInvariant()).Distinct().ToArray();

            if (IsCacheMode)
            {
                groups.AddRange(internalPerms.Groups.Where(x => ids.Contains(x.Id)).ToList());

                if (includeParent)
                {
                    List<string> parentsNeeded1 = new List<string>();

                    foreach (RocketPermissionsGroup g in groups)
                    {
                        var parent = groups.Where(x => x.Id == g.ParentGroup).FirstOrDefault();

                        if (parent == null)
                        {
                            parentsNeeded1.Add(g.ParentGroup);
                        }
                    }

                    string[] parentsArray = parentsNeeded1.Distinct().ToArray();

                    if (parentsArray.Length != 0)
                    {
                        GetGroups(groups, parentsArray, true, false);
                    }
                }

                if (reorder)
                {
                    groups.Distinct().OrderBy(x => x.Priority);
                }

                return;
            }

            var cmd1 = CreateCommand();
            cmd1.CommandText = Queries.SelectGroupsByGroupIds(ids);

            OpenConnection();
            var dr1 = cmd1.ExecuteReader();

            List<string> parentsNeeded = new List<string>();

            while (dr1.Read())
            {
                RocketPermissionsGroup g = new RocketPermissionsGroup
                {
                    Id = dr1.GetString(0).ToLowerInvariant(),
                    DisplayName = dr1.GetString(1),
                    ParentGroup = dr1.GetString(2).ToLowerInvariant(),
                    Prefix = dr1.GetString(3),
                    Suffix = dr1.GetString(4),
                    Priority = dr1.GetInt16(5)
                };

                if (includeParent)
                {
                    var parent = groups.Where(x => x.Id == g.ParentGroup).FirstOrDefault();

                    if (parent == null)
                    {
                        parentsNeeded.Add(g.ParentGroup);
                    }
                }

                groups.Add(g);
            }

            dr1.Close();
            dr1.Dispose();

            CloseConnection();

            if (includeParent)
            {
                string[] parentsArray = parentsNeeded.Distinct().ToArray();

                if (parentsArray.Length != 0)
                {
                    GetGroups(groups, parentsArray, true, false);
                }
            }

            if (reorder)
            {
                groups.Distinct().OrderBy(x => x.Priority);
            }
        }

        //READY
        public static RocketPermissionsProviderResult RemovePlayerFromGroup(string groupId, IRocketPlayer player)
        {
            if (IsPlayerInGroup(player, groupId))
            {
                if (IsCacheMode)
                {
                    lock (internalPermsLock)
                    {
                        internalPerms.Groups.Where(x => x.Id == groupId).First().Members.Remove(player.Id);
                    }
                }

                var cmd1 = CreateCommand();

                cmd1.CommandText = Queries.DeletePlayerFromPlayers(player.DisplayName, groupId);

                OpenConnection();
                cmd1.ExecuteNonQuery();
                CloseConnection();

                return RocketPermissionsProviderResult.Success;
            }

            return RocketPermissionsProviderResult.UnspecifiedError;
        }

        //READY
        public static List<string> GetPlayerGroupIds(IRocketPlayer player)
        {
            List<string> groups;

            if (IsCacheMode)
            {
                lock (internalPermsLock)
                {
                    groups = internalPerms.Groups.Where(x => x.Members.Contains(player.Id)).Select(x => x.Id).Distinct().ToList();
                }

                if (!groups.Contains(LoonePermissionsConfig.DefaultGroup))
                {
                    groups.Add(LoonePermissionsConfig.DefaultGroup);
                }

                return groups;
            }

            groups = new List<string>
            {
                LoonePermissionsConfig.DefaultGroup
            };

            var cmd1 = CreateCommand();

            cmd1.CommandText = Queries.SelectGroupIdsByPlayer(player.Id);

            OpenConnection();

            var dr1 = cmd1.ExecuteReader();

            while (dr1.Read())
            {
                groups.Add(dr1.GetString(0).ToLowerInvariant());
            }

            dr1.Close();
            dr1.Dispose();

            CloseConnection();

            return groups.Distinct().ToList();
        }

        //READY
        public static bool GroupExists(string groupId)
        {
            if (IsCacheMode)
            {
                RocketPermissionsGroup group;

                lock (internalPermsLock)
                {
                    group = internalPerms.Groups.Where(x => x.Id == groupId.ToLowerInvariant()).FirstOrDefault();
                }

                return group != null;
            }

            var cmd1 = CreateCommand();

            cmd1.CommandText = Queries.GroupExists(groupId);

            OpenConnection();
            var result = cmd1.ExecuteScalar();
            CloseConnection();

            return (bool)result;
        }

        //READY
        public static bool IsPlayerInGroup(IRocketPlayer player, string groupId)
        {
            if (IsCacheMode)
            {
                RocketPermissionsGroup group;

                lock (internalPermsLock)
                {
                    group = internalPerms.Groups.Where(x => x.Id == groupId.ToLowerInvariant()).Where(x => x.Members.Contains(player.Id)).FirstOrDefault();
                }

                return group != null;
            }

            var cmd1 = CreateCommand();

            cmd1.CommandText = Queries.PlayerExists(player.Id, groupId);

            OpenConnection();
            var result = cmd1.ExecuteScalar();
            CloseConnection();

            return (bool)result;
        }

        //READY
        public static bool Migrate()
        {
            try
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();

                RocketPermissions p = new XMLFileAsset<RocketPermissions>(Rocket.Core.Environment.PermissionFile, null, null).Instance;

                var cmd1 = CreateCommand();
                var cmd2 = CreateCommand();
                var cmd3 = CreateCommand();
                var cmd4 = CreateCommand();
                var cmd5 = CreateCommand();
                var cmd6 = CreateCommand();

                cmd1.CommandText = Queries.DeleteAllFromGroups;
                cmd2.CommandText = Queries.DeleteAllFromPlayers;
                cmd3.CommandText = Queries.DeleteAllFromPermissions;
                cmd4.CommandText = Queries.InsertGroupsIntoGroups(p.Groups);
                cmd5.CommandText = Queries.InsertGroupsIntoPlayers(p.Groups);
                cmd6.CommandText = Queries.InsertGroupsIntoPermissions(p.Groups);

                OpenConnection();

                cmd1.ExecuteNonQuery();
                cmd2.ExecuteNonQuery();
                cmd3.ExecuteNonQuery();
                cmd4.ExecuteNonQuery();
                cmd5.ExecuteNonQuery();
                cmd6.ExecuteNonQuery();

                CloseConnection();

                LoonePermissionsConfig.SetDefaultGroup(p.DefaultGroup);

                timer.Stop();

                LoonePermissionsPlugin.Log($"Migration took {timer.ElapsedMilliseconds} milliseconds to complete!", ConsoleColor.Yellow);

                return true;
            }
            catch (Exception e)
            {
                LoonePermissionsPlugin.Log("Migration failed!", ConsoleColor.Red);
                LoonePermissionsPlugin.LogException(e);

                return false;
            }
        }

        static class Queries
        {
            public const string ValueSeperator = "', '";

            public static string Connection => $"SERVER={Settings.Address};DATABASE={Settings.Database};UID={Settings.Username};PASSWORD={Settings.Password};PORT={Settings.Port};";

            public static string CreateGroupTable => $"CREATE TABLE `{Settings.GroupsTableName}` (`groupid` VARCHAR(64) NOT NULL UNIQUE, `groupname` VARCHAR(64) NOT NULL, `parent` VARCHAR(64) NOT NULL, `prefix` VARCHAR(64) NOT NULL, `suffix` VARCHAR(64) NOT NULL, `color` VARCHAR(16) NOT NULL DEFAULT 'white', `priority` BIGINT NOT NULL DEFAULT '100', PRIMARY KEY (`groupid`))";
            public static string CreatePermissionTable => $"CREATE TABLE `{Settings.PermissionsTableName}` (`groupid` VARCHAR(64) NOT NULL, `permission` VARCHAR(64) NOT NULL, `cooldown` INTEGER NOT NULL DEFAULT '0')";
            public static string CreatePlayerTable => $"CREATE TABLE `{Settings.PlayerTableName}` (`csteamid` BIGINT NOT NULL, `groupid` VARCHAR(64) NOT NULL)";

            public static string ShowTablesLikeGroupTable => $"SHOW TABLES LIKE '{Settings.GroupsTableName}'";
            public static string ShowTablesLikePermissionTable => $"SHOW TABLES LIKE '{Settings.PermissionsTableName}'";
            public static string ShowTablesLikePlayerTable => $"SHOW TABLES LIKE '{Settings.PlayerTableName}'";

            public static string SelectAllGroups => $"SELECT * FROM `{Settings.GroupsTableName}` ORDER BY `priority` ASC";
            public static string SelectAllPermissions => $"SELECT * FROM `{Settings.PermissionsTableName}`";
            public static string SelectAllPlayers => $"SELECT * FROM `{Settings.PlayerTableName}`";

            public static string DeleteAllFromGroups => $"DELETE FROM `{Settings.GroupsTableName}`";
            public static string DeleteAllFromPermissions => $"DELETE FROM `{Settings.PermissionsTableName}`";
            public static string DeleteAllFromPlayers => $"DELETE FROM `{Settings.PlayerTableName}`";

            public static string DeleteGroupFromGroup(string groupId) => $"DELETE FROM `{Settings.GroupsTableName}` WHERE `groupid` = '{groupId}'";
            public static string DeleteGroupFromPermissions(string groupId) => $"DELETE FROM `{Settings.PermissionsTableName}` WHERE `groupid` = '{groupId}'";
            public static string DeleteGroupFromPlayers(string groupId) => $"DELETE FROM `{Settings.PlayerTableName}` WHERE `groupid` = '{groupId}'";

            public static string SelectGroupIdsByPlayer(string steamId) => $"SELECT `groupid` FROM `{Settings.PlayerTableName}` WHERE `csteamid` = '{steamId}'";
            public static string SelectPermissionsByGroupId(string groupId) => $"SELECT `permission`, `cooldown` FROM `{Settings.PermissionsTableName} WHERE `groupid` = '{groupId}'";
            public static string SelectGroupByGroupId(string groupId) => $"SELECT `groupname`, `parent`, `prefix`, `suffix`, `color`, `priority` FROM `{Settings.GroupsTableName}` WHERE `groupid` = '{groupId}'";

            public static string InsertPlayerToPlayers(string steamId, string groupId) => $"INSERT INTO `{Settings.PlayerTableName}` VALUES ('{steamId}', '{groupId}')";
            public static string DeletePlayerFromPlayers(string steamId, string groupId) => $"DELETE FROM `{Settings.PlayerTableName}'` WHERE `steamid` = '{steamId}' AND `groupid` = '{groupId}'";

            public static string GroupExists(string groupId) => $"SELECT EXISTS (SELECT * FROM `{Settings.GroupsTableName}` WHERE `groupid` = '{groupId}')";
            public static string PermissionExists(string permission, string groupId) => $"SELECT EXISTS (SELECT * FROM `{Settings.PermissionsTableName}` WHERE `groupid` = '{groupId}' AND `permission` = '{permission}')";
            public static string PlayerExists(string steamId, string groupId) => $"SELECT EXISTS (SELECT * FROM `{Settings.PlayerTableName}` WHERE `groupid` = '{groupId}' AND `csteamid` = '{steamId}')";

            public static string UpdateGroup(string groupId, string param, string value) => $"UPDATE `{Settings.GroupsTableName}` SET `{param}` = '{value}' WHERE `groupid` = '{groupId}'";
            public static string UpdatePermission(string groupId, string permission, string newCooldown) => $"UPDATE `{Settings.PermissionsTableName}` SET `cooldown` = '{newCooldown}' WHERE `groupid` = '{groupId}' AND `permission` = '{permission}'";

            #region Select Specific Groups
            public static string SelectGroupsByGroupIds(string[] ids)
            {
                if (ids != null)
                {
                    ids = ids.Where(x => x != null).ToArray();

                    if (ids.Length != 0)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"SELECT * FROM ");
                        sb.Append(Settings.GroupsTableName);
                        sb.Append(" WHERE");

                        for (int i = 0; i < ids.Length; i++)
                        {
                            if (i != 0)
                            {
                                sb.Append(" OR");
                            }

                            sb.Append(" `groupid` = '");
                            sb.Append(ids[i]);
                            sb.Append("'");
                        }

                        return sb.ToString();
                    }

                    throw new ArgumentException("The array must contain at least one non-null member!", nameof(ids));
                }

                throw new ArgumentNullException(nameof(ids));
            }

            public static string SelectPlayersByGroupIds(string[] ids)
            {
                if (ids != null)
                {
                    ids = ids.Where(x => x != null).ToArray();

                    if (ids.Length != 0)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"SELECT * FROM ");
                        sb.Append(Settings.PlayerTableName);
                        sb.Append(" WHERE");

                        for (int i = 0; i < ids.Length; i++)
                        {
                            if (i != 0)
                            {
                                sb.Append(" OR");
                            }

                            sb.Append(" `groupid` = '");
                            sb.Append(ids[i]);
                            sb.Append("'");
                        }

                        return sb.ToString();
                    }

                    throw new ArgumentException("The array must contain at least one non-null member!", nameof(ids));
                }

                throw new ArgumentNullException(nameof(ids));
            }

            public static string SelectPermissionsByGroupIds(string[] ids)
            {
                if (ids != null)
                {
                    ids = ids.Where(x => x != null).ToArray();

                    if (ids.Length != 0)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"SELECT * FROM ");
                        sb.Append(Settings.PermissionsTableName);
                        sb.Append(" WHERE");

                        for (int i = 0; i < ids.Length; i++)
                        {
                            if (i != 0)
                            {
                                sb.Append(" OR");
                            }

                            sb.Append(" `groupid` = '");
                            sb.Append(ids[i]);
                            sb.Append("'");
                        }

                        return sb.ToString();
                    }

                    throw new ArgumentException("The array must contain at least one non-null member!", nameof(ids));
                }

                throw new ArgumentNullException(nameof(ids));
            }
            #endregion

            #region Migration
            public static string InsertGroupsIntoGroups(List<RocketPermissionsGroup> groups)
            {
                if (groups != null)
                {
                    groups = groups.Where(x => x != null).ToList();

                    if (groups.Count != 0)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"INSERT INTO `");
                        sb.Append(Settings.GroupsTableName);
                        sb.Append("` VALUES");

                        for (int i = 0; i < groups.Count; i++)
                        {
                            RocketPermissionsGroup g = groups[i];

                            if (i != 0)
                            {
                                sb.Append(",");
                            }

                            sb.Append(" ('");
                            sb.Append(g.Id);
                            sb.Append(ValueSeperator);
                            sb.Append(g.DisplayName);
                            sb.Append(ValueSeperator);
                            sb.Append(g.ParentGroup);
                            sb.Append(ValueSeperator);
                            sb.Append(g.Prefix);
                            sb.Append(ValueSeperator);
                            sb.Append(g.Suffix);
                            sb.Append(ValueSeperator);
                            sb.Append(g.Color);
                            sb.Append(ValueSeperator);
                            sb.Append(g.Priority);
                            sb.Append("')");

                        }

                        return sb.ToString();
                    }

                    throw new ArgumentException("The array must contain at least one non-null member!", nameof(groups));
                }

                throw new ArgumentNullException(nameof(groups));
            }

            public static string InsertGroupsIntoPlayers(List<RocketPermissionsGroup> groups)
            {
                if (groups != null)
                {
                    groups = groups.Where(x => x != null).ToList();

                    if (groups.Count != 0)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"INSERT INTO `");
                        sb.Append(Settings.PlayerTableName);
                        sb.Append("` VALUES");

                        //Thank you .NET 3.5 for not having Tuples.
                        List<string> Players = new List<string>();
                        List<string> Groups = new List<string>();

                        for (int i = 0; i < groups.Count; i++)
                        {
                            if (groups[i].Members != null && groups[i].Members.Count != 0)
                            {
                                groups[i].Members = groups[i].Members.Where(x => x != null).ToList();

                                for (int ii = 0; ii < groups[i].Members.Count; ii++)
                                {
                                    Players.Add(groups[i].Members[ii]);
                                    Groups.Add(groups[i].Id);
                                }
                            }
                        }

                        for (int i = 0; i < Players.Count; i++)
                        {
                            if (i != 0)
                            {
                                sb.Append(",");
                            }

                            sb.Append(" ('");
                            sb.Append(Players[i]);
                            sb.Append(ValueSeperator);
                            sb.Append(Groups[i]);
                            sb.Append("')");
                        }

                        return sb.ToString();
                    }

                    throw new ArgumentException("The array must contain at least one non-null member!", nameof(groups));
                }

                throw new ArgumentNullException(nameof(groups));
            }

            public static string InsertGroupsIntoPermissions(List<RocketPermissionsGroup> groups)
            {
                if (groups != null)
                {
                    groups = groups.Where(x => x != null).ToList();

                    if (groups.Count != 0)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"INSERT INTO `");
                        sb.Append(Settings.PermissionsTableName);
                        sb.Append("` VALUES");

                        //Again, thank you .NET 3.5 for not having Tuples.
                        List<Permission> Permissions = new List<Permission>();
                        List<string> Groups = new List<string>();

                        for (int i = 0; i < groups.Count; i++)
                        {
                            if (groups[i].Permissions != null && groups[i].Permissions.Count != 0)
                            {
                                groups[i].Permissions = groups[i].Permissions.Where(x => x != null).ToList();

                                for (int ii = 0; ii < groups[i].Permissions.Count; ii++)
                                {
                                    Permissions.Add(groups[i].Permissions[ii]);
                                    Groups.Add(groups[i].Id);
                                }
                            }
                        }

                        for (int i = 0; i < Permissions.Count; i++)
                        {
                            if (i != 0)
                            {
                                sb.Append(",");
                            }

                            sb.Append(" ('");
                            sb.Append(Groups[i]);
                            sb.Append(ValueSeperator);
                            sb.Append(Permissions[i].Name);
                            sb.Append(ValueSeperator);
                            sb.Append(Permissions[i].Cooldown);
                            sb.Append("')");
                        }

                        return sb.ToString();
                    }

                    throw new ArgumentException("The array must contain at least one non-null member!", nameof(groups));
                }

                throw new ArgumentNullException(nameof(groups));
            }
            #endregion
        }
    }
}