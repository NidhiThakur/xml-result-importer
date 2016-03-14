using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.Framework.Client;
using System.IO;
namespace XMLResultImporter
{
    class Program
    {
        static bool QuerySuiteRecursively(ITestPlan plan, IStaticTestSuite topSuite, Dictionary<int, TestPointResultData> test_point_result_dictionary, ITestRun run)
        {
            bool returnVal = false,innerReturnVal = false;

            if (topSuite == null) return returnVal;
 
            Console.WriteLine("\tLooking in Test Suite: {0}...", topSuite.Title);

            ITestPointCollection testPoints = plan.QueryTestPoints(string.Format("SELECT * FROM TestPoint WHERE SuiteId = {0}", topSuite.Id));           

            foreach (ITestSuiteBase suite in topSuite.SubSuites)
            {
                IStaticTestSuite staticSuite = suite as IStaticTestSuite;
                innerReturnVal = QuerySuiteRecursively(plan, staticSuite, test_point_result_dictionary, run);
                if (innerReturnVal) returnVal = true;
            }            

            int number = testPoints.Count;
            if (0 == number) return returnVal;

            System.Linq.IQueryable<ITestPoint> linqQuery = testPoints.AsQueryable();
            foreach (int testID in test_point_result_dictionary.Keys)
            {
                if (test_point_result_dictionary[testID].hasTestPoint) continue;

                var mytestPoint = linqQuery.Where(point => point.TestCaseWorkItem.Id.Equals(testID));
                try
                {
                    int count = mytestPoint.Count();
                }
                catch (System.NullReferenceException)
                {
                    //No TestPoint Found. 
                    continue;
                }
                foreach (ITestPoint tp in mytestPoint)
                {
                    //Console.WriteLine("{0}", tp.TestCaseWorkItem.Title);
                    run.AddTestPoint(tp, null);
                    test_point_result_dictionary[testID].hasTestPoint = true;
                    // TODO: TFS PI Bug is not allowing multiple run.Save() right now so, the result can't be saved here. The result can be saved here to save time when that bug is fixed.
                    if (!returnVal) returnVal = true;
                }
            }

            return returnVal;
           
        }
        
        static void Main(string[] args)
        {
            int testPlanNum;
            if (args.Length != 5)
            {
                System.Console.WriteLine("Please enter the four arguments");
                System.Console.WriteLine("Usage: XMLResultImporter.exe <TFS Server> <Project Name> <Test Plan Number> <Full Path to input file> <Test Run Name>");
                return;
            }
            try
            {
                testPlanNum = int.Parse(args[2]);
                System.Console.WriteLine("Using TFS Server: {0}\nProject: {1}\nTest Plan: {2}\nPath: {3}\nTest Run Name{4}: ", args[0], args[1],testPlanNum, args[3],args[4]);
            }
            catch (System.FormatException)
            {
                System.Console.WriteLine("Please enter a numeric argument.");
                System.Console.WriteLine("Usage: XMLResultImporter.exe <TFS Server> <Project Name> <Test Plan Number> <Full Path to input file>");
                return;
            }

            //Dictionary to store the tfs Ids and corresponding results from the xml file
            Dictionary<int, TestPointResultData> test_point_result_dictionary = new Dictionary<int, TestPointResultData>();


            XmlReader reader = XmlReader.Create(args[3]);

            while (reader.Read())
            {
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == "test-case"))
                {
                    if (reader.HasAttributes)
                    {
                        //Console.WriteLine("Name :" + reader.GetAttribute("description") + "result :" + reader.GetAttribute("result").ToString());
                        string description = reader.GetAttribute("description");
                        bool outcome = false, executed = false; 
                        if (reader.GetAttribute("result").Equals("Success")) outcome = true;
                        if (reader.GetAttribute("executed").Equals("True")) executed = true; 
                        XmlReader inner = reader.ReadSubtree();
                        int tfsid = 0;                        
                        while (inner.Read())
                        {
                            if ((inner.NodeType == XmlNodeType.Element) && (inner.Name == "category"))
                            {
                                //Console.WriteLine("Tag :" + reader.GetAttribute("name") + "\n");                               
                                if (int.TryParse(reader.GetAttribute("name"), out tfsid))
                                    break;
                            }
                        }
                        
                        if(0!=tfsid)
                        {
                            
                            TestPointResultData myData = new TestPointResultData(outcome,executed, false);
                            //Console.WriteLine("Added {0} executed {1} outcome {2}", tfsid,myData.isTestRun, myData.Outcome); 
                            if (test_point_result_dictionary.ContainsKey(tfsid))
                            {
                                Console.WriteLine("Multiple tests found with Tag( TFS ID):" + tfsid);
                            }

                            test_point_result_dictionary.Add(tfsid, myData);
                        }
                        else
                            Console.WriteLine("No TFS ID tag found for {0}", description);
                    }
                }
            }

