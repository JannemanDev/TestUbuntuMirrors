using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUbuntuMirrors
{
    public class UrlDownload : IEquatable<UrlDownload>
    {
        public DateTime DateTime { get; set; }
        public int TimeOutUsed { get; set; }
        public UrlCheckResult UrlCheckResult { get; set; }
        public string Url { get; set; }
        public string ErrorMessage { get; set; }

        public UrlDownload(DateTime dateTime, int timeOutUsed, UrlCheckResult mirrorCheckResult, string url, string errorMessage)
        {
            DateTime = dateTime;
            TimeOutUsed = timeOutUsed;
            UrlCheckResult = mirrorCheckResult;
            Url = url;
            ErrorMessage = errorMessage;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Url);
        }

        public bool Equals(UrlDownload? other)
        {
            return Url == other?.Url;
        }
    }
}
