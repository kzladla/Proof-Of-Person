namespace Unity.AI.Assistant.Editor.Context
{
    class ImageContextMetaData
    {
        public ImageContextCategory Category;
        public int Width;
        public int Height;
        public int Size;
        public string Format;

        public string MimeType => Format?.ToLowerInvariant() switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "tga" => "image/x-tga",
            "tif" or "tiff" => "image/tiff",
            "psd" => "image/vnd.adobe.photoshop",
            "exr" => "image/x-exr",
            "hdr" => "image/vnd.radiance",
            "iff" => "image/x-iff",
            "pct" => "image/x-pict",
            "webp" => "image/webp",
            _ => "image/png"
        };
    }
}
