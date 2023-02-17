using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanMarkdownFiles
{

    //Pay attention to boolean flags (called Switches)
    //https://github.com/commandlineparser/commandline/wiki/CommandLine-Grammar#switch-option
    class Arguments
    {
        [Option(Required = true, HelpText = "Linux distribution for example: lunar, jammy, impish, hirsute, groovy, focal, bionic")]
        public string Distribution { get; set; }

        [Option(Required = true, HelpText = "Architecture for example: arm64, armhf, ppc64el, riscv64, s390x, powerpc, amd64, i386")]
        public string Architecture { get; set; } = "";

        [Option(Required = false, Default = 2, HelpText = "Timeout in seconds")]
        public int Timeout { get; set; }        
        
        [Option(Required = false, Default = int.MaxValue, HelpText = "Max mirrors to check")]
        public int MaxMirrorsToCheck { get; set; }

        [Option(Required = false, Default = false, HelpText = "Always redownload mirrors for countries, even if available from cache")]
        public bool AlwaysRedownloadMirrorsForCountries { get; set; }

        [Option(Required = false, Default = false, HelpText = "Ignore previous mirror check results when checking if packages for given distribution and architecture are available")]
        public bool IgnorePreviousMirrorCheckResults { get; set; }

        [Option(Required = false, Default = false, HelpText = "Only retry failed mirrors which had a lower timeout in previous run than given timeout. Only works when switch --RetryFailedMirrorsWithTimeOut is set")]
        public bool OnlyRetryFailedMirrorsWithLowerTimeout { get; set; }
        
        [Option(Required = false, Default = false, HelpText = "Retry failed mirrors which had a timeout error")]
        public bool RetryFailedMirrorsWithTimeOut { get; set; }              
        
        [Option(Required = false, Default = false, HelpText = "Do not export working mirrors to plain text file, for example for use as input in netselect")]
        public bool DoNotExportWorkingMirrorsToTextFile { get; set; }        
        
        [Option(Required = false, Default = null, HelpText = "Use specified file as input for mirrors to be checked")]
        public string? UseFileAsInputForMirrors { get; set; }

        [Option(Required = false, Default = "http://mirrors.ubuntu.com/", HelpText = "Url of website containing mirrors for each country. Website must contain .txt file(s) for each country using official 2 letter country codes.")]
        public string UrlMirrors { get; set; }

        [Option(Required = false, HelpText = "Filter mirror country files for example:\n --CountryMirrorFilters NL EN")]
        public IEnumerable<string> CountryMirrorFilters { get; set; } = new List<string>();
    }
}
