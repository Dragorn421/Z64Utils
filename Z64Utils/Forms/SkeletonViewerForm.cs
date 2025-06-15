using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using RDP;
using Syroot.BinaryData;
using static Z64.Z64Object;

namespace Z64.Forms
{
    public partial class SkeletonViewerForm : MicrosoftFontForm
    {
        enum PlayState
        {
            Pause,
            Forward,
            Backward,
        }

        bool _formClosing = false;
        System.Timers.Timer _timer;
        PlayState _playState;
        string? _dlistError = null;

        Z64Game? _game;
        F3DZEX.Render.Renderer _renderer;
        SegmentEditorForm? _segForm;
        DisasmForm? _disasForm;
        SettingsForm? _settingsForm;
        F3DZEX.Render.Renderer.Config _rendererCfg;

        Skelanime.Skeleton _skeleton;
        List<AnimationHolder> _anims;
        List<PlayerAnimationHolder> _playerAnims;
        List<F3DZEX.Command.Dlist?> _limbDlists;
        bool[] _limbDlistRenderFlags;

        Skelanime.Animation? _curAnimation;
        Matrix4[] _curPose;
        int _curPoseFrame;

        Skelanime.PlayerAnimation? _curPlayerAnimation;

        string? _animationError;

        byte[]? _animFile;
        int _curSegment = 6;
        int _extAnimSegment = 6;

        public SkeletonViewerForm(
            Z64Game? game,
            int curSegment,
            F3DZEX.Memory.Segment curSegmentData,
            SkeletonHolder skel,
            List<AnimationHolder> anims
        )
        {
            _game = game;
            _curSegment = curSegment;
            _rendererCfg = new F3DZEX.Render.Renderer.Config();

            InitializeComponent();

            _renderer = new F3DZEX.Render.Renderer(game, _rendererCfg);
            modelViewer.RenderCallback = RenderCallback;

            _timer = new System.Timers.Timer();
            _timer.Elapsed += Timer_Elapsed;
            _timer.Interval = (int)numUpDown_playbackSpeed.Value;

            if ((Control.ModifierKeys & Keys.Control) == 0)
            {
                if (game != null)
                {
                    var gameplay_keepFile = game.GetFileByName("gameplay_keep");
                    if (gameplay_keepFile == null || !gameplay_keepFile.Valid())
                        MessageBox.Show(
                            "Could not find valid gameplay_keep file for setting segment 4"
                        );
                    else
                        _renderer.Memory.Segments[4] = F3DZEX.Memory.Segment.FromBytes(
                            "gameplay_keep",
                            gameplay_keepFile.Data
                        );
                }
                for (int i = 8; i < 16; i++)
                {
                    _renderer.Memory.Segments[i] = F3DZEX.Memory.Segment.FromFill(
                        "Empty Dlist",
                        new byte[] { 0xDF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
                    );
                }
            }

            NewRender();

            FormClosing += (s, e) =>
            {
                if (_timer.Enabled && !_formClosing)
                {
                    _formClosing = true;
                    e.Cancel = true;
                }
            };
            _playState = PlayState.Pause;

            SetSegment(curSegment, curSegmentData);
            SetSkeleton(skel, anims);
            SetIdentityPose();
            _curPoseFrame = -1;
        }

        [MemberNotNull(nameof(_curPose))]
        void SetIdentityPose()
        {
            _curPose = new Matrix4[_skeleton.Limbs.Count];
            for (int i = 0; i < _curPose.Length; i++)
            {
                _curPose[i] = Matrix4.Identity;
            }
        }

        void UpdateCurPose()
        {
            if (_curAnimation == null && _curPlayerAnimation == null)
            {
                SetIdentityPose();
            }
            else
            {
                if (_curPoseFrame != trackBar_anim.Value)
                {
                    if (_curAnimation != null)
                    {
                        _curPose = Skelanime
                            .SkeletonPose.Get(_skeleton, _curAnimation, trackBar_anim.Value)
                            .LimbsPose;
                    }
                    else
                    {
                        Debug.Assert(_curPlayerAnimation != null);
                        _curPose = Skelanime
                            .SkeletonPose.Get(_skeleton, _curPlayerAnimation, trackBar_anim.Value)
                            .LimbsPose;
                    }
                    _curPoseFrame = trackBar_anim.Value;
                }
            }
        }

        void RenderCallback(Matrix4 proj, Matrix4 view)
        {
            if (_dlistError != null)
            {
                toolStripErrorLabel.Text = _dlistError;
                return;
            }

            _renderer.RenderStart(proj, view);
            UpdateCurPose();
            _skeleton.Root.Visit(index =>
            {
                _renderer.RdpMtxStack.Load(_curPose[index]);

                var node = treeView_hierarchy.SelectedNode;
                _renderer.SetHightlightEnabled(node?.Tag?.Equals(_skeleton.Limbs[index]) ?? false);

                var dl = _limbDlists[index];
                if (dl != null && _limbDlistRenderFlags[index])
                    _renderer.RenderDList(dl);
            });

            if (_renderer.RenderFailed())
            {
                toolStripErrorLabel.Text =
                    $"RENDER ERROR AT 0x{_renderer.RenderErrorAddr:X8}! ({_renderer.ErrorMsg})";
            }
            else if (!string.IsNullOrEmpty(_animationError))
            {
                toolStripErrorLabel.Text = _animationError;
            }
            else
            {
                toolStripErrorLabel.Text = "";
            }
        }

        private void TreeView_hierarchy_AfterSelect(object sender, EventArgs e)
        {
            var tag = treeView_hierarchy.SelectedNode?.Tag ?? null;
            if (tag != null && tag is SkeletonLimbHolder)
            {
                var dlist = _limbDlists[_skeleton.Limbs.IndexOf((SkeletonLimbHolder)tag)];
                if (dlist != null)
                    _disasForm?.UpdateDlist(dlist);
                else
                    _disasForm?.SetMessage("Empty limb");
            }

            NewRender();
        }

        private void treeView_hierarchy_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var tag = e.Node.Tag ?? null;
                if (tag != null && tag is SkeletonLimbHolder)
                {
                    var index = _skeleton.Limbs.IndexOf((SkeletonLimbHolder)tag);
                    _limbDlistRenderFlags[index] = !_limbDlistRenderFlags[index];
                    if (!_limbDlistRenderFlags[index])
                        e.Node.ForeColor = Color.Gray;
                    else
                        e.Node.ForeColor = Color.Black;

                    treeView_hierarchy.SelectedNode = e.Node;
                }

                NewRender();
            }
        }

