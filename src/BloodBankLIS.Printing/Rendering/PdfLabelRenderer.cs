using BloodBankLIS.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace BloodBankLIS.Printing.Rendering;

/// <summary>
/// Renders a <see cref="LabelDocument"/> to a one-page PDF at the template's 203 dpi
/// size. Used for on-screen preview. Does not talk to a printer.
/// </summary>
public sealed class PdfLabelRenderer : ILabelRenderer
{
    private const float DotsPerInch = 203f;
    private const float PointsPerInch = 72f;

    static PdfLabelRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
    }

    public LabelFormat Format => LabelFormat.Pdf;

    public LabelRenderResult Render(LabelDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var searchable = string.Join(' ', document.Elements.Concat(document.Page2Elements).Select(element => element switch
        {
            LabelText text => text.Value,
            LabelBarcode barcode => barcode.Data,
            _ => string.Empty
        }).Where(value => !string.IsNullOrWhiteSpace(value)));

        var titleSource = document.CheckForm is { } form
            ? form.Attestation + " Signature Date " + searchable
            : searchable;
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(9).FontColor(Colors.Black));
                if (document.CheckForm is not null || document.Page2Elements.Count > 0)
                {
                    ComposeFoldedSheet(page, document);
                }
                else
                {
                    page.Size(Dots(document.WidthDots), Dots(document.HeightDots), Unit.Point);
                    page.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer()
                            .Width(Dots(document.WidthDots))
                            .Height(Dots(document.HeightDots));
                        DrawElements(layers, document.Elements, 0, 0, 1);
                    });
                }
            });
        }).WithMetadata(new DocumentMetadata
        {
            Title = titleSource.Length > 240 ? titleSource[..240] : titleSource,
            Creator = "Greyhound Blood Bank LIS"
        }).GeneratePdf();

        return LabelRenderResult.PdfContent(pdf);
    }

    /// <summary>US Letter, portrait. The tag occupies the top half; the check form the bottom.</summary>
    private const float LetterWidth = 612f;

    private const float LetterHeight = 792f;

    private static void ComposeFoldedSheet(PageDescriptor page, LabelDocument document)
    {
        page.Size(PageSizes.Letter);
        var foldY = LetterHeight / 2f;
        const float topMargin = 22f;
        const float sideMargin = 36f;
        var nativeWidth = Dots(document.WidthDots);
        var nativeHeight = Dots(document.HeightDots);
        var boxWidth = LetterWidth - (sideMargin * 2);
        var boxHeight = foldY - topMargin - 16f;
        var scale = Math.Min(boxWidth / nativeWidth, boxHeight / nativeHeight);
        var offsetX = (LetterWidth - (nativeWidth * scale)) / 2f;

        page.Content().Layers(layers =>
        {
            layers.PrimaryLayer().Width(LetterWidth).Height(LetterHeight);
            DrawElements(layers, document.Elements, offsetX, topMargin, scale);

            layers.Layer()
                .PaddingTop(foldY)
                .LineHorizontal(0.75f)
                .LineColor(Colors.Grey.Medium);

            if (document.Page2Elements.Count > 0)
            {
                var page2Width = Math.Max(1, Dots(document.Page2WidthDots));
                var page2Height = Math.Max(1, Dots(document.Page2HeightDots));
                var bottomTop = foldY + 8f;
                var bottomHeight = LetterHeight - bottomTop - 8f;
                var page2Scale = Math.Min((LetterWidth - 16f) / page2Width, bottomHeight / page2Height);
                var page2OffsetX = (LetterWidth - (page2Width * page2Scale)) / 2f;
                DrawElements(layers, document.Page2Elements, page2OffsetX, bottomTop, page2Scale);
            }
            else if (document.CheckForm is { } form)
            {
                layers.Layer()
                    .PaddingTop(foldY + 18)
                    .PaddingHorizontal(sideMargin)
                    .Column(column =>
                    {
                        column.Item().Text("Unit verification").Bold().FontSize(14);
                        column.Item().PaddingTop(6).Text(form.Attestation).FontSize(10);
                        column.Item().PaddingTop(8).Column(fields =>
                        {
                            foreach (var field in form.Fields)
                            {
                                fields.Item().PaddingTop(3).Row(row =>
                                {
                                    row.ConstantItem(110).Text(field.Label).Bold().FontSize(10);
                                    row.RelativeItem().Text(field.Value).FontSize(10);
                                });
                            }
                        });
                        column.Item().PaddingTop(22).Row(row =>
                        {
                            row.RelativeItem().Element(BlankLine("Signature"));
                            row.ConstantItem(28);
                            row.RelativeItem().Element(BlankLine("Date"));
                        });
                    });
            }
        });
    }

    private static Action<IContainer> BlankLine(string caption) => container =>
    {
        container.Column(column =>
        {
            column.Item().Height(20).BorderBottom(0.75f).BorderColor(Colors.Black);
            column.Item().PaddingTop(2).Text(caption).FontSize(9).FontColor(Colors.Grey.Darken2);
        });
    };

    private static void DrawElements(LayersDescriptor layers, IReadOnlyList<LabelElement> elements, float offsetX, float offsetY, float scale)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case LabelText text when !string.IsNullOrEmpty(text.Value):
                    var span = layers.Layer()
                        .PaddingLeft(offsetX + (Dots(text.X) * scale))
                        .PaddingTop(offsetY + (Dots(text.Y) * scale))
                        .Text(text.Value)
                        .FontSize(Math.Max(6f, Dots(text.FontHeight) * scale));
                    if (text.Bold)
                    {
                        span.Bold();
                    }

                    break;

                case LabelBarcode barcode when !string.IsNullOrEmpty(barcode.Data):
                    var png = EncodeCode128(barcode);
                    layers.Layer()
                        .PaddingLeft(offsetX + (Dots(barcode.X) * scale))
                        .PaddingTop(offsetY + (Dots(barcode.Y) * scale))
                        .Width(Dots(png.Width) * scale)
                        .Height(Dots(barcode.Height) * scale)
                        .Image(png.Png)
                        .FitArea();
                    if (barcode.PrintInterpretation)
                    {
                        layers.Layer()
                            .PaddingLeft(offsetX + (Dots(barcode.X) * scale))
                            .PaddingTop(offsetY + (Dots(barcode.Y + barcode.Height) * scale))
                            .Text(barcode.Data)
                            .FontSize(Math.Max(6f, 7f * scale));
                    }

                    break;
            }
        }
    }

    private static float Dots(int dots) => dots * PointsPerInch / DotsPerInch;

    private static (byte[] Png, int Width) EncodeCode128(LabelBarcode barcode)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Height = Math.Max(barcode.Height, 20),
                Width = Math.Max(80, barcode.Data.Length * 16),
                Margin = 0,
                PureBarcode = true
            }
        };
        var pixels = writer.Write(barcode.Data);
        return (ToPng(pixels), pixels.Width);
    }

    private static byte[] ToPng(PixelData pixels)
    {
        var info = new SKImageInfo(pixels.Width, pixels.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        var rowBytes = bitmap.RowBytes;
        var packed = pixels.Width * 4;
        var destination = bitmap.GetPixels();
        if (rowBytes == packed)
        {
            System.Runtime.InteropServices.Marshal.Copy(pixels.Pixels, 0, destination, pixels.Pixels.Length);
        }
        else
        {
            for (var y = 0; y < pixels.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    pixels.Pixels, y * packed, destination + (y * rowBytes), packed);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
