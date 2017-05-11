using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Кодер_LZW
{
    class ListEqualityComparer : IEqualityComparer<List<byte>>
    {
        public bool Equals(List<byte> x, List<byte> y)
        {
            if (x.Count != y.Count)
                return false;
            else
            {
                return x.SequenceEqual(y);
            }


        }

        public int GetHashCode(List<byte> obj)
        {
            unchecked
            {
                int hash = 19;
                foreach (byte b in obj)
                {
                    hash = hash * 31 + b.GetHashCode();
                }
                return hash;
            }
            
        }
    }
}
