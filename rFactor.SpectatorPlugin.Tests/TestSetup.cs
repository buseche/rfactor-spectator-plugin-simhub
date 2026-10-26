using System.Runtime.CompilerServices;
using System.Text;

namespace User.RfSpectator.Tests
{
    internal static class TestSetup
    {
        // RfParser decodes driver names as cp1252, which .NET Core only offers through this provider.
        [ModuleInitializer]
        internal static void RegisterCodePages()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }
}
