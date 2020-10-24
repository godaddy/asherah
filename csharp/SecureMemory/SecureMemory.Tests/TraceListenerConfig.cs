using System.Diagnostics;

namespace GoDaddy.Asherah.SecureMemory.Tests
{
    internal static class TraceListenerConfig
    {
        static TraceListenerConfig()
        {
            Trace.Listeners.Clear();
            var consoleListener = new ConsoleTraceListener();
            Trace.Listeners.Add(consoleListener);
        }

        internal static void ConfigureTraceListener()
        {
        }
    }
}
