using System.Runtime.InteropServices;
using System.Text;

namespace AvatarFormsApp.Helpers;

public class RuntimeHelper
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    // Make this property settable for unit testing purposes
    public static bool IsMSIX { get; set; } = DetermineIfMSIX();

    private static bool DetermineIfMSIX()
    {
        var length = 0;
        return GetCurrentPackageFullName(ref length, null) != 15700L;
    }
}