        private void NewRender(object? sender = null, EventArgs? e = null)
        {
            _renderer.ClearErrors();
            toolStripErrorLabel.Text = "";
            modelViewer.Render();
        }

        [MemberNotNull(nameof(_skeleton), nameof(_anims), nameof(_playerAnims))]
        [MemberNotNull(nameof(_limbDlistRenderFlags))]
        [MemberNotNull(nameof(_limbDlists))]
        public void SetSkeleton(SkeletonHolder skel, List<AnimationHolder> anims)
        {
            if (skel is FlexSkeletonHolder flexSkel)
                _skeleton = Skelanime.FlexSkeleton.Get(_renderer.Memory, flexSkel);
            else
                _skeleton = Skelanime.Skeleton.Get(_renderer.Memory, skel);
            _anims = anims;
            _playerAnims = new List<PlayerAnimationHolder>();

            listBox_anims.Items.Clear();
            _anims.ForEach(a => listBox_anims.Items.Add(a.Name));

            UpdateSkeleton();
            NewRender();
        }

        [MemberNotNull(nameof(_limbDlists))]
        void UpdateLimbsDlists()
        {
            _dlistError = null;
            _limbDlists = new();

            foreach (var limb in _skeleton.Limbs)
            {
                if (limb.Type != EntryType.StandardLimb && limb.Type != EntryType.LODLimb)
                    throw new Exception($"Unimplemented limb type in skeleton viewer {limb.Type}");
                Debug.Assert(limb.DListSeg != null); // always set for Standard and LOD limbs
                F3DZEX.Command.Dlist? dlist = null;
                try
                {
                    if (limb.DListSeg.VAddr != 0)
                        dlist = _renderer.GetDlist(limb.DListSeg);
                }
                catch (Exception ex)
                {
                    if (_dlistError == null)
                        _dlistError =
                            $"Error while decoding dlist 0x{limb.DListSeg.VAddr:X8} : {ex.Message}";
                }
                _limbDlists.Add(dlist);
            }
        }

