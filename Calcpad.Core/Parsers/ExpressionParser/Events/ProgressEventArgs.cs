using System;

namespace Calcpad.Core
{
    public sealed class ProgressEventArgs(double value, string message) : EventArgs
    {
        public double Value { get; } = value;
        public string Message { get; } = message ?? string.Empty;
    }
}
