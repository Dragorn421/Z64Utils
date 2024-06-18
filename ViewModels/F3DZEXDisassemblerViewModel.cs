using System;
using System.Diagnostics;
using System.IO;
using Common;
using CommunityToolkit.Mvvm.ComponentModel;
using F3DZEX.Command;
using NLog.Config;

namespace Z64Utils_recreate_avalonia_ui;

public partial class F3DZEXDisassemblerViewModel : ObservableObject
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    [ObservableProperty]
    private string _inputHex = "";
    [ObservableProperty]
    private bool _inputIsValid = true;
    [ObservableProperty]
    private string _outputDisas = "";

    [ObservableProperty]
    private F3DZEX.Disassembler.Config _disasConfig = new()
    {
        ShowAddress = true,
        RelativeAddress = false,
        DisasMultiCmdMacro = true,
        AddressLiteral = false,
        Static = true,
    };

    // Provided by the view
    public Func<F3DZEXDisassemblerSettingsViewModel?>? OpenF3DZEXDisassemblerSettings;

    public F3DZEXDisassemblerViewModel()
    {
        PropertyChanged += (sender, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(InputHex):
                case nameof(DisasConfig):
                    UpdateDisassembly();
                    break;
            }
        };
    }

    public void OpenDisassemblySettingsCommand()
    {
        Debug.Assert(OpenF3DZEXDisassemblerSettings != null);
        var vm = OpenF3DZEXDisassemblerSettings();
        if (vm == null)
        {
            // Was already open
            return;
        }

        vm.DisasConfig = DisasConfig;
        vm.PropertyChanged += (sender, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(vm.DisasConfig):
                    DisasConfig = vm.DisasConfig;
                    break;
            }
        };
    }

    public void UpdateDisassembly()
    {
        InputIsValid = Utils.IsValidHex(InputHex);
        OutputDisas = "";

        var dlist = new Dlist();

        if (InputIsValid)
        {
            byte[] data = Utils.HexToBytes(InputHex);
            try
            {
                dlist = new Dlist(data);
            }
            catch
            {
                InputIsValid = false;
            }
        }
        F3DZEX.Disassembler disas = new F3DZEX.Disassembler(dlist, DisasConfig);
        var lines = disas.Disassemble();
        StringWriter sw = new StringWriter();
        foreach (var line in lines)
            sw.Write($"{line}\r\n");

        OutputDisas = sw.ToString();
    }
}