        // Updates skeleton -> limbs / limbs dlists -> matrices
        [MemberNotNull(nameof(_limbDlistRenderFlags))]
        [MemberNotNull(nameof(_limbDlists))]
        void UpdateSkeleton()
        {
            treeView_hierarchy.Nodes.Clear();
            treeView_hierarchy.Nodes.Add("skeleton");

            _limbDlistRenderFlags = new bool[_skeleton.Limbs.Count];
            for (int i = 0; i < _limbDlistRenderFlags.Length; i++)
            {
                _limbDlistRenderFlags[i] = true;
            }

            UpdateLimbsDlists();
            UpdateLimbs();
        }

        // Updates limbs -> matrices
        void UpdateLimbs()
        {
            TreeNode skelNode = treeView_hierarchy.Nodes[0];

            if (_skeleton.Limbs.Count > 0)
                AddLimbRoutine(skelNode, _skeleton.Root);

            UpdateMatrixBuf();
        }

        void AddLimbRoutine(TreeNode parent, Skelanime.SkeletonTreeLimb treeLimb)
        {
            var node = parent.Nodes.Add($"limb_{treeLimb.Index}");
            node.Tag = _skeleton.Limbs[treeLimb.Index];

            if (treeLimb.Sibling != null)
                AddLimbRoutine(parent, treeLimb.Sibling);
            if (treeLimb.Child != null)
                AddLimbRoutine(node, treeLimb.Child);
        }

        // Update anims -> matrices
        void UpdateAnim()
        {
            Debug.Assert(_curAnimation != null);
            trackBar_anim.Minimum = 0;
            trackBar_anim.Maximum = _curAnimation.FrameCount - 1;
            trackBar_anim.Value = 0;

            /*
            var Saved = _renderer.Memory.Segments[_curSegment];

            if (_curAnim.extAnim)
            {
                Debug.Assert(_animFile != null); // extAnim = true is only set with _animFile set
                _renderer.Memory.Segments[_extAnimSegment] = F3DZEX.Memory.Segment.FromBytes(
                    "",
                    _animFile
                );
            }
            */

            /*
            var curSegmentData = _renderer.Memory.Segments[_curSegment].Data;
            Debug.Assert(curSegmentData != null);
            if (bytesToRead + _curAnim.FrameData.SegmentOff > curSegmentData.Length)
            {
                _curAnim = null;
                _curJoints = null;
                _frameData = null;
                _animationError =
                    "Animation is glitchy; displaying folded pose. To view this animation, load it in-game.";
            }
            */

            //_renderer.Memory.Segments[_curSegment] = Saved;

            UpdateMatrixBuf();
        }

        void UpdatePlayerAnim()
        {
            Debug.Assert(_curPlayerAnimation != null);

            trackBar_anim.Minimum = 0;
            trackBar_anim.Maximum = _curPlayerAnimation.FrameCount - 1;
            trackBar_anim.Value = 0;

            UpdateMatrixBuf();
        }

        // Flex Only
        void UpdateMatrixBuf()
        {
            if (!(_skeleton is Skelanime.FlexSkeleton flexSkeleton))
                return;

            UpdateCurPose();

            byte[] mtxBuff = new byte[flexSkeleton.DListCount * Mtx.SIZE];

            using (MemoryStream ms = new MemoryStream(mtxBuff))
            {
                BinaryStream bw = new BinaryStream(ms, Syroot.BinaryData.ByteConverter.Big);

                _skeleton.Root.Visit(index =>
                {
                    if (_limbDlists[index] != null)
                        Mtx.FromMatrix4(_curPose[index]).Write(bw);
                });
            }

            _renderer.Memory.Segments[0xD] = F3DZEX.Memory.Segment.FromBytes(
                "[RESERVED] Anim Matrices",
                mtxBuff
            );
        }

