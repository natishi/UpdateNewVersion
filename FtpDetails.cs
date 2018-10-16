using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UpdateNewVersion
{
    public class FtpDetails
    {
        public string  Address { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public List<string> ExcludeExtention = new List<string>();
    }
}