            Uri tfsUri = new Uri(args[0]);
            TfsTeamProjectCollection myTfsTeamProjectCollection = new TfsTeamProjectCollection(tfsUri);
            ITestManagementService tms = (ITestManagementService)myTfsTeamProjectCollection.GetService(typeof(ITestManagementService));


            ITestManagementTeamProject proj = null;
            
            string project = args[1]; 
            proj = tms.GetTeamProject(project);
            ITestPlanHelper planHelper = proj.TestPlans;
            ITestPlan foundPlan = null;
            try
            {
                foundPlan = proj.TestPlans.Find(testPlanNum); 
                Console.WriteLine("Got Plan {0} with Id {1}", foundPlan.Name, foundPlan.Id);
            }
            catch(System.NullReferenceException)
            {
                Console.WriteLine("No Plan with ID: {0} found!", testPlanNum);
                return;
            }
           

            
            //create a test run                
            ITestRun run = foundPlan.CreateTestRun(true);
            run.Title = args[4];
            bool atLeastOneTPFound = QuerySuiteRecursively(foundPlan, foundPlan.RootSuite, test_point_result_dictionary, run);

            foreach (ITestSuiteBase suite in foundPlan.RootSuite.SubSuites)
            {
                //Console.WriteLine("\tLooking in Test Suite: {0}...", suite.Title);

                IStaticTestSuite staticSuite = suite as IStaticTestSuite;

                ITestPointCollection testPoints = foundPlan.QueryTestPoints(string.Format("SELECT * FROM TestPoint WHERE SuiteId = {0}", suite.Id));
                int number = testPoints.Count;                
                System.Linq.IQueryable<ITestPoint> linqQuery = testPoints.AsQueryable();
                foreach (int testID in test_point_result_dictionary.Keys)
                {
                    if (test_point_result_dictionary[testID].hasTestPoint) continue;

                    var mytestPoint = linqQuery.Where(point => point.TestCaseWorkItem.Id.Equals(testID));
                    try
                    {
                        int count = mytestPoint.Count();
                    }
                    catch (System.NullReferenceException)
                    {
                        //No TestPoint Found. 
                        continue;
                    }
                    foreach (ITestPoint tp in mytestPoint)
                    {
                        //Console.WriteLine("{0}", tp.TestCaseWorkItem.Title);
                        run.AddTestPoint(tp, null);
                        test_point_result_dictionary[testID].hasTestPoint = true;
                        // TODO: TFS PI Bug is not allowing multiple run.Save() right now so, the result can't be saved here. The result can be saved here to save time when that bug is fixed.
                        if (!atLeastOneTPFound) atLeastOneTPFound = true;
                    }
                }
            }

            if(!atLeastOneTPFound)
            {
                Console.WriteLine("\nNo Matching TestPoints found in the Test Plan. Exiting...\n");
                Console.WriteLine("Troubleshooting:");
                Console.WriteLine("Verify the Test Points in the input file exist in at least one of the suites in this TestPlan");
                Console.WriteLine("\nXMLResultImporter : Job Complete\n");
                Console.ReadLine(); 
                return;
            }

            run.Save();

            ITestCaseResultCollection resultCollection = run.QueryResults();               
            foreach( ITestCaseResult result1 in resultCollection)
            {                
                int tfsid = result1.TestCaseId;        
                TestPointResultData value;
                if (0!= tfsid && test_point_result_dictionary.TryGetValue(tfsid, out value))
                {
                    result1.State = TestResultState.Completed; 
                    if(value.Outcome && value.isTestRun)
                        result1.Outcome = TestOutcome.Passed; 
                    else if(!value.isTestRun)
                        result1.Outcome = TestOutcome.NotExecuted;
                    else

                        result1.Outcome = TestOutcome.Failed;                        
                }
            }

            resultCollection.Save(false);
            

            // Optional: Iterate over results to diplay the Recorded results
            /*foreach (ITestCaseResult result1 in resultCollection)
            {
                ITestCase testcase = result1.GetTestCase();
                Console.WriteLine("Testcase {0} Outcome {1}", testcase.Implementation.DisplayText, result1.Outcome);
            }*/
            
            // Identify as tests in the input file which has no corresponding test cases in MTM Test Plan
            foreach (int key in test_point_result_dictionary.Keys)
            {
                TestPointResultData value = test_point_result_dictionary[key];
                bool firstWarning = true;
                if (value.hasTestPoint != true)
                {                    
                    if (value.isTestRun != false)
                    {
                        if (firstWarning)
                            Console.WriteLine("\nWarning(s):\n"); 
                        Console.WriteLine("No Result added for Test {0}. Make sure the corresponding test exists in TFS\n", key);
                    }
                }
            }

            Console.WriteLine("\nXMLResultImporter : Job Complete\n");
            Console.ReadLine();
            return;
        }

        
    }
}


 

