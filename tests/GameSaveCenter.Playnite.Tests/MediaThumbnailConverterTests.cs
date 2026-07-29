using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameSaveCenter.Playnite.Converters;
using Xunit;

namespace GameSaveCenter.Playnite.Tests
{
    public sealed class MediaThumbnailConverterTests
    {
        [Fact]
        public void Convert_UsesBoundedFrozenCacheAndReleasesFiles()
        {
            var root=Path.Combine(Path.GetTempPath(),"GameSaveCenter.Thumbnail.Tests",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var converter=new MediaThumbnailConverter();
                for(var index=0;index<100;index++)
                {
                    var path=Path.Combine(root,index.ToString(CultureInfo.InvariantCulture)+".png");
                    WritePng(path,(byte)index);
                    var converted=converter.Convert(path,typeof(ImageSource),"48",CultureInfo.InvariantCulture);
                    var image=Assert.IsAssignableFrom<ImageSource>(converted);
                    Assert.True(image.IsFrozen);
                    File.Delete(path);
                    Assert.False(File.Exists(path));
                }

                var field=typeof(MediaThumbnailConverter).GetField("Cache",BindingFlags.Static|BindingFlags.NonPublic);
                var cache=Assert.IsAssignableFrom<IDictionary>(field!.GetValue(null));
                Assert.InRange(cache.Count,1,96);
            }
            finally
            {
                if(Directory.Exists(root))Directory.Delete(root,true);
            }
        }

        private static void WritePng(string path,byte value)
        {
            var pixels=new[]{value,(byte)(255-value),(byte)127,(byte)255};
            var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,pixels,4);
            var encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(path);
            encoder.Save(stream);
        }
    }
}
