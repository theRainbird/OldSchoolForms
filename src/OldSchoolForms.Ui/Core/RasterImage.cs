using SkiaSharp;
using System;
using System.IO;
using System.Reflection;

namespace OldSchoolForms.Ui.Core;

/// <summary>
/// Represents a raster image (PNG, JPEG, BMP, GIF, WebP, etc.) that can be rendered by the graphics system.
/// Supports alpha transparency for cutout images.
/// </summary>
public class RasterImage : IGraphicsImage
{
    private SKImage? _nativeImage;
    private bool _disposed;

    /// <summary>
    /// Gets the native SkiaSharp image. For internal renderer use only.
    /// </summary>
    public SKImage? NativeImage => _nativeImage;

    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Gets the Base64-encoded image data this image was imported from, or <c>null</c>
    /// when the image was not imported. Used by the designer to re-emit an equivalent
    /// load call when serializing images that are not embedded resources.
    /// </summary>
    public string? ImportedBase64 { get; private set; }

    private RasterImage() { }

    /// <summary>
    /// Loads a raster image from a Base64-encoded string. The encoded data is retained so
    /// the designer can re-emit an equivalent load call when serializing imported images.
    /// </summary>
    /// <param name="base64">The Base64-encoded image data.</param>
    /// <returns>The loaded RasterImage, or null if the input is not valid Base64 or not a valid image.</returns>
    public static RasterImage? FromBase64(string base64)
    {
        try
        {
            if (string.IsNullOrEmpty(base64))
                return null;
            var bytes = Convert.FromBase64String(base64);
            var image = FromBytes(bytes);
            if (image != null)
                image.ImportedBase64 = base64;
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a raster image from an embedded resource.
    /// </summary>
    /// <param name="resourceName">The fully qualified manifest resource name.</param>
    /// <returns>The loaded RasterImage, or null if loading failed.</returns>
    public static RasterImage? FromResource(string resourceName)
    {
        try
        {
            var assembly = typeof(RasterImage).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;
            return FromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a raster image from a stream.
    /// </summary>
    /// <param name="stream">The stream containing image data.</param>
    /// <returns>The loaded RasterImage, or null if loading failed.</returns>
    public static RasterImage? FromStream(Stream stream)
    {
        try
        {
            using var codec = SKCodec.Create(stream);
            if (codec == null)
                return null;

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);

            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                return null;

            var image = SKImage.FromBitmap(bitmap);
            if (image == null)
                return null;

            return new RasterImage
            {
                _nativeImage = image,
                Width = codec.Info.Width,
                Height = codec.Info.Height
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a raster image from a file path.
    /// </summary>
    /// <param name="filePath">The path to the image file.</param>
    /// <returns>The loaded RasterImage, or null if loading failed.</returns>
    public static RasterImage? FromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            using var stream = File.OpenRead(filePath);
            return FromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads a raster image from a byte array.
    /// </summary>
    /// <param name="data">The byte array containing image data.</param>
    /// <returns>The loaded RasterImage, or null if loading failed.</returns>
    public static RasterImage? FromBytes(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            return FromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Releases all resources used by this RasterImage.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _nativeImage?.Dispose();
        _nativeImage = null;
    }
}