        private void ToolStripRenderCfgBtn_Click(object sender, System.EventArgs e)
        {
            if (_settingsForm != null)
            {
                _settingsForm.Activate();
            }
            else
            {
                _settingsForm = new SettingsForm(_rendererCfg);
                _settingsForm.FormClosed += (sender, e) =>
                {
                    _settingsForm = null;
                };
                _settingsForm.SettingsChanged += NewRender;
                _settingsForm.Show();
            }
        }

        private void ToolStripDisassemblyBtn_Click(object sender, System.EventArgs e)
        {
            if (_disasForm != null)
            {
                _disasForm.Activate();
            }
            else
            {
                _disasForm = new DisasmForm(defaultText: "No limb selected");

                _disasForm.FormClosed += (sender, e) => _disasForm = null;
                _disasForm.Show();
            }

            var tag = treeView_hierarchy.SelectedNode?.Tag ?? null;
            if (tag != null && tag is SkeletonLimbHolder)
            {
                var dlist = _limbDlists[_skeleton.Limbs.IndexOf((SkeletonLimbHolder)tag)];
                _disasForm.UpdateDlist(dlist);
            }
        }

        private void SkeletonViewerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _disasForm?.Close();
            _segForm?.Close();
            _settingsForm?.Close();
        }

        public void SetSegment(int idx, F3DZEX.Memory.Segment seg)
        {
            if (idx < 0 || idx > F3DZEX.Memory.Segment.COUNT)
                throw new IndexOutOfRangeException();

            _renderer.Memory.Segments[idx] = seg;

            if (_skeleton != null)
                UpdateLimbsDlists();
        }

        private void ToolStripSegmentsBtn_Click(object sender, System.EventArgs e)
        {
            if (_segForm != null)
            {
                _segForm.Activate();
            }
            else
            {
                _segForm = new SegmentEditorForm(_game, _renderer);
                _segForm.SegmentsChanged += (sender, e) =>
                {
                    if (e.SegmentID == 0xD && _skeleton is Skelanime.FlexSkeleton)
                        MessageBox.Show(
                            "Error",
                            "Cannot set segment 13 (reserved for animation matrices)"
                        );
                    else
                    {
                        _renderer.Memory.Segments[e.SegmentID] = e.Segment;

                        UpdateLimbsDlists();
                        NewRender();
                    }
                };
                _segForm.FormClosed += (sender, e) => _segForm = null;
                _segForm.Show();
            }
        }

        private void listBox_anims_SelectedIndexChanged(object sender, EventArgs e)
        {
            button_playAnim.Enabled =
                button_playbackAnim.Enabled =
                trackBar_anim.Enabled =
                    listBox_anims.SelectedIndex >= 0;

            _curAnimation = null;
            _curPlayerAnimation = null;
            if (listBox_anims.SelectedIndex >= 0)
            {
                if (listBox_anims.SelectedIndex < _anims.Count)
                {
                    var anim = _anims[listBox_anims.SelectedIndex];
                    _curAnimation = Skelanime.Animation.Get(
                        _renderer.Memory,
                        anim,
                        _skeleton.Limbs.Count
                    );
                    UpdateAnim();
                }
                else
                {
                    var playerAnim = _playerAnims[listBox_anims.SelectedIndex - _anims.Count];

                    if (_game == null)
                        return;

                    var Saved = _renderer.Memory.Segments[
                        playerAnim.PlayerAnimationSegment.SegmentId
                    ];
                    var link_animetionFile = _game.GetFileByName("link_animetion");
                    Debug.Assert(link_animetionFile != null);
                    Debug.Assert(link_animetionFile.Valid());
                    _renderer.Memory.Segments[playerAnim.PlayerAnimationSegment.SegmentId] =
                        F3DZEX.Memory.Segment.FromBytes("link_animetion", link_animetionFile.Data);

                    _curPlayerAnimation = Skelanime.PlayerAnimation.Get(
                        _renderer.Memory,
                        playerAnim
                    );

                    _renderer.Memory.Segments[playerAnim.PlayerAnimationSegment.SegmentId] = Saved;

                    UpdatePlayerAnim();
                }

                NewRender();
            }

            //if (_playState != PlayState.Pause)
            //    _timer.Start()
        }

        private void trackBar_anim_ValueChanged(object sender, EventArgs e)
        {
            label_anim.Text = $"{trackBar_anim.Value}/{trackBar_anim.Maximum}";
            UpdateMatrixBuf();
            NewRender();
        }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (this.IsDisposed || _formClosing)
            {
                _timer.Stop();
                Invoke(new Action(Close));
                return;
            }

            Invoke(
                new Action(() =>
                {
                    if (_playState == PlayState.Forward)
                    {
                        trackBar_anim.Value =
                            trackBar_anim.Value < trackBar_anim.Maximum
                                ? trackBar_anim.Value + 1
                                : 0;
                    }
                    else
                    {
                        trackBar_anim.Value =
                            trackBar_anim.Value > 0
                                ? trackBar_anim.Value - 1
                                : trackBar_anim.Maximum;
                    }
                })
            );
        }

        private void button_playbackAnim_Click(object sender, EventArgs e)
        {
            if (_playState == PlayState.Backward)
            {
                _playState = PlayState.Pause;
                _timer.Stop();
                button_playbackAnim.BackgroundImage = Properties.Resources.playback_icon;
            }
            else
            {
                _playState = PlayState.Backward;
                _timer.Start();
                button_playbackAnim.BackgroundImage = Properties.Resources.pause_icon;
                button_playAnim.BackgroundImage = Properties.Resources.play_icon;
            }
        }

        private void button_playAnim_Click(object sender, EventArgs e)
        {
            if (_playState == PlayState.Forward)
            {
                _playState = PlayState.Pause;
                _timer.Stop();
                button_playAnim.BackgroundImage = Properties.Resources.play_icon;
            }
            else
            {
                _playState = PlayState.Forward;
                _timer.Start();
                button_playAnim.BackgroundImage = Properties.Resources.pause_icon;
                button_playbackAnim.BackgroundImage = Properties.Resources.playback_icon;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            _timer.Stop();
            _timer.Interval = (int)numUpDown_playbackSpeed.Value;
            _playState = PlayState.Pause;
            button_playAnim.BackgroundImage = Properties.Resources.play_icon;
            button_playbackAnim.BackgroundImage = Properties.Resources.playback_icon;
        }

        private void listBox_anims_DoubleClick(object sender, EventArgs e)
        {
            _timer.Stop();
            _playState = PlayState.Pause;
            button_playAnim.BackgroundImage = Properties.Resources.play_icon;
            button_playbackAnim.BackgroundImage = Properties.Resources.playback_icon;

            OpenFileDialog of = new OpenFileDialog();
            DialogResult DR = of.ShowDialog();

            if (DR == DialogResult.OK)
            {
                _animFile = File.ReadAllBytes(of.FileName);
                int segment = _curSegment;
                if (of.FileName.Contains("gameplay_keep"))
                {
                    segment = 4;
                }
                else if (
                    of.FileName.Contains("gameplay_dangeon_keep")
                    || of.FileName.Contains("gameplay_field_keep")
                )
                {
                    segment = 5;
                }
                else if ((segment == 4 || segment == 5) && !of.FileName.Contains("keep"))
                {
                    segment = 6;
                }
                _extAnimSegment = segment;

                using (var form = new ObjectAnalyzerForm(_game, _animFile, of.FileName, segment))
                {
                    _anims.Clear();
                    _playerAnims.Clear();

                    form._obj.Entries.ForEach(e =>
                    {
                        if (e is Z64Object.AnimationHolder eAnim)
                        {
                            eAnim.extAnim = true;
                            eAnim.Name = "ext_" + eAnim.Name;
                            _anims.Add(eAnim);
                        }
                        if (e is Z64Object.PlayerAnimationHolder ePlayerAnim)
                        {
                            ePlayerAnim.extAnim = true;
                            ePlayerAnim.Name = "ext_" + ePlayerAnim.Name;
                            _playerAnims.Add(ePlayerAnim);
                        }
                    });
                }

                listBox_anims.Items.Clear();
                _anims.ForEach(a => listBox_anims.Items.Add(a.Name));
                _playerAnims.ForEach(a => listBox_anims.Items.Add(a.Name));
            }
        }
    }
}
