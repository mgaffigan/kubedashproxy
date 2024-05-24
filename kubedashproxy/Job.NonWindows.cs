#if !WINDOWS

using System;

namespace Esatto.Win32.Processes;

public class Job : IDisposable
{
    public void AddProcess(System.Diagnostics.Process proc)
    {
        // nop
    }

    public void Dispose()
    {
        // nop
    }
}

#endif