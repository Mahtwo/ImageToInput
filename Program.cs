using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ImageToInput
{
    internal partial class Program
    {
        [LibraryImport("user32.dll")]
        private static partial IntPtr GetForegroundWindow();

        [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr GetDesktopWindow();

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [LibraryImport("user32.dll")]
        private static partial IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr hWnd);

        private const string vJoyInterfaceDllPath = @"C:\Program Files\vJoy\x64\vJoyInterface.dll";

        [LibraryImport(vJoyInterfaceDllPath, EntryPoint = "AcquireVJD", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AcquireVJD(uint rID);

        [LibraryImport(vJoyInterfaceDllPath, EntryPoint = "SetBtn", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetBtn(byte Value, uint rID, byte nBtn);

        [LibraryImport(vJoyInterfaceDllPath, EntryPoint = "ReleaseVJD", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ReleaseVJD(uint rID);

        // 1 is the first device ID
        public const uint vJoyDeviceID = 1;

        // Enum doesn't work as specifying as byte still makes it an int and need to be explicitly casted to byte
        public const byte ButtonA = 1;
        public const byte ButtonB = 2;
        public const byte ButtonX = 3;
        public const byte ButtonY = 4;
        public const byte ButtonL1 = 5;
        public const byte ButtonR1 = 6;
        public const byte ButtonL2 = 7;
        public const byte ButtonR2 = 8;

        // Process name of the window to capture screenshots from
        public const string ProcessName = "Process name (edit me!)";
        public static readonly Process process = Process.GetProcessesByName(ProcessName).ElementAtOrDefault(0);
        // Get pixels of original images to compare against
        public static readonly Color[] enemyPreparingAttack = GetAllPixels(new Bitmap("path/to/enemy preparing to attack.png"));
        public static readonly Color[] enemyDefending = GetAllPixels(new Bitmap("path/to/enemy defending.png"));

        private static readonly string backupDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), ProcessName);
        private static int backupID = 1; // Default value, will become the last backup ID + 1 if a backup exists
        private static Bitmap backupPreviousScreenshot = new(1, 1);

        public static Bitmap CaptureDesktop()
        {
            return CaptureWindow(GetDesktopWindow());
        }

        public static Bitmap CaptureActiveWindow()
        {
            return CaptureWindow(GetForegroundWindow());
        }

        public static Bitmap CaptureWindow(IntPtr handle)
        {
            Rect rect = new();
            GetWindowRect(handle, ref rect);
            Rectangle bounds = new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
            Bitmap result = new(bounds.Width, bounds.Height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
            }

            return result;
        }

        public static Bitmap CaptureWindow(IntPtr handle, int cropLeft, int cropTop, int width, int height)
        {
            Rect rect = new();
            GetWindowRect(handle, ref rect);
            Rectangle bounds = new(rect.Left + cropLeft, rect.Top + cropTop, width, height);
            Bitmap result = new(bounds.Width, bounds.Height);

            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
            }

            return result;
        }

        public static Color[] GetAllPixels(Bitmap bitmap)
        {
            Color[] pixels = new Color[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    pixels[y * bitmap.Width + x] = bitmap.GetPixel(x, y);
                }
            }
            return pixels;
        }

        // This is surprisingly fast enough for small images but an actually performant solution should probably use shaders/GPU
        // It also doesn't support searching an image within another image and instead only compares the whole image
        public static bool ComparePixels(Color[] original, Color[] screenshot, int margin = 5)
        {
            if (original.Length != screenshot.Length)
            {
                return false;
            }
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i].A == 255)
                {
                    // Depending on the sources, the colors may not be exactly the same. A margin is used to account for that
                    if (Math.Abs(original[i].R - screenshot[i].R) > margin ||
                        Math.Abs(original[i].G - screenshot[i].G) > margin ||
                        Math.Abs(original[i].B - screenshot[i].B) > margin)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void PressButton(uint vJoyDeviceID, byte button, int fps = 30)
        {
            int frameMs = 1000 / fps;
            SetBtn(1, vJoyDeviceID, button);
            // Sleeps aren't perfectly accurate but should be close enough
            Thread.Sleep(frameMs);
            SetBtn(0, vJoyDeviceID, button);
            // Sleep after releasing the button to ensure the release is also registered
            Thread.Sleep(frameMs);
        }

        public static void PressButton(uint vJoyDeviceID, byte[] buttons, int fps = 30)
        {
            int frameMs = 1000 / fps;
            foreach (byte button in buttons) { SetBtn(1, vJoyDeviceID, button); }
            Thread.Sleep(frameMs);
            foreach (byte button in buttons) { SetBtn(0, vJoyDeviceID, button); }
            Thread.Sleep(frameMs);
        }

        public static void HoldButton(uint vJoyDeviceID, byte button, int frames, int fps = 30)
        {
            int frameMs = 1000 / fps;
            int holdMs = frameMs * frames;
            SetBtn(1, vJoyDeviceID, button);
            Thread.Sleep(holdMs);
            SetBtn(0, vJoyDeviceID, button);
            Thread.Sleep(frameMs);
        }

        public static void HoldButton(uint vJoyDeviceID, byte[] buttons, int frames, int fps = 30)
        {
            int frameMs = 1000 / fps;
            int holdMs = frameMs * frames;
            foreach (byte button in buttons) { SetBtn(1, vJoyDeviceID, button); }
            Thread.Sleep(holdMs);
            foreach (byte button in buttons) { SetBtn(0, vJoyDeviceID, button); }
            Thread.Sleep(frameMs);
        }

        private static void InitializeBackupVariables()
        {
            if (Directory.Exists(backupDirectory))
            {
                int[] backupNumbers = [.. Directory.GetFiles(backupDirectory, "*.png")
                        .Select(file => Path.GetFileNameWithoutExtension(file))
                        .Select(fileName => int.TryParse(fileName, out int number) ? number : (int?)null)
                        .Where(number => number.HasValue)
                        .Select(number => number.Value)];
                if (backupNumbers.Length != 0)
                {
                    backupPreviousScreenshot = new Bitmap(Path.Combine(backupDirectory, $"{backupNumbers.Max()}.png"));
                    backupID = backupNumbers.Max() + 1;
                }
            }
            else
            {
                Directory.CreateDirectory(backupDirectory);
            }
        }

        private static bool CompareScreenshots(Bitmap original, Bitmap screenshot)
        {
            if (original.Width != screenshot.Width || original.Height != screenshot.Height)
            {
                return false;
            }
            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    if (original.GetPixel(x, y) != screenshot.GetPixel(x, y))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static void BackupScreenshot(Bitmap screenshot)
        {
            // Return without backup if the new screenshot is the same as the previous one
            if (CompareScreenshots(backupPreviousScreenshot, screenshot))
            {
                return;
            }
            string backupPath = Path.Combine(backupDirectory, $"{backupID}.png");
            screenshot.Save(backupPath, System.Drawing.Imaging.ImageFormat.Png);
            backupPreviousScreenshot = screenshot;
            backupID++;
        }

        public static void Heal(uint vJoyDeviceID)
        {
            PressButton(vJoyDeviceID, ButtonY);
            Thread.Sleep(500); // Wait for the menu animation to finish
            PressButton(vJoyDeviceID, [ButtonL1, ButtonA]); // Target player while healing
            // Exit menu
            PressButton(vJoyDeviceID, ButtonB);
            PressButton(vJoyDeviceID, ButtonB);
        }

        public static void Attack(uint vJoyDeviceID)
        {
            HoldButton(vJoyDeviceID, ButtonA, 8, 25); // Attack mode changes the frame rate to PAL (what kind of game does that?)
            // Spam B to flee after attacking
            for (int i = 0; i < 20; i++)
            {
                PressButton(vJoyDeviceID, ButtonB);
            }
        }

        static void Main()
        {
            if (!AcquireVJD(vJoyDeviceID))
            {
                Console.Error.WriteLine($"Error acquiring vJoy device. Error code: {Marshal.GetLastWin32Error()}");
                return;
            }

            // Ensure the vJoy device is always released on exit
            try
            {
                if (process == null)
                {
                    Console.Error.WriteLine($"Process {ProcessName} not found");
                    return;
                }
                // Activate window to foreground and focus it, with sleep so the window has time to show up
                SetForegroundWindow(process.MainWindowHandle);
                Thread.Sleep(200);

                InitializeBackupVariables();

                while (!process.HasExited)
                {
                    MainLoop();
                }
            }
            finally
            {
                if (!ReleaseVJD(vJoyDeviceID))
                {
                    Console.Error.WriteLine($"Error releasing vJoy device. Error code: {Marshal.GetLastWin32Error()}");
                }
            }
        }

        private static void MainLoop()
        {
            Bitmap screenshotBitmap = CaptureWindow(process.MainWindowHandle);
            BackupScreenshot(screenshotBitmap);
            Color[] screenshot = GetAllPixels(screenshotBitmap);
            if (ComparePixels(enemyPreparingAttack, screenshot, 5))
            {
                Attack(vJoyDeviceID);
                Console.WriteLine("Attack");
                return;
            }
            if (ComparePixels(enemyDefending, screenshot, 5))
            {
                Heal(vJoyDeviceID);
                Console.WriteLine("Heal");
                return;
            }
            Console.WriteLine("No action");
        }
    }
}
