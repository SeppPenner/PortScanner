// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GuiExtensions.cs" company="Hämmer Electronics">
//   Copyright (c) All rights reserved.
// </copyright>
// <summary>
//   The GUI extensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace PortScanner;

/// <summary>
/// The GUI extensions.
/// </summary>
public static class GuiExtensions
{
    /// <summary>
    /// Does an invocation from a background thread to the UI thread.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="code">the code.</param>
    public static void UiThreadInvoke(this Control control, Action code)
    {
        if (control.InvokeRequired)
        {
            control.Invoke(code);
            return;
        }

        code.Invoke();
    }
}
