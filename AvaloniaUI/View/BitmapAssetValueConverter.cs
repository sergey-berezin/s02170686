using System;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Platform;

namespace View 
{
        public class BitmapAssetValueConverter : IValueConverter
        {
                public static BitmapAssetValueConverter Instance = new BitmapAssetValueConverter();
                public object Convert(object value, Type targetType, object parameter,
                                      System.Globalization.CultureInfo culture)
                {
                        if (value == null)
                                return null;

                        return new Bitmap((string)value);
                }

                public object ConvertBack(object value, Type targetType, object parameter,
                                          System.Globalization.CultureInfo culture)
                {
                        return null;
                }
        }

}