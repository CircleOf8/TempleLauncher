using System;
using System.Collections.Generic;
using System.Text;

namespace TempleExtender
{
    internal static class IntPtrUtil
    {

        public static IntPtr Add(IntPtr ptr, int offset)
        {
            return new IntPtr(ptr.ToInt32() + offset);
        }

    }
}
