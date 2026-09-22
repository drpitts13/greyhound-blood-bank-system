namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Renderer output. Text formats (ZPL, on-screen proof) use <see cref="Text"/>.
/// PDF uses <see cref="Pdf"/> bytes. <see cref="MediaType"/> says which payload is set.
/// </summary>
public sealed record LabelRenderResult(string? Text, byte[]? Pdf, string MediaType)
{
    public static LabelRenderResult TextContent(string text, string mediaType = "text/plain") =>
        new(text, null, mediaType);

    public static LabelRenderResult PdfContent(byte[] pdf) =>
        new(null, pdf, "application/pdf");
}
