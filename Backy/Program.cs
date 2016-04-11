using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using CefSharp;
using CefSharp.Internals;
using CefSharp.OffScreen;

namespace Backy
{
    class Program
    {
        [DllImport("User32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("User32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        static Rectangle screenBounds = new Rectangle(new Point(), new Size());

        static void Main(string[] args)
        {
            DesktopDrawer drawer = new DesktopDrawer();

            CefSettings settings = new CefSettings
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/48.0.2564.109 Safari/537.36",
                WindowlessRenderingEnabled = false,
                CachePath = "BrowserCache"
            };

            settings.CefCommandLineArgs.Add("disable-gpu-compositing", "1");
            settings.CefCommandLineArgs.Add("enable-begin-frame-scheduling", "1");
            settings.CefCommandLineArgs.Add("disable-gesture-requirement-for-media-playback", "1");
            settings.CefCommandLineArgs.Add("disable-gesture-requirement-for-presentation", "1");

            Cef.Initialize(settings, shutdownOnProcessExit: true, performDependencyCheck: true);

            BrowserSettings browserSettings = new BrowserSettings
            {
                OffScreenTransparentBackground = false,
                FileAccessFromFileUrls = CefState.Enabled,
                UniversalAccessFromFileUrls = CefState.Enabled,
                JavascriptOpenWindows = CefState.Disabled,
                WindowlessFrameRate = 60,
                WebGl = CefState.Enabled
            };

            ChromiumWebBrowser browser = new ChromiumWebBrowser("http://google.com/", browserSettings);
            
            browser.Size = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

            Graphics graph = Graphics.FromHdc(drawer.DesktopHandle);
            
            graph.CompositingMode = CompositingMode.SourceCopy;
            graph.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
            graph.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graph.CompositingQuality = CompositingQuality.HighSpeed;
            graph.SmoothingMode = SmoothingMode.HighSpeed;
            graph.InterpolationMode = InterpolationMode.NearestNeighbor;

            Thread thread = new Thread(new ThreadStart(delegate
            {
                while (browser != null && graph != null)
                {
                    lock (browser.BitmapLock)
                    {
                        Bitmap screenBitmap = browser.Bitmap;

                        if (screenBitmap != null)
                        {
                            graph.DrawImageUnscaled(screenBitmap, new Point(screenBounds.X, 0));
                        }

                        Thread.Sleep(15);
                    }
                }

            })); 

            thread.Start();
            

            Console.WriteLine("Type a url (starting with http) to change pages.");
            Console.WriteLine("Type a single number (1-9) to switch to that display.");
            Console.WriteLine("Type -1 and hit enter to span across all displays");
            Console.WriteLine();
            Console.WriteLine("Commands");
            Console.WriteLine("Type fs [element id] to fullscreen an element.");
            Console.WriteLine("Type js [script] to run a line of javascript.");
            Console.WriteLine("Type jsf [path to filename.js] to run a javascript file.");
            Console.WriteLine("Type \"quit\" to quit.");
            Console.WriteLine();

            string input = "";

            while (input != "quit")
            {
                input = Console.ReadLine();

                if (input.StartsWith("http"))
                {
                    browser.Load(input);
                    continue;
                }

                if (input.StartsWith("js"))
                {
                    string lastArg = input.Split(new[] { ' ' }, 2)[1];
                    browser.ExecuteScriptAsync(lastArg);
                    continue;
                }

                if (input.StartsWith("jsf"))
                {
                    string lastArg = input.Split(new[] { ' ' }, 2)[1];
                    browser.ExecuteScriptAsync(File.ReadAllText(lastArg));
                    continue;
                }

                if (input.StartsWith("fs"))
                {
                    string lastArg = input.Split(new[] { ' ' }, 2)[1];

                    //Hackiness to get around Chromium's user gesture fullscreen limitations...

                    browser.ExecuteScriptAsync($"var elementid = '{lastArg}';" + File.ReadAllText("fullscreen.js"));

                    Thread.Sleep(500);

                    browser.GetBrowser().GetHost().SendKeyEvent(new KeyEvent
                    {
                        FocusOnEditableField = false,
                        IsSystemKey = true,
                        NativeKeyCode = 70,
                        WindowsKeyCode = 46,
                        Type = KeyEventType.KeyDown
                    });

                    continue;
                }

                int screenIndex = 1;

                if (Int32.TryParse(input, out screenIndex))
                {
                    if (screenIndex == -1)
                    {
                        screenBounds = new Rectangle(0, 0, Screen.AllScreens.Sum(d => d.Bounds.Width), Screen.AllScreens.Sum(d => d.Bounds.Height));
                    }
                    else
                    {
                        if (screenIndex < 1 || screenIndex > Screen.AllScreens.Length)
                        {
                            Console.WriteLine("Invalid screen index. Must be -1 for span, or between 1 and {0}", Screen.AllScreens.Length);
                        }
                        else
                        {
                            screenBounds = Screen.AllScreens[screenIndex - 1].Bounds;
                        }
                    }
                }

                if (screenBounds.Width != browser.Size.Width || screenBounds.Height != browser.Size.Height)
                {
                    browser.Size = new Size(screenBounds.Width, screenBounds.Height);
                }
            }

            browser.Dispose();
            browser = null;
            //graph.Dispose();
            Cef.Shutdown();
        }

    }
}
