using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Runs the EditMode tests and exits the Editor with a status, for CI through game-ci's
/// unity-builder (buildMethod: CiTestRunner.RunEditModeTests). game-ci's test runner only
/// runs Unity in Docker (Linux, Windows), where the cwipc installed on the runner isn't
/// visible; the builder runs Unity directly on macOS runners. See cwi-dis/cwipc_unity#63.
///
/// Exit status: 0 all passed, 1 failures or no tests found.
/// Results (NUnit3 XML) go to $CWIPC_TEST_RESULTS, default test-results/editmode.xml in the project.
/// </summary>
public static class CiTestRunner
{
    class Callbacks : ICallbacks
    {
        public ITestResultAdaptor Result;
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result) { Result = result; }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
            {
                Debug.Log($"CiTestRunner: {result.ResultState}: {result.Test.FullName} {result.Message}");
            }
        }
    }

    public static void RunEditModeTests()
    {
        int status = 1;
        try
        {
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Callbacks callbacks = new Callbacks();
            api.RegisterCallbacks(callbacks);
            // Synchronous, so the results are in before this method returns and the
            // builder quits the Editor. Works for EditMode tests without coroutines.
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode })
            {
                runSynchronously = true
            });
            ITestResultAdaptor result = callbacks.Result;
            if (result == null)
            {
                Debug.LogError("CiTestRunner: the test run did not finish");
            }
            else
            {
                string path = Environment.GetEnvironmentVariable("CWIPC_TEST_RESULTS");
                if (string.IsNullOrEmpty(path)) path = Path.Combine("test-results", "editmode.xml");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                TestRunnerApi.SaveResultToFile(result, path);
                int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                Debug.Log($"CiTestRunner: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped, {result.InconclusiveCount} inconclusive; results in {Path.GetFullPath(path)}");
                if (total == 0)
                {
                    Debug.LogError("CiTestRunner: no tests found");
                }
                else if (result.FailCount == 0 && result.InconclusiveCount == 0)
                {
                    status = 0;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CiTestRunner: {e}");
        }
        ExitWithoutStaticDestructors(status);
    }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
    [DllImport("libc", EntryPoint = "_exit")]
    static extern void _exit(int status);
#endif

    /// <summary>
    /// Workaround, blocked on cwi-dis/cwipc#357: cwipc's native libraries link VTK, whose static
    /// destructors sometimes crash the process at exit (SIGSEGV after the tests passed), which
    /// would make CI fail at random. _exit skips the static destructors. Remove when CI tests
    /// against a cwipc without VTK (8.3.0).
    /// </summary>
    static void ExitWithoutStaticDestructors(int status)
    {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        Debug.Log($"CiTestRunner: exiting with status {status} (_exit, see cwi-dis/cwipc#357)");
        Console.Out.Flush();
        Console.Error.Flush();
        _exit(status);
#else
        EditorApplication.Exit(status);
#endif
    }
}
