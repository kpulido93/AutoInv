using Microsoft.Win32.TaskScheduler;

namespace AutoInventario.Helpers
{
    public static class TaskSchedulerHelper
    {
        public static void CreateStartupTask(string taskName, string exePath, string arguments)
        {
            using (TaskService ts = new TaskService())
            {
                bool needsUpdate = true;
                var existingTask = ts.GetTask(taskName);

                if (existingTask != null)
                {
                    var def = existingTask.Definition;

                    // Comprobar acción
                    var action = def.Actions.FirstOrDefault() as ExecAction;
                    bool sameAction = action != null &&
                                      action.Path == exePath &&
                                      (action.Arguments ?? "") == (arguments ?? "");

                    // Comprobar triggers: Logon y SessionUnlock
                    bool hasLogonTrigger = def.Triggers.OfType<LogonTrigger>().Any();
                    bool hasUnlockTrigger = def.Triggers
                        .OfType<SessionStateChangeTrigger>()
                        .Any(t => t.StateChange == TaskSessionStateChangeType.SessionUnlock);

                    bool sameTriggers = hasLogonTrigger && hasUnlockTrigger;

                    // Comprobar nivel de ejecución
                    bool samePrincipal = def.Principal.RunLevel == TaskRunLevel.Highest &&
                                         def.Principal.LogonType == TaskLogonType.ServiceAccount;

                    if (sameAction && sameTriggers && samePrincipal)
                        needsUpdate = false;
                }

                if (!needsUpdate) return;

                // Si necesita actualización, eliminar y crear nueva
                if (existingTask != null)
                    ts.RootFolder.DeleteTask(taskName);

                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "Ejecuta AutoInventario al iniciar sesión o al desbloquear el equipo";
                td.Principal.UserId = "SYSTEM";
                td.Principal.LogonType = TaskLogonType.ServiceAccount;
                td.Principal.RunLevel = TaskRunLevel.Highest;

                // Trigger: Logon
                td.Triggers.Add(new LogonTrigger
                {
                    UserId = null // cualquier usuario
                });

                // Trigger: Desbloqueo de sesión
                td.Triggers.Add(new SessionStateChangeTrigger
                {
                    StateChange = TaskSessionStateChangeType.SessionUnlock
                });

                td.Actions.Add(new ExecAction(exePath, arguments, null));

                ts.RootFolder.RegisterTaskDefinition(taskName, td);
            }
        }

        public static void DeleteTask(string taskName)
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(taskName, false);
            }
        }
    }
}
