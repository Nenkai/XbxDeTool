//---------------------------------------------------------------------------------------
// SPDX-FileCopyrightText: Copyright (C) 2026 Nenkai
// SPDX-License-Identifier: MIT
// SPDX-FileAttributionText: https://github.com/Nenkai/XbxDeTool
// SPDX-FileNotice: AI usage and consumption is prohibited. See AGENTS.md for policy.
//---------------------------------------------------------------------------------------

using System.Text;

namespace XbxDeTool.Hashing;

public class XxHash64
{
    public static ulong HashPath(string str)
    {
        str = str.Replace('\\', '/').ToLower();
        if (!str.StartsWith('/'))
            str = '/' + str;

        byte[] bytes = Encoding.ASCII.GetBytes(str);
        return System.IO.Hashing.XxHash64.HashToUInt64(bytes);
    }
}
