//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/XbxDeTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using CommunityToolkit.HighPerformance.Buffers;

using System.Reflection;

namespace XbxDeTool;

public class Utils
{
    public static void CopyStreamRange(Stream inputStream, Stream outputStream, uint length)
    {
        const int BufferSize = 0x40000;

        long remSize = length;
        using MemoryOwner<byte> outBuffer = MemoryOwner<byte>.Allocate(BufferSize);

        while (remSize > 0)
        {
            int chunkSize = (int)Math.Min(remSize, BufferSize);
            Span<byte> chunk = outBuffer.Span.Slice(0, chunkSize);

            inputStream.ReadExactly(chunk);
            outputStream.Write(chunk);

            remSize -= chunkSize;
        }
    }

    public static string GetCurrentExecutingPath()
    {
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(assemblyLocation)) // This may be empty if we compiled the executable as single-file.
            assemblyLocation = Environment.GetCommandLineArgs()[0]!;

        return assemblyLocation;
    }
}
