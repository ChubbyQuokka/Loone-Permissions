using System;
using System.Reflection;
using System.Collections.Generic;

using Rocket.API;
using Rocket.API.Collections;
using Rocket.Core;
using Rocket.Core.Plugins;
using RocketLogger = Rocket.Core.Logging.Logger;

using UnityEngine;

using ChubbyQuokka.LoonePermissions.Hooks;
using ChubbyQuokka.LoonePermissions.Managers;
using ChubbyQuokka.LoonePermissions.Providers;

namespace ChubbyQuokka.LoonePermissions
{
    public sealed class LoonePermissionsPlugin : RocketPlugin<LoonePermissionsConfig>
    {
        internal static LoonePermissionsPlugin Instance { get; private set; }
        internal static Assembly GameAssembly { get; private set; }
        internal static Assembly RocketAssembly { get; private set; }
        internal static IGameHook GameHook { get; private set; }

        static List<IGameHook> hooks = new List<IGameHook>();
        internal static IRocketPermissionsProvider RocketPermissionProvider;
        static MySqlPermissionProvider Provider;

        protected override void Load()
        {
            Instance = this;

            hooks.Add(new UnturnedProvider());

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetName().Name == "Assembly-CSharp")
                {
                    GameAssembly = assemblies[i];
                    break;
                }
            }

            for (int i = 0; i < hooks.Count; i++)
            {
                for (int ii = 0; ii < assemblies.Length; ii++)
                {
                    if (hooks[i].DeterminingAssembly == assemblies[ii].GetName().Name)
                    {
                        GameHook = hooks[i];
                        RocketAssembly = assemblies[ii];
                        goto FoundRocketAssembly;
                    }
                }
            }

            Log("Failed to find any supported games! Please only use this plugin with supported games.");

            UnloadPlugin();
            return;

        FoundRocketAssembly:

            //Releasing the references to the extra hooks. (lol only one hook rn anyway)
            hooks.Clear();

            Log(string.Format("Welcome to Loone Permissions v{0}!", Assembly.GetName().Version), ConsoleColor.Yellow);
            Log(string.Format("Plugin is now running in compatibility mode with: {0}.", GameHook.DeterminingAssembly), ConsoleColor.Yellow);

            GameHook.Initialize();

            ThreadedWorkManager.Initialize();
            MySqlManager.Initialize();
            CommandManager.Initialize();

            Invoke("LateInit", 1f);

