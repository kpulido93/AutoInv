using System.Runtime.InteropServices;

namespace AutoInventario.Helpers
{
    public static class ConsoleHelper
    {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        public static void ShowConsole()
        {

            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }
        }
    }
}
