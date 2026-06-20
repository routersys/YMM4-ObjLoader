using System.Runtime.ExceptionServices;
using System.Threading;

namespace ObjLoader.UnitTests.Infrastructure;

public static class StaTestHelper
{
    public static void RunInSTA(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
