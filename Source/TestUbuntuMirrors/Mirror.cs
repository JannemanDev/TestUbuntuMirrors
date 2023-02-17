using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUbuntuMirrors
{
    public class Mirror
    {
        public string Url { get; set; }
        public string CountryCode { get; set; }

        public Mirror(string url, string countryCode)
        {
            Url = url;
            CountryCode = countryCode;
        }
    }
}
