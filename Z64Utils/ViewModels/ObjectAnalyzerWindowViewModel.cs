using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using F3DZEX.Command;
using RDP;
using Z64;
using Z64Utils.ViewModels.OHED;
using Z64Utils.Views;

namespace Z64Utils.ViewModels;

public partial class ObjectAnalyzerWindowViewModel : ObservableObject
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private Z64Game? _game;
    private Z64File? _file;
    private int _segment;
    private Z64Object? _object;

    public static string DEFAULT_WINDOW_TITLE = "Object Analyzer";

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
    public Func<Task<IStorageFile?>>? OpenJSONFile;
    public Func<Task<IStorageFile?>>? OpenJSONFileForSave;
    public Func<Task<IStorageFile?>>? OpenXMLFile;
    public Action<DListViewerWindowViewModel>? OpenDListViewer;
    public Action<SkeletonViewerWindowViewModel>? OpenSkeletonViewer;
    public Action<CollisionViewerWindowViewModel>? OpenCollisionViewer;
    public Func<
        Func<F3DZEXDisassemblerSettingsViewModel>,
        F3DZEXDisassemblerSettingsViewModel?
    >? OpenF3DZEXDisassemblerSettings;

    public ICommand OpenDListViewerObjectHolderEntryCommand;
    public ICommand OpenSkeletonViewerObjectHolderEntryCommand;
    public ICommand OpenCollisionViewerObjectHolderEntryCommand;

    public ObjectAnalyzerWindowViewModel()
    {
        OpenDListViewerObjectHolderEntryCommand = new RelayCommand<ObjectHolderEntry>(
            OpenDListViewerObjectHolderEntryCommandExecute
        );
        OpenSkeletonViewerObjectHolderEntryCommand = new RelayCommand<ObjectHolderEntry>(
            OpenSkeletonViewerObjectHolderEntryCommandExecute
        );
        OpenCollisionViewerObjectHolderEntryCommand = new RelayCommand<ObjectHolderEntry>(
            OpenCollisionViewerObjectHolderEntryCommandExecute
        );
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

    public void FindDListsCommand()
    {
        Utils.Assert(HasFile());
        Utils.Assert(_file.Valid());

        // TODO prompt for config
        var config = new Z64ObjectAnalyzer.Config();
        Z64ObjectAnalyzer.FindDlists(_object, _file.Data, _segment, config);
        UpdateMap();
    }

    public bool CanFindDListsCommand(object? parameter)
    {
        return HasFile();
    }

    public void AnalyzeDListsCommand()
    {
        Utils.Assert(HasFile());
        Utils.Assert(_file.Valid());

        Z64ObjectAnalyzer.AnalyzeDlists(_object, _file.Data, _segment);
        UpdateMap();
    }

    public bool CanAnalyzeDListsCommand(object? parameter)
    {
        return HasFile();
    }

    [RelayCommand(CanExecute = nameof(CanImportJSON))]
    private async Task ImportJSON()
    {
        Utils.Assert(OpenJSONFile != null);
        Utils.Assert(HasFile());
        Utils.Assert(_file.Valid());

        var fJSON = await OpenJSONFile();
        if (fJSON == null)
            return;

        var json = File.ReadAllText(fJSON.Path.LocalPath);

        ObjectHolderEntryDetailsViewModel = null;
        ObjectHolderEntryDataBytes = null;
        ObjectHolderEntryFirstByteAddress = 0;
        ObjectHolderEntries.Clear();

        _object = Z64Object.FromJson(json);
        _object.SetData(_file.Data);
        UpdateMap();
    }

    private bool CanImportJSON()
    {
        return HasFile();
    }

    [RelayCommand(CanExecute = nameof(CanExportJSON))]
    private async Task ExportJSON()
    {
        Utils.Assert(HasFile());
        Utils.Assert(OpenJSONFileForSave != null);

        var json = _object.GetJSON();

        var fJSON = await OpenJSONFileForSave();
        if (fJSON == null)
            return;

        File.WriteAllText(fJSON.Path.LocalPath, json);
    }

    private bool CanExportJSON()
    {
        return HasFile();
    }

    [RelayCommand(CanExecute = nameof(CanImportXML))]
    private async Task ImportXML()
    {
        Utils.Assert(OpenXMLFile != null);
        Utils.Assert(HasFile());
        Utils.Assert(_file.Valid());

        var fXML = await OpenXMLFile();
        if (fXML == null)
            return;

        var xml = File.ReadAllText(fXML.Path.LocalPath);

        try
        {
            _object = Z64Object.FromXml(xml, _file.Data);
        }
        catch (Z64Object.Z64ObjectFromXmlException e)
        {
            Logger.Error(e);
            Utils.ReportError(e.Message);
            return;
        }

        ObjectHolderEntryDetailsViewModel = null;
        ObjectHolderEntryDataBytes = null;
        ObjectHolderEntryFirstByteAddress = 0;
        ObjectHolderEntries.Clear();

        UpdateMap();
    }

    private bool CanImportXML()
    {
        return HasFile();
    }

    public void ResetCommand()
    {
        Utils.Assert(HasFile());
        Utils.Assert(_file.Valid());

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
        Utils.Assert(OpenF3DZEXDisassemblerSettings != null);
        var vm = OpenF3DZEXDisassemblerSettings(() => new());
        if (vm == null)
        {
            // Was already open
            return;
        }

        vm.DisasConfig = F3DZEXDisasConfig;
        vm.DisasConfigChanged += (sender, e) =>
        {
            Logger.Debug("F3DZEXDisassemblerSettingsViewModel.DisasConfigChanged");
            F3DZEXDisasConfig = vm.DisasConfig;
        };
    }

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private ObservableCollection<ObjectHolderEntry> _objectHolderEntries = new();

    public class ObjectHolderEntry
    {
        ObjectAnalyzerWindowViewModel _parentVM;

        public string Offset { get; }
        public string Name { get; }
        public string Type { get; }
        public Z64Object.ObjectHolder ObjectHolder { get; }

        public ObjectHolderEntry(
            ObjectAnalyzerWindowViewModel parentVM,
            string offset,
            string name,
            string type,
            Z64Object.ObjectHolder objectHolder
        )
        {
            _parentVM = parentVM;
            Offset = offset;
            Name = name;
            Type = type;
            ObjectHolder = objectHolder;
        }

        public class AvailableAction
        {
            public string? label;
            public ICommand? command;
            public object? commandParameter;
        }

        internal IEnumerable<AvailableAction> GetAvailableActions()
        {
            var availableActions = new List<AvailableAction>();
            switch (ObjectHolder.GetEntryType())
            {
                case Z64Object.EntryType.DList:
                    availableActions.Add(
                        new()
                        {
                            label = "Open in DList Viewer",
                            command = _parentVM.OpenDListViewerObjectHolderEntryCommand,
                            commandParameter = this,
                        }
                    );
                    break;

                case Z64Object.EntryType.SkeletonHeader:
                case Z64Object.EntryType.FlexSkeletonHeader:
                    availableActions.Add(
                        new()
                        {
                            label = "Open in Skeleton Viewer",
                            command = _parentVM.OpenSkeletonViewerObjectHolderEntryCommand,
                            commandParameter = this,
                        }
                    );
                    break;

                case Z64Object.EntryType.CollisionHeader:
                    availableActions.Add(
                        new()
                        {
                            label = "Open in Collision Viewer",
                            command = _parentVM.OpenCollisionViewerObjectHolderEntryCommand,
                            commandParameter = this,
                        }
                    );
                    break;
            }
            availableActions.Add(new() { label = "(TODO)" });
            return availableActions;
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

    [MemberNotNullWhen(true, nameof(_file), nameof(_object))]
    public bool HasFile()
    {
        if (_file == null)
        {
            Utils.Assert(_object == null);
            return false;
        }
        else
        {
            Utils.Assert(_object != null);
            return true;
        }
    }

    public void SetFile(Z64Game? game, string fileName, Z64File file, int segment, bool analyze)
    {
        ClearFile();

        try
        {
            WindowTitle = $"\"{fileName}\" ({file.VRomStart:X8}-{file.VRomEnd:X8})";

            _game = game;
            _file = file;
            _segment = segment;

            Utils.Assert(file.Valid()); // TODO
            _object = new Z64Object(game, file.Data, fileName);

            if (analyze)
            {
                var config = new Z64ObjectAnalyzer.Config();
                Z64ObjectAnalyzer.FindDlists(_object, file.Data, segment, config);

                Z64ObjectAnalyzer.AnalyzeDlists(_object, file.Data, segment);
            }

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
        Utils.Assert(_object != null);

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

            if (
                filterText == ""
                || entry.Name.ToLower().Contains(filterText)
                || addrStr.ToLower().Contains(filterText)
                || entryTypeStr.ToLower().Contains(filterText)
            )
            {
                newObjectHolderEntries.Add(
                    new ObjectHolderEntry(
                        this,
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
        Utils.Assert(_object != null);
        ObjectHolderEntryDataBytes = ohe.ObjectHolder.GetData();
        ObjectHolderEntryFirstByteAddress = (uint)_object.OffsetOf(ohe.ObjectHolder);

        DisasDList = null;

        switch (ohe.ObjectHolder.GetEntryType())
        {
            case Z64Object.EntryType.Texture:
                var textureHolder = (Z64Object.TextureHolder)ohe.ObjectHolder;
                Bitmap? bitmap = null;
                try
                {
                    bitmap = textureHolder.GetBitmap().ToAvaloniaBitmap();
                }
                catch (Exception e)
                {
                    Logger.Warn(e);
                }
                var imageVM = new ImageOHEDViewModel()
                {
                    InfoText =
                        $"{textureHolder.Name} {textureHolder.Format}"
                        + $" {textureHolder.Width}x{textureHolder.Height}"
                        + (
                            textureHolder.Tlut == null
                                ? ""
                                : (
                                    $" (TLUT: {_object.OffsetOf(textureHolder.Tlut):X6}"
                                    + $" {textureHolder.Tlut.Name} {textureHolder.Tlut.Format})"
                                )
                        ),
                    Image = bitmap,
                };
                ObjectHolderEntryDetailsViewModel = imageVM;
                break;

            case Z64Object.EntryType.Vertex:
                var vertexHolder = (Z64Object.VertexHolder)ohe.ObjectHolder;
                uint vertexHolderAddress = new SegmentedAddress(
                    _segment,
                    _object.OffsetOf(vertexHolder)
                ).VAddr;
                var vertexArrayVM = new VertexArrayOHEDViewModel()
                {
                    Vertices = new(
                        vertexHolder.Vertices.Select(
                            (v, i) =>
                                new VertexArrayOHEDViewModel.VertexEntry(
                                    index: i,
                                    address: vertexHolderAddress
                                        + (uint)i * Z64Object.VertexHolder.VERTEX_SIZE,
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
                    Logger.Trace(
                        "ObjectHolderEntryDetailsViewModel = vertexArrayVM; t2-t1={0}ms",
                        t2 - t1
                    );
                });
                break;

            case Z64Object.EntryType.DList:
                var dListHolder = (Z64Object.DListHolder)ohe.ObjectHolder;
                DisasDList = new Dlist(
                    dListHolder.GetData(),
                    new SegmentedAddress(_segment, _object.OffsetOf(dListHolder)).VAddr
                );
                break;

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
                        Text = sw.ToString(),
                    };
                }
                break;
            case Z64Object.EntryType.JointIndices:
                {
                    var joints = (Z64Object.AnimationJointIndicesHolder)ohe.ObjectHolder;
                    StringWriter sw = new StringWriter();
                    sw.WriteLine($"Joints:");
                    foreach (var joint in joints.JointIndices)
                        sw.WriteLine(
                            $"{{ frameData[{joint.X}], frameData[{joint.Y}], frameData[{joint.Z}] }}"
                        );

                    ObjectHolderEntryDetailsViewModel = new TextOHEDViewModel()
                    {
                        Text = sw.ToString(),
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
        Utils.Assert(DisasDList != null);
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
        ObjectHolderEntryDetailsViewModel = new TextOHEDViewModel() { Text = text };
    }

    private static F3DZEX.Memory.Segment EMPTY_DLIST_SEGMENT = F3DZEX.Memory.Segment.FromFill(
        "Empty Dlist",
        new byte[] { 0xDF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
    );

    private void SetBaseSegmentsConfig(F3DZEX.Memory memory)
    {
        if (_game != null)
        {
            var gKeepFile = _game.GetFileByName("gameplay_keep");
            if (gKeepFile != null && gKeepFile.Valid())
            {
                memory.Segments[4] = F3DZEX.Memory.Segment.FromBytes(
                    "gameplay_keep",
                    gKeepFile.Data
                );
            }
        }
        Utils.Assert(_file != null);
        Utils.Assert(_file.Valid());
        memory.Segments[_segment] = F3DZEX.Memory.Segment.FromBytes("[this object]", _file.Data);
        for (int i = 8; i <= 15; i++)
            memory.Segments[i] = EMPTY_DLIST_SEGMENT;
    }

    private void OpenDListViewerObjectHolderEntryCommandExecute(ObjectHolderEntry? ohe)
    {
        Utils.Assert(ohe != null);
        Utils.Assert(_object != null);
        Utils.Assert(OpenDListViewer != null);

        var dlvVM = new DListViewerWindowViewModel(_game)
        {
            Renderer = new F3DZEX.Render.Renderer(_game, new F3DZEX.Render.Renderer.Config()),
        };
        SetBaseSegmentsConfig(dlvVM.Renderer.Memory);

        dlvVM.SetSingleDlist(
            new SegmentedAddress(_segment, _object.OffsetOf(ohe.ObjectHolder)).VAddr
        );

        OpenDListViewer(dlvVM);
    }

    private void OpenSkeletonViewerObjectHolderEntryCommandExecute(ObjectHolderEntry? ohe)
    {
        Utils.Assert(ohe != null);
        Utils.Assert(_object != null);
        Utils.Assert(ohe.ObjectHolder is Z64Object.SkeletonHolder);
        Utils.Assert(OpenSkeletonViewer != null);

        var skelvVM = new SkeletonViewerWindowViewModel(_game)
        {
            Renderer = new F3DZEX.Render.Renderer(_game, new F3DZEX.Render.Renderer.Config()),
        };
        SetBaseSegmentsConfig(skelvVM.Renderer.Memory);

        var skeletonHolder = (Z64Object.SkeletonHolder)ohe.ObjectHolder;
        skelvVM.SetSkeleton(skeletonHolder);
        skelvVM.SetAnimations(
            _object
                .Entries.FindAll(oh => oh.GetEntryType() == Z64Object.EntryType.AnimationHeader)
                .Cast<Z64Object.AnimationHolder>()
        );

        OpenSkeletonViewer(skelvVM);
    }

    private void OpenCollisionViewerObjectHolderEntryCommandExecute(ObjectHolderEntry? ohe)
    {
        Utils.Assert(ohe != null);
        Utils.Assert(OpenCollisionViewer != null);
        var collisionvVM = new CollisionViewerWindowViewModel();
        OpenCollisionViewer(collisionvVM);
        Utils.Assert(ohe.ObjectHolder is Z64Object.ColHeaderHolder);
        var collisionHeaderHolder = (Z64Object.ColHeaderHolder)ohe.ObjectHolder;
        collisionvVM.SetCollisionHeader(collisionHeaderHolder);
    }
}
