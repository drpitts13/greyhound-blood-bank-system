using System.Runtime.InteropServices;
using BloodBankLIS.Printing.Delivery;

namespace BloodBankLIS.Api.Printing;

/// <summary>Sends raw bytes (ZPL) to a Windows print queue so the driver does not rewrite the language.</summary>
public sealed class WindowsRawSpooler : IRawSpooler
{
    public void Write(string printerName, byte[] bytes)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Raw printing requires Windows.");
        }

        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            throw new InvalidOperationException($"Printer '{printerName}' could not be opened (Win32 {Marshal.GetLastWin32Error()}).");
        }

        try
        {
            var info = new DocInfo
            {
                DocumentName = "Greyhound Blood Bank label",
                DataType = "RAW"
            };
            if (StartDocPrinter(handle, 1, info) == 0)
            {
                throw new InvalidOperationException($"Could not start a raw document on '{printerName}' (Win32 {Marshal.GetLastWin32Error()}).");
            }

            try
            {
                if (!StartPagePrinter(handle))
                {
                    throw new InvalidOperationException($"Could not start a page on '{printerName}'.");
                }

                if (!WritePrinter(handle, bytes, bytes.Length, out _))
                {
                    throw new InvalidOperationException($"Could not write ZPL to '{printerName}' (Win32 {Marshal.GetLastWin32Error()}).");
                }

                EndPagePrinter(handle);
            }
            finally
            {
                EndDocPrinter(handle);
            }
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        public string DocumentName = string.Empty;
        public string? OutputFile;
        public string DataType = "RAW";
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, DocInfo pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);
}
