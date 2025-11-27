using Microsoft.VisualStudio.Extensibility.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAlign.Services;
#pragma warning disable VSEXTPREVIEW_OUTPUTWINDOW

public static class OutputChannelExtensions
{

    public static async Task LogInfoAsync(this OutputChannel outputChannel, string message)
    {
        await outputChannel.WriteLineAsync(message);
    }
}
