using System;
using System.Collections;

namespace SpringExpressions.Core.TypeResolution.SafeProxies
{
       // todo: rejestracja!
    internal static class SystemEnvironment
    {
        public static string CommandLine => System.Environment.CommandLine;
        public static string ExpandEnvironmentVariables(string name) => Environment.ExpandEnvironmentVariables(name);
        public static string MachineName => Environment.MachineName;
        public static int ProcessorCount => Environment.ProcessorCount;
        public static int SystemPageSize => Environment.SystemPageSize;
        public static string[] GetCommandLineArgs() => Environment.GetCommandLineArgs();
        public static string GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
        public static string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target) 
            => Environment.GetEnvironmentVariable(variable, target);
        public static IDictionary GetEnvironmentVariables() => Environment.GetEnvironmentVariables();
        public static IDictionary GetEnvironmentVariables(EnvironmentVariableTarget target)
            => Environment.GetEnvironmentVariables(target);

        public static string NewLine => Environment.NewLine;

        public static long WorkingSet => Environment.WorkingSet;
        public static OperatingSystem OSVersion => Environment.OSVersion;
        public static string StackTrace => Environment.StackTrace;
        public static bool Is64BitProcess => Environment.Is64BitProcess;
        public static bool Is64BitOperatingSystem => Environment.Is64BitOperatingSystem;

        public static bool UserInteractive => Environment.UserInteractive;

           // todo: zablokować jakoś inne typy!

        public static void chuj()
        {
            //            "System.Windows.Forms.Application"
            //            System.IO.File.Create()
            //           System.IO.Directory
            // System.Threading.Thread.Resume
            // System.Threading.Thread.Suspend

            // System.Reflection.Assembly.LoadFile

            // new System.Diagnostics.Process()
            // new System.Diagnostics.Process()
        }
    }
}
