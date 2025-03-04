
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using F3DZEX.Command;
using RDP;
using Z64;

namespace Z64Utils_Avalonia;

public partial class ObjectAnalyzerWindowViewModel : ObservableObject
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private Z64Game? _game;
    private Z64File? _file;
    private int _segment;
    private Z64Object? _object;

    static public string DEFAULT_WINDOW_TITLE = "Object Analyzer";
    [ObservableProperty]
    private string _windowTitle = DEFAULT_WINDOW_TITLE;

    [ObservableProperty]
    private F3DZEX.Disassembler.Config _F3DZEXDisasConfig = new()
    {
        ShowAddress = true,
        RelativeAddress = false,
        DisasMultiCmdMacro = true,
        AddressLiteral = false,
        Static = true,
    };
    [ObservableProperty]
    private Dlist? _disasDList = null;

    // Provided by the view
    public Func<DListViewerWindowViewModel>? OpenDListViewer;
    public Func<SkeletonViewerWindowViewModel>? OpenSkeletonViewer;
    public Func<F3DZEXDisassemblerSettingsViewModel?>? OpenF3DZEXDisassemblerSettings;

    public ICommand OpenDListViewerObjectHolderEntryCommand;
    public ICommand OpenSkeletonViewerObjectHolderEntryCommand;

    public ObjectAnalyzerWindowViewModel()
    {
        OpenDListViewerObjectHolderEntryCommand = new RelayCommand<ObjectHolderEntry>(OpenDListViewerObjectHolderEntryCommandExecute);
        OpenSkeletonViewerObjectHolderEntryCommand = new RelayCommand<ObjectHolderEntry>(OpenSkeletonViewerObjectHolderEntryCommandExecute);
        PropertyChanged += (sender, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(FilterText):
                    UpdateMap();
                    break;
                case nameof(F3DZEXDisasConfig):
                case nameof(DisasDList):
                    if (DisasDList != null)
                        UpdateDListDisassembly();
                    break;
            }
        };
    }

    // TODO: vvv

    public void FindDListsCommand()
    {
        Debug.Assert(HasFile());
        Debug.Assert(_object != null);
        Debug.Assert(_file != null);

        var config = new Z64ObjectAnalyzer.Config(); // TODO
        Z64ObjectAnalyzer.FindDlists(_object, _file.Data, _segment, config);
        UpdateMap();
    }
    public bool CanFindDListsCommand(object? parameter)
    {
        return HasFile();
    }

    public void AnalyzeDListsCommand()
    {
        Debug.Assert(HasFile());
        Debug.Assert(_object != null);
        Debug.Assert(_file != null);

        Z64ObjectAnalyzer.AnalyzeDlists(_object, _file.Data, _segment);
        UpdateMap();
    }
    public bool CanAnalyzeDListsCommand(object? parameter)
    {
        return HasFile();
    }

    public void ImportJSONCommand() { }
    public void ExportJSONCommand() { }

    public void ResetCommand()
    {
        Debug.Assert(HasFile());
        Debug.Assert(_object != null);
        Debug.Assert(_file != null);

        _object.Entries.Clear();
        _object.AddUnknow(_file.Data.Length);
        _object.SetData(_file.Data);
        UpdateMap();
    }
    public bool CanResetCommand(object? parameter)
    {
        return HasFile();
    }

    public void DisassemblySettingsCommand()
    {
        Debug.Assert(OpenF3DZEXDisassemblerSettings != null);
        var vm = OpenF3DZEXDisassemblerSettings();
        if (vm == null)
        {
            // Was already open
            return;
        }

        vm.DisasConfig = F3DZEXDisasConfig;
        vm.PropertyChanged += (sender, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(vm.DisasConfig):
                    F3DZEXDisasConfig = vm.DisasConfig;
                    break;
            }
        };
    }

    //

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private ObservableCollection<ObjectHolderEntry> _objectHolderEntries = new();

    public class ObjectHolderEntry
    {
        public string Offset { get; }
        public string Name { get; }
        public string Type { get; }
        public Z64Object.ObjectHolder ObjectHolder { get; }

        public ObjectHolderEntry(string offset, string name, string type, Z64Object.ObjectHolder objectHolder)
        {
            Offset = offset;
            Name = name;
            Type = type;
            ObjectHolder = objectHolder;
        }
    }

    [ObservableProperty]
    private IObjectHolderEntryDetailsViewModel? _objectHolderEntryDetailsViewModel = null;
    [ObservableProperty]
    private byte[]? _objectHolderEntryDataBytes;
    [ObservableProperty]
    private uint _objectHolderEntryFirstByteAddress;

    public void ClearFile()
    {
        WindowTitle = DEFAULT_WINDOW_TITLE;
        ObjectHolderEntryDetailsViewModel = null;
        ObjectHolderEntryDataBytes = null;
        ObjectHolderEntryFirstByteAddress = 0;
        ObjectHolderEntries.Clear();
        _game = null;
        _file = null;
        _segment = 0;
        _object = null;
    }

    public bool HasFile()
    {
        if (_file == null)
        {
            Debug.Assert(_game == null);
            Debug.Assert(_object == null);
            return false;
        }
        else
        {
            Debug.Assert(_game != null);
            Debug.Assert(_object != null);
            return true;
        }
    }

    public void SetFile(Z64Game game, Z64File file, int segment)
    {
        ClearFile();

        try
        {
            string fileName = game.GetFileName(file.VRomStart);
            WindowTitle = $"\"{fileName}\" ({file.VRomStart:X8}-{file.VRomEnd:X8})";

            _game = game;
            _file = file;
            _segment = segment;

            _object = new Z64Object(game, file.Data, fileName);

            UpdateMap();
        }
        catch
        {
            ClearFile();
            throw;
        }
    }

    private void UpdateMap()
    {
        Debug.Assert(_object != null);

        // TODO handle this better (keep the selection)
        ObjectHolderEntryDetailsViewModel = null;
        ObjectHolderEntryDataBytes = null;
        ObjectHolderEntryFirstByteAddress = 0;

        var newObjectHolderEntries = new List<ObjectHolderEntry>();

        string filterText = FilterText.ToLower();

        for (int i = 0; i < _object.Entries.Count; i++)
        {
            var entry = _object.Entries[i];
            var addr = new SegmentedAddress(_segment, _object.OffsetOf(entry));
            string addrStr = $"{addr.VAddr:X8}";
            string entryTypeStr = entry.GetEntryType().ToString();

            if (filterText == ""
                || entry.Name.ToLower().Contains(filterText)
                || addrStr.ToLower().Contains(filterText)
                || entryTypeStr.ToLower().Contains(filterText)
            )
            {
                newObjectHolderEntries.Add(
                    new ObjectHolderEntry(
                        offset: addrStr,
                        name: entry.Name,
                        type: entryTypeStr,
                        objectHolder: entry
                    )
                );
            }
        }

        ObjectHolderEntries = new(newObjectHolderEntries);
    }

    public void OnObjectHolderEntrySelected(ObjectHolderEntry ohe)
    {
        Debug.Assert(_object != null);
        ObjectHolderEntryDataBytes = ohe.ObjectHolder.GetData();
        ObjectHolderEntryFirstByteAddress = (uint)_object.OffsetOf(ohe.ObjectHolder);

        DisasDList = null;

        switch (ohe.ObjectHolder.GetEntryType())
        {
            case Z64Object.EntryType.Texture:
                var textureHolder = (Z64Object.TextureHolder)ohe.ObjectHolder;
                var bitmap = textureHolder.GetBitmap().ToAvaloniaBitmap();
                var imageVM = new ImageOHEDViewModel()
                {
                    InfoText =
                        $"{textureHolder.Name} {textureHolder.Format}"
                        + $" {textureHolder.Width}x{textureHolder.Height}",
                    Image = bitmap,
                };
                ObjectHolderEntryDetailsViewModel = imageVM;
                break;

            case Z64Object.EntryType.Vertex:
                var vertexHolder = (Z64Object.VertexHolder)ohe.ObjectHolder;
                uint vertexHolderAddress = new SegmentedAddress(_segment, _object.OffsetOf(vertexHolder)).VAddr;
                var vertexArrayVM = new VertexArrayOHEDViewModel()
                {
                    Vertices = new(
                        vertexHolder.Vertices.Select(
                            (v, i) => new VertexArrayOHEDViewModel.VertexEntry(
                                index: i,
                                address: vertexHolderAddress + (uint)i * Z64Object.VertexHolder.VERTEX_SIZE,
                                coordX: v.X,
                                coordY: v.Y,
                                coordZ: v.Z,
                                texCoordS: v.TexX,
                                texCoordT: v.TexY,
                                colorRorNormalX: v.R,
                                colorGorNormalY: v.G,
                                colorBorNormalZ: v.B,
                                alpha: v.A
                            )
                        )
                    ),
                };
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var t1 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    // Loading this view is a bit slow
                    // Setting it in another task slightly improves the feeling of responsiveness
                    ObjectHolderEntryDetailsViewModel = vertexArrayVM;
                    var t2 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    Logger.Trace("ObjectHolderEntryDetailsViewModel = vertexArrayVM; t2-t1={0}ms", t2 - t1);
                });
                break;

            case Z64Object.EntryType.DList:
                var dListHolder = (Z64Object.DListHolder)ohe.ObjectHolder;
                DisasDList = new Dlist(
                    dListHolder.GetData(),
                    new SegmentedAddress(_segment, _object.OffsetOf(dListHolder)).VAddr
                );
                break;

            // pretty much verbatim from ObjectAnalyzerForm.listView_map_SelectedIndexChanged
            case Z64Object.EntryType.AnimationHeader:
                {
                    var anim = (Z64Object.AnimationHolder)ohe.ObjectHolder;

                    StringWriter sw = new StringWriter();
                    sw.WriteLine($"Frame Count: {anim.FrameCount}");
                    sw.WriteLine($"Frame Data: 0x{anim.FrameData.VAddr:X8}");
                    sw.WriteLine($"Joint Indices: 0x{anim.JointIndices.VAddr:X8}");
                    sw.WriteLine($"Static Index Max: 0x{anim.StaticIndexMax}");

                    ObjectHolderEntryDetailsViewModel = new TextOHEDViewModel()
                    {
                        Text = sw.ToString()
                    };
                }
                break;
            case Z64Object.EntryType.JointIndices:
                {
                    var joints = (Z64Object.AnimationJointIndicesHolder)ohe.ObjectHolder;
                    StringWriter sw = new StringWriter();
                    sw.WriteLine($"Joints:");
                    foreach (var joint in joints.JointIndices)
                        sw.WriteLine($"{{ frameData[{joint.X}], frameData[{joint.Y}], frameData[{joint.Z}] }}");

                    ObjectHolderEntryDetailsViewModel = new TextOHEDViewModel()
                    {
                        Text = sw.ToString()
                    };
                }
                break;

            default:
                ObjectHolderEntryDetailsViewModel = null;
                break;
        }
    }

    public void UpdateDListDisassembly()
    {
        Debug.Assert(DisasDList != null);
        string text;
        try
        {
            F3DZEX.Disassembler disas = new F3DZEX.Disassembler(DisasDList, F3DZEXDisasConfig);
            var lines = disas.Disassemble();
            StringWriter sw = new StringWriter();
            lines.ForEach(s => sw.WriteLine(s));
            text = sw.ToString();
        }
        catch (Exception)
        {
            text = "ERROR";
        }
        ObjectHolderEntryDetailsViewModel = new TextOHEDViewModel()
        {
            Text = text
        };
    }

    static private F3DZEX.Memory.Segment EMPTY_DLIST_SEGMENT = F3DZEX.Memory.Segment.FromFill("Empty Dlist", new byte[] { 0xDF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

    private void OpenDListViewerObjectHolderEntryCommandExecute(ObjectHolderEntry? ohe)
    {
        Debug.Assert(ohe != null);
        Debug.Assert(OpenDListViewer != null);
        var dlvVM = OpenDListViewer();
        Debug.Assert(_game != null);
        dlvVM.Renderer = new F3DZEX.Render.Renderer(_game, new F3DZEX.Render.Renderer.Config());
        // TODO cleanup, segment config, render config
        dlvVM.SomeTextForNow = "Viewing DL " + ohe.ObjectHolder.Name;
        Debug.Assert(_file != null);
        dlvVM.SetSegment(_segment, F3DZEX.Memory.Segment.FromBytes("[this object]", _file.Data));
        dlvVM.SetSegment(8, EMPTY_DLIST_SEGMENT);
        Debug.Assert(_object != null);
        dlvVM.SetSingleDlist(new SegmentedAddress(_segment, _object.OffsetOf(ohe.ObjectHolder)).VAddr);
    }

    private void OpenSkeletonViewerObjectHolderEntryCommandExecute(ObjectHolderEntry? ohe)
    {
        Debug.Assert(ohe != null);
        Debug.Assert(OpenSkeletonViewer != null);
        var skelvVM = OpenSkeletonViewer();
        Debug.Assert(_game != null);
        skelvVM.Renderer = new F3DZEX.Render.Renderer(_game, new F3DZEX.Render.Renderer.Config());
        // TODO
        Debug.Assert(_file != null);
        skelvVM.SetSegment(_segment, F3DZEX.Memory.Segment.FromBytes("[this object]", _file.Data));
        skelvVM.SetSegment(8, EMPTY_DLIST_SEGMENT);
        skelvVM.SetSegment(9, EMPTY_DLIST_SEGMENT);
        Debug.Assert(_object != null);
        Debug.Assert(ohe.ObjectHolder is Z64Object.SkeletonHolder);
        var skeletonHolder = (Z64Object.SkeletonHolder)ohe.ObjectHolder;
        skelvVM.SetSkeleton(skeletonHolder);
        skelvVM.SetAnimations(
            _object.Entries
                .FindAll(oh => oh.GetEntryType() == Z64Object.EntryType.AnimationHeader)
                .Cast<Z64Object.AnimationHolder>()
        );
    }
}
