using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GameSaveCenter.Playnite.Converters
{
    /// <summary>Loads bounded, frozen thumbnails with a small process-local LRU cache.</summary>
    public sealed class MediaThumbnailConverter : IValueConverter
    {
        private const int DefaultPreviewWidth = 480;
        private const int CacheLimit = 96;
        private static readonly object CacheGate = new object();
        private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly LinkedList<string> Recency = new LinkedList<string>();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path=value as string;
            if(path==null||path.Length==0)return null;
            if(!File.Exists(path)||!IsImage(path))return null;
            try
            {
                var width=ParseWidth(parameter);
                var info=new FileInfo(path);
                var key=string.Concat(path,"|",width.ToString(CultureInfo.InvariantCulture),"|",
                    info.Length.ToString(CultureInfo.InvariantCulture),"|",info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
                lock(CacheGate)
                {
                    if(Cache.TryGetValue(key,out var cached))
                    {
                        Recency.Remove(cached.Node);
                        Recency.AddFirst(cached.Node);
                        return cached.Image;
                    }
                }
                using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                var image=new BitmapImage();
                image.BeginInit();
                image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=width;
                image.StreamSource=stream;
                image.EndInit();
                image.Freeze();
                AddToCache(key,image);
                return image;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static int ParseWidth(object parameter)
        {
            if(parameter!=null&&int.TryParse(parameter.ToString(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var width))
                return Math.Max(48,Math.Min(width,DefaultPreviewWidth));
            return DefaultPreviewWidth;
        }

        private static void AddToCache(string key,ImageSource image)
        {
            lock(CacheGate)
            {
                if(Cache.ContainsKey(key))return;
                var node=Recency.AddFirst(key);
                Cache[key]=new CacheEntry(image,node);
                while(Cache.Count>CacheLimit&&Recency.Last!=null)
                {
                    var expired=Recency.Last;
                    Recency.RemoveLast();
                    Cache.Remove(expired.Value);
                }
            }
        }

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

        private sealed class CacheEntry
        {
            public CacheEntry(ImageSource image,LinkedListNode<string> node)
            {
                Image=image;
                Node=node;
            }

            public ImageSource Image { get; }
            public LinkedListNode<string> Node { get; }
        }
    }

    /// <summary>Returns a local URI only for video formats supported by the embedded preview.</summary>
    public sealed class MediaVideoSourceConverter : IValueConverter
    {
        public object? Convert(object value,Type targetType,object parameter,CultureInfo culture)
        {
            var path=value as string;
            if(path==null||path.Length==0||!File.Exists(path)||!IsVideo(path))return null;
            try{return new Uri(path,UriKind.Absolute);}
            catch(UriFormatException){return null;}
        }

        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)
            => throw new NotSupportedException();

        private static bool IsVideo(string path)
        {
            var extension=Path.GetExtension(path);
            return string.Equals(extension,".mp4",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".m4v",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".wmv",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".avi",StringComparison.OrdinalIgnoreCase)
                   || string.Equals(extension,".mov",StringComparison.OrdinalIgnoreCase);
        }
    }
}
