using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using WpfApplication = System.Windows.Application;
using WinFormsColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Centralized theme manager that provides all theme resources with enhanced security
    /// </summary>
    public static class ThemeManager
    {
        private const int MAX_BRUSHES_COUNT = 500;
        private const int MAX_RESOURCE_KEY_LENGTH = 100;
        private const int MAX_HEX_COLOR_LENGTH = 9;
        private const int INITIALIZATION_TIMEOUT_MS = 30000;

        private static readonly Regex _hexColorPattern = new Regex(@"^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$", RegexOptions.Compiled);
        private static readonly Regex _resourceKeyPattern = new Regex(@"^[a-zA-Z0-9_.]+$", RegexOptions.Compiled);

        private static readonly object _lockObject = new object();
        private static readonly object _initializationLock = new object();
        private static volatile bool _isInitialized;
        private static volatile bool _isInitializing;
        private static readonly Timer _initializationTimer = InitializeTimer();

        private static Enums.Theme _theme = Enums.Theme.Dark;
        private static List<WpfBrush> _brushes;
        private static List<Enums.Theme> _themes;
        private static readonly Dictionary<string, WinFormsColor> _colorCache = new Dictionary<string, WinFormsColor>(StringComparer.Ordinal);
        private static DateTime _lastInitializationAttempt = DateTime.MinValue;

        private static Timer InitializeTimer()
        {
            try
            {
                return new Timer(OnInitializationTimeout, null, Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception e)
            {
                try
                {
                    Logger.Error("Failed to initialize timer: " + e.Message);
                }
                catch
                {
                }
                return null;
            }
        }

        private static void OnInitializationTimeout(object state)
        {
            try
            {
                Logger.Error("ThemeManager initialization timed out");

                lock (_initializationLock)
                {
                    if (_isInitializing)
                    {
                        _isInitializing = false;
                        InitializeMinimalDefaults();
                        _isInitialized = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Initialization timeout handler failed: " + e.Message);
            }
        }

        private static void InitializeMinimalDefaults()
        {
            try
            {
                _themes = new List<Enums.Theme> { Enums.Theme.Dark, Enums.Theme.Light };
                _brushes = new List<WpfBrush>();
                _theme = Enums.Theme.Dark;
            }
            catch (Exception e)
            {
                Logger.Error("Minimal defaults initialization failed: " + e.Message);
            }
        }

        private static void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            var timeSinceLastAttempt = DateTime.UtcNow - _lastInitializationAttempt;
            if (timeSinceLastAttempt.TotalSeconds < 1 && _lastInitializationAttempt != DateTime.MinValue)
            {
                Thread.Sleep(100);
                if (_isInitialized)
                    return;
            }

            lock (_initializationLock)
            {
                if (_isInitialized)
                    return;

                if (_isInitializing)
                {
                    Monitor.Wait(_initializationLock, 5000);
                    return;
                }

                _isInitializing = true;
                _lastInitializationAttempt = DateTime.UtcNow;

                try
                {
                    if (_initializationTimer is not null)
                        _initializationTimer.Change(INITIALIZATION_TIMEOUT_MS, Timeout.Infinite);

                    InitializeDefaults();
                    InitializeThemes();
                    InitializeBrushes();

                    _isInitialized = true;
                    _isInitializing = false;

                    if (_initializationTimer is not null)
                        _initializationTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    Monitor.PulseAll(_initializationLock);
                }
                catch (Exception e)
                {
                    Logger.Error("Initialization failed: " + e.Message);

                    InitializeMinimalDefaults();
                    _isInitialized = true;
                    _isInitializing = false;

                    Monitor.PulseAll(_initializationLock);
                }
            }
        }

        private static bool IsValidResourceKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (key.Length > MAX_RESOURCE_KEY_LENGTH)
                return false;

            try
            {
                return _resourceKeyPattern.IsMatch(key);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidTheme(Enums.Theme theme)
        {
            try
            {
                return Enum.IsDefined(typeof(Enums.Theme), theme);
            }
            catch
            {
                return false;
            }
        }

        private static void InitializeDefaults()
        {
            try
            {
                var app = WpfApplication.Current;
                if (app is null || app.Resources is null)
                {
                    Logger.Warning("Application.Current or Resources is null during theme initialization");
                    return;
                }

                var defaultResources = new Dictionary<string, WpfColor>
                {
                    { "Accent", WpfColor.FromArgb(0xFF, 0xFF, 0x3B, 0x30) },
                    { "MemoryBarIndicatorBackground", WpfColor.FromArgb(0xFF, 0xF4, 0xF2, 0xEE) },
                    { "MemoryBarTrackBackground", WpfColor.FromArgb(0xFF, 0x6E, 0x6E, 0x69) },
                    { "PrimaryBackground", WpfColor.FromArgb(0xFF, 0x14, 0x14, 0x14) },
                    { "PrimaryBorder", WpfColor.FromArgb(0xFF, 0x4A, 0x4A, 0x47) },
                    { "SecondaryBackground", WpfColor.FromArgb(0xFF, 0x1E, 0x1E, 0x1D) },
                    { "SecondaryBorder", WpfColor.FromArgb(0xFF, 0x8A, 0x8A, 0x85) },
                    { "SecondaryDisabled", WpfColor.FromArgb(0xFF, 0x9A, 0x9A, 0x94) },
                    { "SecondaryForeground", WpfColor.FromArgb(0xFF, 0xF4, 0xF2, 0xEE) }
                };

                ApplyFluentTheme(Enums.Theme.Dark);

                foreach (var kvp in defaultResources)
                {
                    try
                    {
                        if (!IsValidResourceKey(kvp.Key))
                        {
                            Logger.Warning("Invalid resource key skipped: " + kvp.Key);
                            continue;
                        }

                        app.Resources[kvp.Key] = new WpfBrush(kvp.Value);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to set default resource " + kvp.Key + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Default resources initialization failed: " + e.Message);
            }
        }

        private static void InitializeThemes()
        {
            try
            {
                var enumValues = Enum.GetValues(typeof(Enums.Theme));
                if (enumValues is null || enumValues.Length == 0)
                {
                    _themes = new List<Enums.Theme> { Enums.Theme.Dark };
                    return;
                }

                _themes = new List<Enums.Theme>();

                foreach (Enums.Theme theme in enumValues)
                {
                    if (IsValidTheme(theme))
                    {
                        _themes.Add(theme);
                    }
                }

                if (_themes.Count == 0)
                {
                    _themes.Add(Enums.Theme.Dark);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Theme initialization failed: " + e.Message);
                _themes = new List<Enums.Theme> { Enums.Theme.Dark };
            }
        }

        private static void InitializeBrushes()
        {
            try
            {
                var colorType = typeof(WinFormsColor);
                if (colorType is null)
                {
                    _brushes = new List<WpfBrush>();
                    return;
                }

                var colorProperties = colorType.GetProperties(BindingFlags.Public | BindingFlags.Static);
                if (colorProperties is null || colorProperties.Length == 0)
                {
                    _brushes = new List<WpfBrush>();
                    return;
                }

                _brushes = new List<WpfBrush>();
                var processedCount = 0;

                foreach (var property in colorProperties)
                {
                    try
                    {
                        if (processedCount >= MAX_BRUSHES_COUNT)
                        {
                            Logger.Warning("Brush count limit reached: " + MAX_BRUSHES_COUNT);
                            break;
                        }

                        if (property is null || property.PropertyType != typeof(WinFormsColor))
                            continue;

                        var color = (WinFormsColor)property.GetValue(null, null);

                        if (color.A == 255)
                        {
                            var wpfColor = WpfColor.FromArgb(color.A, color.R, color.G, color.B);
                            _brushes.Add(new WpfBrush(wpfColor));
                            processedCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        var propertyName = (property is not null) ? property.Name : "unknown";
                        Logger.Error("Failed to process color property " + propertyName + ": " + e.Message);
                    }
                }

                try
                {
                    _brushes = _brushes
                        .OrderBy(GetColorHueSafe)
                        .ThenBy(GetColorSaturationSafe)
                        .ThenBy(GetColorBrightnessSafe)
                        .ToList();
                }
                catch (Exception e)
                {
                    Logger.Error("Brush sorting failed: " + e.Message);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Brush initialization failed: " + e.Message);
                _brushes = new List<WpfBrush>();
            }
        }

        private static float GetColorHueSafe(WpfBrush brush)
        {
            try
            {
                if (brush is null)
                    return 0f;

                var color = brush.Color;
                var winFormsColor = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);
                var hue = winFormsColor.GetHue();

                if (float.IsNaN(hue) || float.IsInfinity(hue))
                    return 0f;

                return Math.Max(0f, Math.Min(360f, hue));
            }
            catch
            {
                return 0f;
            }
        }

        private static float GetColorSaturationSafe(WpfBrush brush)
        {
            try
            {
                if (brush is null)
                    return 0f;

                var color = brush.Color;
                var winFormsColor = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);
                var saturation = winFormsColor.GetSaturation();

                if (float.IsNaN(saturation) || float.IsInfinity(saturation))
                    return 0f;

                return Math.Max(0f, Math.Min(1f, saturation));
            }
            catch
            {
                return 0f;
            }
        }

        private static float GetColorBrightnessSafe(WpfBrush brush)
        {
            try
            {
                if (brush is null)
                    return 0f;

                var color = brush.Color;
                var winFormsColor = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);
                var brightness = winFormsColor.GetBrightness();

                if (float.IsNaN(brightness) || float.IsInfinity(brightness))
                    return 0f;

                return Math.Max(0f, Math.Min(1f, brightness));
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Gets the accent color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor AccentColor
        {
            get { return GetColorSafe(nameof(AccentColor).Replace("Color", string.Empty), WinFormsColor.DeepSkyBlue); }
        }

        /// <summary>
        /// Gets the primary background color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor PrimaryBackgroundColor
        {
            get { return GetColorSafe(nameof(PrimaryBackgroundColor).Replace("Color", string.Empty), WinFormsColor.FromArgb(32, 32, 32)); }
        }

        /// <summary>
        /// Gets the secondary background color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor SecondaryBackgroundColor
        {
            get { return GetColorSafe(nameof(SecondaryBackgroundColor).Replace("Color", string.Empty), WinFormsColor.DarkSlateGray); }
        }
        
        /// <summary>
        /// Gets the secondary border color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor SecondaryBorderColor
        {
            get { return GetColorSafe(nameof(SecondaryBorderColor).Replace("Color", string.Empty), WinFormsColor.DimGray); }
        }

        /// <summary>
        /// Gets the secondary foreground color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor SecondaryForegroundColor
        {
            get { return GetColorSafe(nameof(SecondaryForegroundColor).Replace("Color", string.Empty), WinFormsColor.White); }
        }

        private static WinFormsColor GetColorSafe(string resourceKey, WinFormsColor fallback)
        {
            if (!IsValidResourceKey(resourceKey))
            {
                Logger.Warning("Invalid resource key: " + resourceKey);
                return fallback;
            }

            try
            {
                lock (_colorCache)
                {
                    WinFormsColor cachedColor;
                    if (_colorCache.TryGetValue(resourceKey, out cachedColor))
                    {
                        return cachedColor;
                    }
                }

                EnsureInitialized();

                var app = WpfApplication.Current;
                if (app is null || app.Resources is null)
                    return fallback;

                var resource = app.Resources[resourceKey] as WpfBrush;
                if (resource is null)
                    return fallback;

                var color = resource.Color;
                var result = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);

                lock (_colorCache)
                {
                    if (!_colorCache.ContainsKey(resourceKey))
                    {
                        _colorCache[resourceKey] = result;
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                Logger.Error("Failed to get color for resource " + resourceKey + ": " + e.Message);
                return fallback;
            }
        }

        /// <summary>
        /// Gets the brushes with defensive copying.
        /// </summary>
        public static List<WpfBrush> Brushes
        {
            get
            {
                EnsureInitialized();
                lock (_lockObject)
                {
                    var brushes = _brushes;
                    if (brushes is null || brushes.Count == 0)
                        return new List<WpfBrush>();

                    return new List<WpfBrush>(brushes);
                }
            }
        }

        /// <summary>
        /// Gets or sets the theme with enhanced validation and error handling.
        /// </summary>
        public static Enums.Theme Theme
        {
            get
            {
                EnsureInitialized();
                lock (_lockObject)
                {
                    return _theme;
                }
            }
            set
            {
                if (!IsValidTheme(value))
                {
                    Logger.Warning("Invalid theme value: " + value);
                    throw new ArgumentException("Invalid theme value: " + value, "value");
                }

                EnsureInitialized();

                lock (_lockObject)
                {
                    if (_theme == value)
                        return;

                    var oldTheme = _theme;

                    try
                    {
                        Load(value);
                        _theme = value;

                        lock (_colorCache)
                        {
                            _colorCache.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to load theme " + value + ", reverting to " + oldTheme + ": " + e.Message);
                        _theme = oldTheme;
                        throw;
                    }
                }

                try
                {
                    App.ReleaseMemory();
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to release memory after theme change: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Gets the themes with defensive copying.
        /// </summary>
        public static List<Enums.Theme> Themes
        {
            get
            {
                EnsureInitialized();
                lock (_lockObject)
                {
                    var themes = _themes;
                    if (themes is null || themes.Count == 0)
                    {
                        return new List<Enums.Theme> { Enums.Theme.Dark };
                    }

                    return new List<Enums.Theme>(themes);
                }
            }
        }

        private static bool IsValidHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return false;

            if (hex.Length > MAX_HEX_COLOR_LENGTH)
                return false;

            try
            {
                return _hexColorPattern.IsMatch(hex);
            }
            catch
            {
                return false;
            }
        }

        private static void Load(Enums.Theme theme)
        {
            Theme resource;

            try
            {
                if (string.IsNullOrEmpty(Constants.App.ThemesResourcePath) ||
                    string.IsNullOrEmpty(Constants.App.EmbeddedResourcePathExtension))
                {
                    Logger.Error("Theme resource path constants are null or empty");
                    return;
                }

                if (Constants.App.ThemesResourcePath.Contains("..") ||
                    Constants.App.EmbeddedResourcePathExtension.Contains(".."))
                {
                    Logger.Error("Theme resource path contains directory traversal characters");
                    return;
                }

                var resourcePath = string.Format(CultureInfo.InvariantCulture,
                    "{0}{1}Theme{2}",
                    Constants.App.ThemesResourcePath,
                    theme,
                    Constants.App.EmbeddedResourcePathExtension);

                if (resourcePath.Length > 260)
                {
                    Logger.Error("Theme resource path is too long");
                    return;
                }

                resource = Helper.ReadEmbeddedResource<Theme>(resourcePath);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to read embedded theme resource for " + theme + ": " + e.Message);
                return;
            }

            if (resource is null)
            {
                Logger.Warning("Theme resource is null for " + theme);
                return;
            }

            try
            {
                var app = WpfApplication.Current;
                if (app is null || app.Resources is null)
                {
                    Logger.Warning("Application.Current or Resources is null during theme loading");
                    return;
                }

                var properties = typeof(Theme).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties is null || properties.Length == 0)
                {
                    Logger.Warning("No properties found on Theme class");
                    return;
                }

                foreach (var property in properties)
                {
                    if (property is null)
                        continue;

                    if (!IsValidResourceKey(property.Name))
                    {
                        Logger.Warning("Invalid property name skipped: " + property.Name);
                        continue;
                    }

                    string hex;

                    try
                    {
                        hex = property.GetValue(resource, null) as string;
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to get property value for " + property.Name + ": " + e.Message);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(hex))
                        continue;

                    if (!IsValidHexColor(hex))
                    {
                        Logger.Warning("Invalid hex color '" + hex + "' for property " + property.Name);
                        continue;
                    }

                    try
                    {
                        var color = ParseHexColor(hex);
                        app.Resources[property.Name] = new WpfBrush(color);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to parse or set color for property " + property.Name + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Failed to apply theme " + theme + ": " + e.Message);
            }

            ApplyFluentTheme(theme);
        }

        /// <summary>
        /// Applies the WPF-UI (Fluent) theme and the signal-red accent that matches the selected theme.
        /// </summary>
        /// <param name="theme">The theme.</param>
        private static void ApplyFluentTheme(Enums.Theme theme)
        {
            try
            {
                var fluentTheme = theme == Enums.Theme.Dark ? Wpf.Ui.Appearance.ApplicationTheme.Dark : Wpf.Ui.Appearance.ApplicationTheme.Light;
                var accent = theme == Enums.Theme.Dark ? WpfColor.FromRgb(0xFF, 0x3B, 0x30) : WpfColor.FromRgb(0xE3, 0x06, 0x13);

                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(fluentTheme, Wpf.Ui.Controls.WindowBackdropType.Mica, false);
                Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(accent, fluentTheme, false);

                // o WPF-UI clareia o destaque no tema escuro; fixa o vermelho de sinal nos estados do botão primário e dos controles marcados
                var isDark = theme == Enums.Theme.Dark;
                var states = new Dictionary<string, WpfColor>
                {
                    { "AccentFillColorDefaultBrush", accent },
                    { "AccentFillColorSecondaryBrush", isDark ? WpfColor.FromRgb(0xE6, 0x35, 0x2B) : WpfColor.FromRgb(0xC9, 0x05, 0x0F) },
                    { "AccentFillColorTertiaryBrush", isDark ? WpfColor.FromRgb(0xCC, 0x2F, 0x26) : WpfColor.FromRgb(0xAF, 0x04, 0x0D) },
                    { "TextOnAccentFillColorPrimaryBrush", WpfColor.FromRgb(0xFF, 0xFF, 0xFF) },
                    { "AccentButtonBackground", accent },
                    { "AccentButtonBackgroundPointerOver", isDark ? WpfColor.FromRgb(0xE6, 0x35, 0x2B) : WpfColor.FromRgb(0xC9, 0x05, 0x0F) },
                    { "AccentButtonBackgroundPressed", isDark ? WpfColor.FromRgb(0xCC, 0x2F, 0x26) : WpfColor.FromRgb(0xAF, 0x04, 0x0D) },
                    { "AccentButtonForeground", WpfColor.FromRgb(0xFF, 0xFF, 0xFF) },
                    { "AccentButtonForegroundPointerOver", WpfColor.FromRgb(0xFF, 0xFF, 0xFF) },
                    { "AccentButtonForegroundPressed", WpfColor.FromRgb(0xFF, 0xFF, 0xFF) },
                    { "SystemAccentColorPrimaryBrush", accent },
                    { "SystemAccentColorSecondaryBrush", accent },
                    { "SystemAccentColorTertiaryBrush", accent },
                    { "CheckBoxCheckBackgroundFillChecked", accent },
                    { "CheckBoxCheckBackgroundFillCheckedPointerOver", accent },
                    { "CheckBoxCheckBackgroundFillCheckedPressed", accent },
                    { "SliderThumbBackground", accent },
                    { "SliderThumbBackgroundPointerOver", accent },
                    { "SliderOuterThumbBackground", accent },
                    { "SliderTrackValueFill", accent },
                    { "ToggleSwitchFillOn", accent },
                    { "ToggleSwitchFillOnPointerOver", accent },
                    { "ToggleSwitchFillOnPressed", accent },
                    { "ComboBoxBorderBrushFocused", accent }
                };

                foreach (var state in states)
                    WpfApplication.Current.Resources[state.Key] = new WpfBrush(state.Value);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to apply Fluent theme: " + e.Message);
            }
        }

        private static WpfColor ParseHexColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor))
                throw new ArgumentException("Hex color cannot be null or empty", "hexColor");

            if (!IsValidHexColor(hexColor))
                throw new ArgumentException("Invalid hex color format: " + hexColor, "hexColor");

            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(hexColor);
                return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to parse hex color '" + hexColor + "': " + e.Message);
                throw new ArgumentException("Invalid hex color format: " + hexColor, "hexColor", e);
            }
        }

        /// <summary>
        /// Cleanup method for proper resource disposal
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                lock (_initializationLock)
                {
                    if (_initializationTimer is not null)
                        _initializationTimer.Dispose();
                }

                lock (_colorCache)
                {
                    _colorCache.Clear();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Cleanup failed: " + e.Message);
            }
        }
    }
}