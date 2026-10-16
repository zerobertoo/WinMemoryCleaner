using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Percentage To Angle Converter
    /// Converts a percentage value (0-100) to degrees (0-360) for the clock dial hand
    /// </summary>
    /// <seealso cref="IValueConverter" />
    public class PercentageToAngleConverter : IValueConverter
    {
        /// <summary>
        /// Converts a percentage to an angle in degrees.
        /// </summary>
        /// <param name="value">The percentage (0-100).</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>The angle in degrees</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || value == DependencyProperty.UnsetValue)
                    return 0.0;

                var percentage = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);

                // limita a 0-100 antes de converter
                if (percentage < 0)
                    percentage = 0;

                if (percentage > 100)
                    percentage = 100;

                return percentage * 3.6;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Converts an angle back to percentage (not supported).
        /// </summary>
        /// <param name="value">The value that the binding target produces.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>Not supported</returns>
        /// <exception cref="NotSupportedException"></exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
