using System.Text;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Draws a QR code as inline SVG on the server, so the authenticator setup link can be scanned
/// instead of typed. Nothing is sent to a third-party image service.
/// </summary>
public static class QrSvg
{
    public static string Render(string text)
    {
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = 2,
            [EncodeHintType.ERROR_CORRECTION] = ErrorCorrectionLevel.M
        };

        // A requested size of 0 returns one matrix cell per module, which the viewBox then scales.
        var matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, 0, 0, hints);
        var path = new StringBuilder();
        for (var y = 0; y < matrix.Height; y++)
        {
            for (var x = 0; x < matrix.Width; x++)
            {
                if (matrix[x, y])
                {
                    path.Append('M').Append(x).Append(' ').Append(y).Append("h1v1h-1z");
                }
            }
        }

        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {matrix.Width} {matrix.Height}\" shape-rendering=\"crispEdges\" role=\"img\" aria-label=\"Authenticator setup QR code\">"
            + $"<rect width=\"{matrix.Width}\" height=\"{matrix.Height}\" fill=\"#fff\"/><path d=\"{path}\" fill=\"#000\"/></svg>";
    }
}