            if (LoonePermissionsConfig.CacheModeSettings.Enabled)
            {
                InitializeReshresh();
            }
        }

        protected override void Unload()
        {
            if (RocketPermissionProvider != null)
            {
                R.Permissions = RocketPermissionProvider;
                RocketPermissionProvider = null;
            }

            CancelInvoke();

            ThreadedWorkManager.Destroy();
            MySqlManager.Destroy();
            CommandManager.Destroy();

            GameHook = null;
            GameAssembly = null;
            RocketAssembly = null;

            Instance = null;
        }

        void Update()
        {
            ThreadedWorkManager.Update();
        }

        void LateInit()
        {
            RocketPermissionProvider = R.Permissions;
            Provider = new MySqlPermissionProvider();
            R.Permissions = Provider;
        }

        void InitializeReshresh()
        {
            MySqlManager.Refresh();

            Log($"LoonePermissions has cached your database, it will refresh its cache every {LoonePermissionsConfig.CacheModeSettings.SyncTime} milliseconds!", ConsoleColor.Yellow);

            float refreshTime = LoonePermissionsConfig.CacheModeSettings.SyncTime / 1000f;
            InvokeRepeating("Refresh", refreshTime, refreshTime);
        }

        void Refresh()
        {
            ThreadedWorkManager.EnqueueWorkerThread(MySqlManager.Refresh);
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { TranslationConstants.INVALID_ARGS, "You have specified an invalid arguement!" },
            { TranslationConstants.INVALID_PERMS, "You don't have permission to do this!" },
            { TranslationConstants.INVALID_CMD, "That commands doesn't exist!" },
            { TranslationConstants.INVALID_COLOR, "You specified an invalid color!" },
            { TranslationConstants.INVALID_NUM, "Please specify an actual number!" },
            { TranslationConstants.GROUP_CREATE, "You have created the group: {0}!" },
            { TranslationConstants.GROUP_DELETE, "You have deleted the group: {0}!" },
            { TranslationConstants.GROUP_DELETE_DEFAULT, "You can't delete the default group!" },
            { TranslationConstants.GROUP_DEFAULT, "You have changed the default group to {0}!" },
            { TranslationConstants.GROUP_DEFAULT_ALREADY, "This is already the default group!" },
            { TranslationConstants.GROUP_EXISTS, "That group already exists!" },
            { TranslationConstants.GROUP_NOT_EXISTS, "That group doesn't exist!" },
            { TranslationConstants.GROUP_MODIFIED, "You have set the {0} of {1} to {2}!" },
            { TranslationConstants.PERM_ADDED, "The permission {0} has been modified or added to {1} with a cooldown of {2}!" },
            { TranslationConstants.PERM_REMOVED, "The permission {0} has been removed from {1}!" },
            { TranslationConstants.PERM_EXISTS, "The group {0} already has the permission {1} with a cooldown of {2}! " },
            { TranslationConstants.PERM_NOT_EXISTS, "The group {0} doesn't have the permission {1}!" },
            { TranslationConstants.MIGRATE_START, "The migration from XML to MySQL has started!"},
            { TranslationConstants.MIGRATE_FINISH, "The migration has finished!"},
            { TranslationConstants.MIGRATE_FAIL, "The migration has failed!"}
        };

        internal static void Say(IRocketPlayer caller, string message, Color color, params object[] objs)
        {
            Action say = () =>
            {
                if (caller is ConsolePlayer)
                {
                    RocketLogger.Log(Instance.Translate(message, objs), ConsoleColor.Yellow);
                }
                else
                {
                    GameHook.Say(caller, Instance.Translate(message, objs), color);
                }
            };

            if (ThreadedWorkManager.IsWorkerThread)
            {
                ThreadedWorkManager.EnqueueMainThread(say);
            }
            else
            {
                say.Invoke();
            }
        }

        internal static void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            Action log = () =>
            {
                RocketLogger.Log(message, color);
            };

            if (ThreadedWorkManager.IsWorkerThread)
            {
                ThreadedWorkManager.EnqueueMainThread(log);
            }
            else
            {
                log.Invoke();
            }
        }

        internal static void LogException(Exception e)
        {
            Action log = () =>
            {
                RocketLogger.Log(e);
            };

            if (ThreadedWorkManager.IsWorkerThread)
            {
                ThreadedWorkManager.EnqueueMainThread(log);
            }
            else
            {
                log.Invoke();
            }
        }

        internal static class TranslationConstants
        {
            public const string INVALID_ARGS = "invalid_args";
            public const string INVALID_PERMS = "invalid_perms";
            public const string INVALID_CMD = "invalid_cmd";
            public const string INVALID_COLOR = "invalid_color";
            public const string INVALID_NUM = "invalid_num";
            public const string GROUP_CREATE = "group_create";
            public const string GROUP_DELETE = "group_delete";
            public const string GROUP_DELETE_DEFAULT = "group_delete_default";
            public const string GROUP_DEFAULT = "group_default";
            public const string GROUP_DEFAULT_ALREADY = "group_default_already";
            public const string GROUP_EXISTS = "group_exists";
            public const string GROUP_NOT_EXISTS = "group_not_exists";
            public const string GROUP_MODIFIED = "group_modified";
            public const string PERM_ADDED = "perm_added";
            public const string PERM_REMOVED = "perm_removed";
            public const string PERM_EXISTS = "perm_exists";
            public const string PERM_NOT_EXISTS = "perm_not_exists";
            public const string MIGRATE_START = "migrate_start";
            public const string MIGRATE_FINISH = "migrate_finish";
            public const string MIGRATE_FAIL = "migrate_fail";
        }
    }

    namespace Extensions
    {
        public static class StringCollectionExtensions
        {
            public static bool ContainsIgnoreCase(this IEnumerable<string> collection, string str)
            {
                foreach (string s in collection)
                {
                    if (str.Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}