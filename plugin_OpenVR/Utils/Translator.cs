using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace plugin_OpenVR.Utils;

public class Translator : INotifyPropertyChanged
{
    public static Translator Get { get; } = new();

    public string this[string key]
    {
        get => String(key);
    }

    public string Culture
    {
        get => SteamVR.Instance?.Host?.LanguageCode ?? "en";
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string String(string key)
    {
        return SteamVR.Instance?.Host?.RequestLocalizedString(key);
    }

    public void OnPropertyChanged(string propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}

public class TranslateExtension : MarkupExtension
{
    public TranslateExtension()
    {
        Key = "";
        Arguments = [];
    }

    public TranslateExtension(string key)
    {
        Key = key;
        Arguments = [];
    }

    public TranslateExtension(string key, IList<IBinding> arguments)
    {
        Key = key;
        Arguments = arguments ?? [];
    }

    public string Key { get; set; }
    public IList<IBinding> Arguments { get; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var multi = new MultiBinding { Converter = new TranslateConverter(Key) };

        multi.Bindings.Add(new Binding { Source = Translator.Get, Path = nameof(Translator.Culture), Mode = BindingMode.OneWay });

        foreach (var arg in Arguments)
            multi.Bindings.Add(arg);

        return multi;
    }
}

public class TranslateConverter(string key) : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        var args = values.Skip(1).ToArray();
        var raw = Translator.Get.String(key);
        return args.Length > 0 ? raw.Format(args) : raw;
    }
}

public static class StringExtensions
{
    public static string Format(this string s, params object[] arguments)
    {
        var result = s; // Create a backup
        var formats = arguments.ToList();

        foreach (var format in formats.Where(format => result.Contains($"{{{formats.IndexOf(format)}}}")))
            result = result.Replace($"{{{formats.IndexOf(format)}}}", format?.ToString() ?? string.Empty);


        return result; // Return the outer result
    }
}
