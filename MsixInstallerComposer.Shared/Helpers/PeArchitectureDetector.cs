using System;
using System.IO;
using System.Reflection.PortableExecutable;

namespace MsixInstallerComposer.Shared.Helpers;

public static class PeArchitectureDetector
{
    public const string NeutralArchitecture = "Neutral";
    public const string UnknownArchitecture = "Unknown";

    /// <summary>
    /// Detects the architecture of a PE image and returns "x64", "x86", "ARM64", <see cref="NeutralArchitecture"/>, or <see cref="UnknownArchitecture"/>.
    /// IL-only managed images built for AnyCPU without the 32-bit requirement are reported as <see cref="NeutralArchitecture"/>.
    /// </summary>
    public static string DetectArchitecture(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var peReader = new PEReader(stream);

        try
        {
            var headers = peReader.PEHeaders;
            return ClassifyArchitecture(headers.CoffHeader.Machine, headers.CorHeader?.Flags);
        }
        catch (BadImageFormatException) { return UnknownArchitecture; }
    }

    private static string ClassifyArchitecture(Machine machine, CorFlags? corFlags)
    {
        // AnyCPU IL-only images carry the I386 machine value, so the CLR flags decide whether the image is bound to 32-bit.
        // Prefer 32-bit builds also set Requires32Bit and run as x86 processes.
        if (corFlags is { } flags && flags.HasFlag(CorFlags.ILOnly) && machine == Machine.I386) return flags.HasFlag(CorFlags.Requires32Bit) ? "x86" : NeutralArchitecture;

        return machine switch
        {
            Machine.Amd64 => "x64",
            Machine.I386 => "x86",
            Machine.Arm64 => "ARM64",
            _ => UnknownArchitecture
        };
    }
}
