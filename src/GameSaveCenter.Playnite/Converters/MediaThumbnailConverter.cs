using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GameSaveCenter.Playnite.Converters
{
    /// <summary>Loads only the selected screenshot and decodes it to a bounded preview.</summary>
    public sealed class MediaThumbnailConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path=value as string;
            if(path==null||path.Length==0)return null;
            if(!File.Exists(path)||!IsImage(path))return null;
            try
            {
                using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                var image=new BitmapImage();
                image.BeginInit();
                image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=480;
                image.StreamSource=stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static bool IsImage(string path)
        {
            var extension=Path.GetExtension(path);
            return string.Equals(extension,".png",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".jpg",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".jpeg",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".bmp",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".gif",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".webp",StringComparison.OrdinalIgnoreCase);
        }
    }
}
