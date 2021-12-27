using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database
{
    public class Image
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Hash { get; set; }
        public virtual byte[] Data { get; set; }
    }
}
