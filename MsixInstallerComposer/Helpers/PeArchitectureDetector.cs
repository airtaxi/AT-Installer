using System;
using System.IO;

namespace MsixInstallerComposer.Helpers;

public static class PeArchitectureDetector
{
    private const ushort DosSignature = 0x5A4D;
    private const uint PeSignature = 0x00004550;
    private const int PeOffsetLocation = 0x3C;
    private const int MachineOffset = 4;

    public static string DetectArchitecture(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (stream.Length < MachineOffset + 2) return "Unknown";

        var dosSignature = reader.ReadUInt16();
        if (dosSignature != DosSignature) return "Unknown";

        stream.Seek(PeOffsetLocation, SeekOrigin.Begin);
        var peOffset = reader.ReadInt32();

        if (peOffset + 6 > stream.Length) return "Unknown";

        stream.Seek(peOffset, SeekOrigin.Begin);
        var peSignature = reader.ReadUInt32();
        if (peSignature != PeSignature) return "Unknown";

        var machine = reader.ReadUInt16();

        return machine switch
        {
            0x8664 => "x64",
            0x014C => "x86",
            0xAA64 => "ARM64",
            _ => "Unknown"
        };
    }
}