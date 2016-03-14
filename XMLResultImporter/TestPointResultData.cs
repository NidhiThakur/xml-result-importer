using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XMLResultImporter
{
    class TestPointResultData
    {
        // Field to store the test point outcome
        public bool Outcome { get; set; }

        // Field to store the test point run state
        public bool isTestRun { get; set; }

        public bool hasTestPoint { get; set; }

        // Field to store the info about failure( reason etc.). To be extended later
        public String Info { get; set; }

        public TestPointResultData(bool outcome, bool istestrun, bool foundTestPoint, String info = null)  
        {
            Outcome = outcome;
            isTestRun = istestrun;
            hasTestPoint = foundTestPoint;
        }  
    }
}
