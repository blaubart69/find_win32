using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi
{
    public class StringTools
    {
        public static bool Contains_OrdinalIgnoreCase(IEnumerable<string> Items, string ValueToSearch)
        {
            if (Items == null)
            {
                return false;
            }

            return Items.Any(item =>
            {
                if (item == null && ValueToSearch == null)
                {
                    return true;
                }
                else
                {
                    if (item == null)
                    {
                        return false;   // ValueToSearch != null ==> false
                    }
                    else
                    {
                        return item.Equals(ValueToSearch, StringComparison.OrdinalIgnoreCase);
                    }
                }
            });
        }
    }
}
