using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Hashing;

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
