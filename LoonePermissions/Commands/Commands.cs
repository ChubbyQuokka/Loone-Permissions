using System;

using Rocket.API;

using UnityEngine;

using ChubbyQuokka.LoonePermissions.Managers;
using ChubbyQuokka.LoonePermissions.API;
using Rocket.API.Serialisation;

namespace ChubbyQuokka.LoonePermissions.Commands
{
    /*
    public class CommandGroup : ILooneCommand
    {
        public string Help => "Modifies a group!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            if (args.Length != 3)
            {
                LoonePermissionsPlugin.Say(caller, "invalid_args", Color.red);
                return;
            }

            string groupId = args[0].ToLower();
            string modify = args[1].ToLower();
            string value = args[2];

            if (!MySqlManager.GroupExists(groupId))
            {
                LoonePermissionsPlugin.Say(caller, "group_not_exists", Color.red);
                return;
            }

            switch (modify)
            {
                case "name":
                    MySqlManager.UpdateGroup(EGroupProperty.NAME, value, groupId);
                    LoonePermissionsPlugin.Say(caller, "group_modified", Color.green, modify, groupId, value);
                    break;
                case "parent":
                    MySqlManager.UpdateGroup(EGroupProperty.PARENT, value, groupId);
                    LoonePermissionsPlugin.Say(caller, "group_modified", Color.green, modify, groupId, value);
                    break;
                case "prefix":
                    MySqlManager.UpdateGroup(EGroupProperty.PREFIX, value, groupId);
                    LoonePermissionsPlugin.Say(caller, "group_modified", Color.green, modify, groupId, value);
                    break;
                case "suffix":
                    MySqlManager.UpdateGroup(EGroupProperty.SUFFIX, value, groupId);
                    LoonePermissionsPlugin.Say(caller, "group_modified", Color.green, modify, groupId, value);
                    break;
                case "color":
                    if (!MySqlManager.UpdateGroup(EGroupProperty.COLOR, value, groupId))
                        LoonePermissionsPlugin.Say(caller, "invalid_color", Color.red);
                    else
                        LoonePermissionsPlugin.Say(caller, "group_modified", Color.green, modify, groupId, value);
                    break;
                case "priority":
                    if (!MySqlManager.UpdateGroup(EGroupProperty.PRIORITY, value, groupId))
                        LoonePermissionsPlugin.Say(caller, "invalid_num", Color.red);
                    else
                        LoonePermissionsPlugin.Say(caller, "group_modified", Color.green, modify, groupId, value);
                    break;
                default:
                    LoonePermissionsPlugin.Say(caller, "invalid_args", Color.red);
                    break;
            }

            LoonePermissionsPlugin.Log(string.Format("{0} has set the {1} of {2} to {3}!", caller.DisplayName, modify, groupId, value), ConsoleColor.Yellow);
        }
    }

*/
    public class CommandDelete : ILooneCommand
    {
        public string Help => "Deletes a group!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            if (args.Length != 1)
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.INVALID_ARGS, Color.red);
                return;
            }

            if (!MySqlManager.GroupExists(args[0]))
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_NOT_EXISTS, Color.red);
                return;
            }

            if (LoonePermissionsConfig.DefaultGroup.Equals(args[0], StringComparison.InvariantCultureIgnoreCase))
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_DELETE_DEFAULT, Color.red);
                return;
            }

            MySqlManager.DeleteGroup(args[0]);
            LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_DELETE, Color.green, args[0], LoonePermissionsConfig.DefaultGroup);
            LoonePermissionsPlugin.Log(string.Format("{0} deleted the group {1}!", caller.DisplayName, args[0].ToLower()), ConsoleColor.Yellow);
        }
    }

    /*
    public class CommandCreate : ILooneCommand
    {
        public string Help => "Creates a new group!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            if (args.Length != 1)
            {
                LoonePermissionsPlugin.Say(caller, "invalid_args", Color.red);
                return;
            }

            if (MySqlManager.GroupExists(args[0].ToLower()))
            {
                LoonePermissionsPlugin.Say(caller, "group_exists", Color.red);
                return;
            }

            //MySqlManager.CreateGroup(args[0].ToLower());
            LoonePermissionsPlugin.Say(caller, "group_create", Color.green, args[0].ToLower());
            LoonePermissionsPlugin.Log(string.Format("{0} created the group {1}!", caller.DisplayName, args[0].ToLower()), ConsoleColor.Yellow);
        }
    }
    */

    public class CommandDefault : ILooneCommand
    {
        public string Help => "Sets the default group!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            if (args.Length != 1)
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.INVALID_ARGS, Color.red);
                return;
            }

            if (!MySqlManager.GroupExists(args[0]))
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_NOT_EXISTS, Color.red);
                return;
            }

            if (LoonePermissionsConfig.DefaultGroup.Equals(args[0], StringComparison.InvariantCultureIgnoreCase))
            {
                LoonePermissionsPlugin.Say(caller, "group_default_already", Color.red);
                return;
            }

            LoonePermissionsConfig.SetDefaultGroup(args[0]);
            LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_DEFAULT, Color.green, args[0]);
            LoonePermissionsPlugin.Log(string.Format("{0} set the default group to {1}!", caller.DisplayName, args[0]), ConsoleColor.Yellow);
        }
    }

    public class CommandAdd : ILooneCommand
    {
        public string Help => "Adds a permission to a group!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            if (args.Length != 2 && args.Length != 3)
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.INVALID_ARGS, Color.red);
                return;
            }

            if (!MySqlManager.GroupExists(args[0].ToLower()))
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_NOT_EXISTS, Color.red);
                return;
            }

            string groupId = args[0];
            string perm = args[1];

            uint cooldown;

            if (args.Length == 3)
            {
                if (!uint.TryParse(args[2], out cooldown))
                {
                    LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.INVALID_ARGS, Color.red);
                    return;
                }
            }
            else
            {
                cooldown = 0;
            }

            MySqlManager.AddPermissionToGroup(new Permission(perm, cooldown), groupId);
            LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.PERM_ADDED, Color.green, perm, groupId, cooldown);
            LoonePermissionsPlugin.Log(string.Format("{0} added the permission {1} with a cooldown of {2} to {3}!", caller.DisplayName, perm, cooldown, groupId), ConsoleColor.Yellow);
        }
    }

    public class CommandRemove : ILooneCommand
    {
        public string Help => "Deletes a permission from a group!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            if (args.Length != 2)
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.INVALID_ARGS, Color.red);
                return;
            }

            string groupId = args[0];
            string perm = args[1];

            if (!MySqlManager.GroupExists(groupId))
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.GROUP_NOT_EXISTS, Color.red);
                return;
            }

            if (MySqlManager.PermissionExists(perm, groupId))
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.PERM_NOT_EXISTS, Color.red, groupId, perm);
                return;
            }

            MySqlManager.DeletePermissionFromGroup(perm, groupId);
            LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.PERM_REMOVED, Color.green, perm, groupId);
            LoonePermissionsPlugin.Log(string.Format("{0} removed the permission {1} from {2}!", caller.DisplayName, perm, groupId), ConsoleColor.Yellow);
        }
    }

    public class CommandMigrate : ILooneCommand
    {
        public string Help => "Migrates all data from the XML file to MySQL!";

        public void Excecute(IRocketPlayer caller, string[] args)
        {
            LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.MIGRATE_START, Color.green);

            try
            {
                MySqlManager.Migrate();
            }
            catch (Exception e)
            {
                LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.MIGRATE_FAIL, Color.red);
                LoonePermissionsPlugin.LogException(e);
            }

            LoonePermissionsPlugin.Say(caller, LoonePermissionsPlugin.TranslationConstants.MIGRATE_FINISH, Color.green);
        }
    }
}
