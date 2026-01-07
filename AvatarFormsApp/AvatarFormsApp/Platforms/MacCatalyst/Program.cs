using UIKit;
using Uno.UI.Hosting;

namespace AvatarFormsApp;

internal class Program
{
    static void Main(string[] args)
    {
        // This is the magic line for MacCatalyst/iOS
        // It tells Apple's native layer to start the app and use 'App' as the delegate
        UIApplication.Main(args, null, typeof(App));
    }
}