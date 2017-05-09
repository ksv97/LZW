using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Кодер_LZW
{
    class Chain
    {
        public List<byte> ChainBytes { get; set; }

        public Chain()
        {
            ChainBytes = new List<byte>();
        }

        

        public override bool Equals(object obj)
        {
            return ChainBytes.Equals(obj);
        }

        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        public override string ToString()
        {
            string str = "";
            foreach (byte b in ChainBytes)
                str += b;
            return str;
        }
    }
}
