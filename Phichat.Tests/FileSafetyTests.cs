using Phichat.API.Security;
using Phichat.Infrastructure.Files;

namespace Phichat.Tests;

public class FileNameSanitizerTests
{
    [Theory]
    [InlineData("../../../../evil.html", "evil.html")]
    [InlineData(@"..\..\windows\evil.exe", "evil.exe")]
    [InlineData("C:/temp/report.pdf", "report.pdf")]
    [InlineData("photo.png", "photo.png")]
    [InlineData("a<b>c|d?.txt", "a_b_c_d_.txt")]
    [InlineData("..", "file")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    public void Produces_a_single_safe_segment(string? input, string expected)
    {
        Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void Long_names_are_truncated_keeping_the_extension()
    {
        var result = FileNameSanitizer.Sanitize(new string('a', 300) + ".pdf");

        Assert.True(result.Length <= 100);
        Assert.EndsWith(".pdf", result);
    }
}

public class ImageSignatureTests
{
    [Fact]
    public void Detects_png() =>
        Assert.Equal(".png", ImageSignature.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }));

    [Fact]
    public void Detects_jpeg() =>
        Assert.Equal(".jpg", ImageSignature.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));

    [Fact]
    public void Detects_gif() =>
        Assert.Equal(".gif", ImageSignature.Detect("GIF89a......"u8.ToArray()));

    [Fact]
    public void Detects_webp() =>
        Assert.Equal(".webp", ImageSignature.Detect("RIFF\0\0\0\0WEBP"u8.ToArray()));

    [Theory]
    [InlineData("hello world")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("<html><script>alert(1)</script>")]
    public void Rejects_non_images(string content) =>
        Assert.Null(ImageSignature.Detect(System.Text.Encoding.UTF8.GetBytes(content)));
}
