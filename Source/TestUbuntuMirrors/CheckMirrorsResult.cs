using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUbuntuMirrors
{
    internal class CheckMirrorsResult
    {
        public int NrOfFailedMirrorsForThisRun { get; set; }
        public int NrOfWorkingMirrorsForThisRun { get; set; }

        public CheckMirrorsResult(int nrOfFailedMirrorsForThisRun, int nrOfWorkingMirrorsForThisRun)
        {
            NrOfFailedMirrorsForThisRun = nrOfFailedMirrorsForThisRun;
            NrOfWorkingMirrorsForThisRun = nrOfWorkingMirrorsForThisRun;
        }
    }
}
