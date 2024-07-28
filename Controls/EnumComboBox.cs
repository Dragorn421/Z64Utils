using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Z64Utils_Avalonia;

public class EnumComboBox : ComboBox
{
    protected override Type StyleKeyOverride => typeof(ComboBox);

    public static readonly StyledProperty<Type> EnumTypeProperty =
        AvaloniaProperty.Register<HexTextBox, Type>(nameof(EnumType));
    public Type EnumType
    {
        get => GetValue(EnumTypeProperty);
        set => SetValue(EnumTypeProperty, value);
    }

    public static readonly StyledProperty<Enum?> SelectedEnumValueProperty =
        AvaloniaProperty.Register<HexTextBox, Enum?>(nameof(SelectedEnumValue), defaultValue: null);
    public Enum? SelectedEnumValue
    {
        get => GetValue(SelectedEnumValueProperty);
        set => SetValue(SelectedEnumValueProperty, value);
    }

    public static readonly StyledProperty<EnumUINameMap?> EnumUINameMapProperty =
        AvaloniaProperty.Register<HexTextBox, EnumUINameMap?>(nameof(EnumUINameMap), defaultValue: null);
    public EnumUINameMap? EnumUINameMap
    {
        get => GetValue(EnumUINameMapProperty);
        set => SetValue(EnumUINameMapProperty, value);
    }

    public EnumComboBox()
    {
        PropertyChanged += (sender, e) =>
        {
            switch (e.Property.Name)
            {
                case nameof(EnumType):
                case nameof(EnumUINameMap):
                    Debug.WriteLine($"EnumComboBox PropertyChanged e.Property.Name={e.Property.Name}");
                    UpdateItemsSource();
                    break;
            }
        };
    }

    public void UpdateItemsSource()
    {
        var EnumToUINameDict = GetEnumToUINameDict();
        foreach (var enumName in EnumToUINameDict.Keys)
            Debug.Assert(Enum.IsDefined(EnumType, enumName));

        ItemsSource = Enum.GetValues(EnumType);
        ItemTemplate = new FuncDataTemplate(EnumType,
            (enumValue, nameScope) =>
            {
                Debug.Assert(enumValue != null);
                Debug.Assert(EnumType.IsInstanceOfType(enumValue));
                var enumName = Enum.GetName(EnumType, enumValue);
                Debug.Assert(enumName != null);
                var uiName = EnumToUINameDict.GetValueOrDefault(enumName, enumValue.ToString() ?? enumName);
                return new TextBlock()
                {
                    Text = uiName
                };
            }
        );
    }

    public Dictionary<string, string> GetEnumToUINameDict()
    {
        if (EnumUINameMap == null)
            return new();
        Dictionary<string, string> enumToUINameDict = new();
        foreach (var item in EnumUINameMap.Entries)
        {
            Debug.Assert(item.Enum != null);
            Debug.Assert(item.UI != null);
            enumToUINameDict[item.Enum] = item.UI;
        }
        return enumToUINameDict;
    }
}

public class EnumUINameMap : AvaloniaObject
{
    public List<EnumUINameMapEntry> Entries = new();
    public void Add(EnumUINameMapEntry entry)
    {
        Entries.Add(entry);
    }
}

public class EnumUINameMapEntry : AvaloniaObject
{

    public static readonly StyledProperty<string> EnumProperty =
        AvaloniaProperty.Register<HexTextBox, string>(nameof(Enum));
    public string Enum
    {
        get => GetValue(EnumProperty);
        set => SetValue(EnumProperty, value);
    }

    public static readonly StyledProperty<string> UIProperty =
        AvaloniaProperty.Register<HexTextBox, string>(nameof(UI));
    public string UI
    {
        get => GetValue(UIProperty);
        set => SetValue(UIProperty, value);
    }
}