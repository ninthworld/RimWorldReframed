using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal sealed class ReframedSaveBrowser : Dialog_SaveFileList_Load
    {
        private const string All = "all", Unknown = "unknown";
        private float layoutScale = 1f, styleScale = -1f, styleControlScale = -1f;
        private float S(float value) => value * layoutScale;
        // Enlarging the shell gives its contents space, not a second UI zoom.
        // Match new-game's native 12/14/16 text roles and 32-unit actions while
        // retaining the existing fit-down behavior on constrained viewports.
        private float ControlScale => EntryScene.Active ? Mathf.Min(layoutScale, NewGameSetup.FitScale) : 1f;
        private float C(float value) => value * ControlScale;
        private float RowHeight => S(32);
        private static readonly Color Shell = Theme.Rgb(0x0D1318), Header = Theme.Rgb(0x060B0B),
            Card = Theme.Rgb(0x060A0D), Rule = Theme.Rgb(0x1B252C), Edge = Theme.Rgb(0x27323B),
            ListInk = Theme.Rgb(0xBAC9D2), SecondaryInk = Theme.Rgb(0x8597A3);
        private readonly List<SaveFileInfo> visible = new List<SaveFileInfo>();
        private readonly Dictionary<string, MetadataResult> metadata = new Dictionary<string, MetadataResult>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> colonyColors = new Dictionary<string, Color>();
        private readonly ConcurrentQueue<MetadataResult> completed = new ConcurrentQueue<MetadataResult>();
        private readonly SemaphoreSlim metadataGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource scanCancel;
        private Task scanTask;
        private int generation;
        private string query = "", colony = All, selectedPath, notice, listError;
        private bool searchOpen, focusSearch;
        private Vector2 listScroll;
        private float tabScroll;
        private FileStamp selectedStamp;
        private GUIStyle body, meta, title, search, wrapped, tooltipStyle;
        private static GUIStyle scrollTrack, scrollThumb;
        private Texture2D previewTexture;
        private byte[] decodedPreview;
        private static Texture2D previewFade, listFade, tabFade;
        private string hoveredTip, previousTip;
        private float tipSince;
        private bool entryMotion, contentInteractive = true;
        private float contentOpacity = 1f;
        internal static Color TransitionShellColor => PanelColor(Shell);
        private Color ContentColor(Color color) { color.a *= contentOpacity; return color; }

        internal static void Open()
        {
            if (Find.WindowStack == null || Find.WindowStack.IsOpen<ReframedSaveBrowser>()) return;
            bool animate = EntryScene.Active && !Find.WindowStack.AnyWindowAbsorbingAllInput;
            var browser = new ReframedSaveBrowser();
            Find.WindowStack.Add(browser);
            if (animate)
            {
                browser.entryMotion = true;
                EntryMenuTransition.Open(browser);
            }
        }
        internal ReframedSaveBrowser()
        {
            doCloseButton = doCloseX = doWindowBackground = drawShadow = false;
            closeOnAccept = false;
            RebuildVisible();
            if (visible.Count > 0) Select(visible[0]);
            BeginMetadataScan();
        }
        protected override float Margin => 0;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(546, UI.screenWidth - 24), Mathf.Min(374, UI.screenHeight - 24));
        protected override void SetInitialSizeAndPosition()
        {
            if (EntryScene.Active) windowRect = EntryScene.BrowserBounds;
            else base.SetInitialSizeAndPosition();
        }
        protected override void ReloadFiles()
        {
            try { base.ReloadFiles(); listError = null; }
            catch (Exception ex)
            {
                files.Clear(); listError = "Saved games could not be read.";
                Log.Warning("[ReframedUI] Save list unavailable: " + ex.Message);
            }
        }
        public override void PostClose()
        {
            EntryMenuTransition.Forget(this);
            CancelScan();
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null; decodedPreview = null;
            base.PostClose();
        }
        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (!entryMotion) return;
            if (!EntryScene.Active)
            {
                EntryMenuTransition.Forget(this); entryMotion = false;
            }
            else if (EntryMenuTransition.ReadyToClose(this))
            {
                entryMotion = false;
                EntryMenuTransition.Forget(this);
                base.Close();
            }
        }
        public override void OnCancelKeyPressed()
        {
            if (entryMotion && EntryMenuTransition.Owns(this))
            {
                EntryMenuTransition.Back(this);
                Event.current?.Use();
            }
            else base.OnCancelKeyPressed();
        }
        private void Back()
        {
            if (entryMotion && EntryMenuTransition.Owns(this)) EntryMenuTransition.Back(this);
            else Close();
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!IsOpen) return;
            bool transition = entryMotion && EntryMenuTransition.Owns(this);
            contentInteractive = !transition || !EntryMenuTransition.BlocksInput;
            contentOpacity = transition ? EntryMenuTransition.BrowserOpacity : 1f;
            if (!contentInteractive && Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
            {
                Event.current.Use();
                return;
            }
            if (contentOpacity <= 0) return;
            // GUI.Window owns the root bounds and native content clip. Keep its
            // matrix untouched and express every authored distance in local UI units.
            float nextScale = EntryScene.Active ? rect.width / 546f : 1f;
            if (!Mathf.Approximately(layoutScale, nextScale))
            {
                float ratio = nextScale / layoutScale;
                listScroll *= ratio;
                tabScroll *= ratio;
                layoutScale = nextScale;
            }
            PumpMetadata(); EnsureStyles(); hoveredTip = null;
            using (new Theme.GuiScope(true))
            {
                GUI.color = ContentColor(Color.white); GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.WordWrap = false;
                bool rootShell = transition && EntryMenuTransition.PaintsShell;
                if (!rootShell) Theme.RoundRect(rect, PanelColor(Shell), S(16));
                // Two translated groups preserve the native local origin while
                // clipping incoming content to the moving root-space shell.
                if (rootShell)
                {
                    Rect root = EntryMenuTransition.ShellBounds;
                    Rect clip = new Rect(root.x - windowRect.x, root.y - windowRect.y, root.width, root.height);
                    GUI.BeginGroup(clip);
                    GUI.BeginGroup(new Rect(-clip.x, -clip.y, rect.width, rect.height));
                }
                try
                {
                    DrawHeader(rect);
                    if (!IsOpen) return;
                    float detailWidth = Mathf.Min(S(239), rect.width * .46f);
                    Rect detail = new Rect(rect.width - S(17) - detailWidth, S(64), detailWidth, Mathf.Max(S(1), rect.height - S(80)));
                    Rect list = new Rect(S(16), S(64), Mathf.Max(S(1), detail.x - S(32)), detail.height);
                    DrawList(list); DrawDetails(detail); if (contentInteractive) DrawTooltip(rect);
                }
                finally { if (rootShell) { GUI.EndGroup(); GUI.EndGroup(); } }
            }
        }
        private void EnsureStyles()
        {
            using (new Theme.GuiScope(true))
            {
                Text.Font = GameFont.Small;
                GUIStyle native = Text.CurFontStyle;
                if (body != null && body.font == native.font && Mathf.Approximately(styleScale, layoutScale) &&
                    Mathf.Approximately(styleControlScale, ControlScale)) return;
                styleScale = layoutScale;
                styleControlScale = ControlScale;
                body = Clone(native, 14); meta = Clone(native, 12); title = Clone(native, 16);
                wrapped = new GUIStyle(body) { wordWrap = true, alignment = TextAnchor.UpperLeft };
                tooltipStyle = new GUIStyle(meta) { wordWrap = true, alignment = TextAnchor.UpperLeft };
                search = Clone(native, 14);
                int searchInset = Mathf.RoundToInt(C(8));
                search.padding = new RectOffset(searchInset, searchInset, 0, 0);
                search.hover.textColor = search.active.textColor = search.focused.textColor = Color.white;
                if (previewFade == null) previewFade = Fade(Card, false, 1);
                if (listFade == null) listFade = Fade(Shell, false, .58f);
                if (tabFade == null) tabFade = Fade(Header, true, 1);
                if (scrollTrack == null)
                {
                    scrollTrack = ScrollStyle(Theme.Rgb(0x10171C));
                    scrollThumb = ScrollStyle(Theme.Rgb(0x23323D));
                    scrollThumb.hover.background = scrollThumb.active.background = Solid(Theme.Rgb(0x3A4B58));
                }
                scrollThumb.fixedWidth = C(4);
                int scrollInset = Mathf.RoundToInt(C(3));
                scrollThumb.margin = new RectOffset(scrollInset, scrollInset, 0, 0);
            }
        }
        private GUIStyle Clone(GUIStyle native, int size)
        {
            var style = new GUIStyle(native) { fontSize = Mathf.Max(1, Mathf.RoundToInt(C(size))), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft,
                wordWrap = false, richText = false, clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset(),
                contentOffset = native.contentOffset * ControlScale };
            style.normal.textColor = Color.white;
            return style;
        }
        private void Label(Rect rect, string text, GUIStyle style, Color color, bool centered = false)
        {
            using (new Theme.GuiScope(true))
            {
                GUI.color = ContentColor(color);
                float offset = centered ? Mathf.Max(0, (rect.width - style.CalcSize(new GUIContent(text ?? "")).x) / 2) : 0;
                GUI.BeginGroup(rect);
                try { GUI.Label(new Rect(offset, 0, Mathf.Max(0, rect.width - offset), rect.height), text ?? "", style); }
                finally { GUI.EndGroup(); }
            }
        }
        private void DrawHeader(Rect rect)
        {
            GUI.BeginGroup(new Rect(0, 0, rect.width, S(49)));
            try
            {
            Theme.RoundRect(new Rect(0, 0, rect.width, S(64)), PanelColor(Header), S(16));
            // Header controls keep their open bottoms flush with the divider.
            float controlTop = S(49) - C(33);
            float searchLeft = S(24) + C(32);
            if (IconButton(new Rect(S(16), controlTop, C(32), C(33)), Icon.Back, "Back")) Back();
            if (IconButton(new Rect(searchLeft, controlTop, C(32), C(33)), Icon.Search, "Search saves", active: searchOpen))
            {
                searchOpen = !searchOpen; focusSearch = searchOpen;
                if (!searchOpen) { query = ""; listScroll = Vector2.zero; RebuildVisible(); GUI.FocusControl(null); }
            }
            float tabsLeft = searchLeft + C(32) + S(16);
            Rect tabsRect = new Rect(tabsLeft, controlTop, Mathf.Max(S(1), rect.width - tabsLeft - S(8)), C(33));
            if (searchOpen)
            {
                TabFace(tabsRect, Header);
                GUI.SetNextControlName("ReframedSaveSearch");
                string next = GUI.TextField(new Rect(tabsRect.x, tabsRect.y, tabsRect.width, C(32)), query, 128, search);
                if (focusSearch) { GUI.FocusControl("ReframedSaveSearch"); focusSearch = false; }
                if (next != query) { query = next; listScroll = Vector2.zero; RebuildVisible(); }
            }
            else DrawTabs(tabsRect);
            Widgets.DrawBoxSolid(new Rect(0, S(48), rect.width, S(1)), ContentColor(Rule));
            }
            finally { GUI.EndGroup(); }
        }
        private void DrawTabs(Rect rect)
        {
            var tabs = new List<Tab> { new Tab { Id = All, Name = "All" } };
            var known = new HashSet<string>();
            foreach (var save in files)
            {
                HeaderDetails data = Data(save);
                string id = data?.ColonyId ?? Unknown;
                if (known.Add(id)) tabs.Add(new Tab { Id = id, Name = data?.ColonyName ?? "Unknown colony", World = data?.WorldName });
            }
            float total = 0;
            foreach (var tab in tabs)
            {
                float requested = body.CalcSize(new GUIContent(tab.Name)).x + C(tab.Id == All ? 22 : 46);
                // Keep uncapped labels clear of both fractional-width cancellation
                // and glyph clipping after native window/UI-scale rounding.
                tab.Width = Mathf.Clamp(Mathf.Ceil(requested) + 1f, C(40), C(190));
                total += tab.Width + S(8);
            }
            total = Mathf.Max(0, total - S(8));
            bool overflow = total > rect.width;
            Rect viewport = rect;
            if (overflow) viewport.width -= C(36);
            if (contentInteractive && overflow && Mouse.IsOver(rect))
            {
                if (Event.current.type == EventType.ScrollWheel)
                { tabScroll += (Event.current.delta.y + Event.current.delta.x) * S(35); Event.current.Use(); }
                if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
                {
                    var options = tabs.Select(tab => new FloatMenuOption(tab.Name, () =>
                    { colony = tab.Id; listScroll = Vector2.zero; RebuildVisible(); RevealTab(tabs, tab.Id, viewport.width); })).ToList();
                    Find.WindowStack.Add(new FloatMenu(options)); Event.current.Use();
                }
            }
            float maximum = Mathf.Max(0, total - viewport.width);
            tabScroll = Mathf.Clamp(tabScroll, 0, maximum);
            if (overflow)
            {
                using (new Theme.GuiScope(true))
                {
                    GUI.enabled = tabScroll > S(.5f);
                    if (IconButton(new Rect(rect.xMax - C(34), rect.y, C(16), C(33)), Icon.Left, "Previous colonies"))
                        tabScroll = Mathf.Max(0, tabScroll - viewport.width * .7f);
                    GUI.enabled = tabScroll < maximum - S(.5f);
                    if (IconButton(new Rect(rect.xMax - C(16), rect.y, C(16), C(33)), Icon.Right, "Next colonies"))
                        tabScroll = Mathf.Min(maximum, tabScroll + viewport.width * .7f);
                }
            }
            bool pointerInViewport = Mouse.IsOver(viewport);
            GUI.BeginGroup(viewport);
            try
            {
                float x = -tabScroll;
                foreach (var tab in tabs)
                {
                    Rect button = new Rect(x, 0, tab.Width, C(33));
                    bool active = tab.Id == colony;
                    TabFace(button, active ? Theme.Rgb(0x212932) : Header);
                    float inset = C(10);
                    if (tab.Id != All) { Image(new Rect(x + C(12), C(8), C(16), C(16)), SaveBrowserIcons.House, ColonyColor(tab.Id)); inset = C(38); }
                    float nameWidth = tab.Width - inset - C(8);
                    Label(new Rect(x + inset, 0, nameWidth, C(32)), Ellipsis(tab.Name, body, nameWidth), body, Color.white);
                    if (pointerInViewport) Tip(button, tab.Name + (tab.World == null ? "" : "\n" + tab.World) +
                        (overflow ? "\nUse the arrows or scroll for more colonies; right-click for the list." : ""));
                    if (Widgets.ButtonInvisible(button)) { colony = tab.Id; listScroll = Vector2.zero; RebuildVisible(); }
                    x += tab.Width + S(8);
                }
                if (overflow && tabScroll < maximum - S(.5f))
                    GUI.DrawTexture(new Rect(viewport.width - C(28), 0, C(28), C(32)), tabFade);
                if (overflow && tabScroll > S(.5f))
                    GUI.DrawTextureWithTexCoords(new Rect(0, 0, C(18), C(32)), tabFade, new Rect(1, 0, -1, 1));
            }
            finally { GUI.EndGroup(); }
        }
        private void RevealTab(List<Tab> tabs, string id, float width)
        {
            float x = 0;
            foreach (var tab in tabs)
            {
                if (tab.Id == id) { tabScroll = Mathf.Max(0, x - Mathf.Max(0, width - tab.Width)); return; }
                x += tab.Width + S(8);
            }
        }
        private void DrawList(Rect rect)
        {
            string status = notice ?? listError;
            if (status != null)
            {
                float height = Mathf.Clamp(tooltipStyle.CalcHeight(new GUIContent(status), rect.width), S(18), S(48));
                Rect message = new Rect(rect.x, rect.yMax - height, rect.width, height);
                Label(message, status, tooltipStyle, SecondaryInk);
                Tip(message, status); rect.height -= height + S(4);
            }
            if (visible.Count == 0)
            { Label(new Rect(rect.x, rect.y, rect.width, S(35)), files.Count == 0 ? "No saved games found" : "No matching saves", body, SecondaryInk); return; }
            float contentHeight = visible.Count * RowHeight;
            bool overflow = contentHeight > rect.height;
            float width = rect.width - (overflow ? S(12) : 0);
            listScroll = GUI.BeginScrollView(new Rect(rect.x, rect.y, width, rect.height), listScroll,
                new Rect(0, 0, width, contentHeight), false, false, GUIStyle.none, GUIStyle.none);
            try
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    float y = i * RowHeight;
                    if (y + RowHeight < listScroll.y || y > listScroll.y + rect.height) continue;
                    SaveFileInfo save = visible[i]; Rect row = new Rect(0, y, width, RowHeight);
                    if (contentInteractive && Mouse.IsOver(row)) Theme.RoundRect(row, Theme.Rgb(0x17232B), S(2));
                    else if (SamePath(selectedPath, save.FileInfo.FullName)) Theme.RoundRect(row, Theme.Rgb(0x101920), S(2));
                    bool auto = SaveGameFilesUtility.IsAutoSave(Path.GetFileNameWithoutExtension(save.FileName));
                    Image(new Rect(S(11) - C(8), y + RowHeight / 2 - C(8), C(16), C(16)), SaveBrowserIcons.Save, auto ? Theme.Rgb(0xE0A05B) : Theme.Rgb(0x84929F));
                    string age = Age(save.LastWriteTime);
                    float ageWidth = Mathf.Min(S(80), meta.CalcSize(new GUIContent(age)).x + S(2));
                    float nameWidth = Mathf.Max(S(1), width - ageWidth - S(36));
                    Label(new Rect(S(28), y, nameWidth, S(31)), Ellipsis(Path.GetFileNameWithoutExtension(save.FileName), body, nameWidth), body, ListInk);
                    Label(new Rect(width - ageWidth, y, ageWidth, S(31)), age, meta, SecondaryInk);
                    Widgets.DrawBoxSolid(new Rect(S(1), y + S(31), width - S(1), S(1)), ContentColor(Rule));
                    Tip(row, Path.GetFileNameWithoutExtension(save.FileName) + "\n" + save.LastWriteTime.ToString("g") + " · " + save.GameVersion + "\nDouble-click to load.");
                    if (contentInteractive && GUI.enabled && Event.current.type == EventType.MouseDown &&
                        Event.current.button == 0 && Event.current.clickCount == 2 && Mouse.IsOver(row) &&
                        SamePath(selectedPath, save.FileInfo.FullName))
                    {
                        Event.current.Use();
                        LoadSelected(save);
                        return;
                    }
                    if (Widgets.ButtonInvisible(row)) Select(save);
                }
            }
            finally { GUI.EndScrollView(); }
            if (overflow)
            {
                scrollThumb.fixedHeight = Mathf.Max(S(28), rect.height * rect.height / contentHeight);
                listScroll.y = GUI.VerticalSlider(new Rect(rect.xMax - S(10), rect.y, S(10), rect.height),
                    listScroll.y, 0, contentHeight - rect.height, scrollTrack, scrollThumb);
            }
            if (visible.Count * RowHeight > rect.height - S(64))
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - S(72), width, S(72)), listFade);
        }
        private void DrawDetails(Rect rect)
        {
            Theme.RoundRect(rect, PanelColor(Card), S(7));
            SaveFileInfo save = Selected();
            if (save == null) { Label(rect.ContractedBy(S(12)), "Select a saved game", body, SecondaryInk, true); return; }
            HeaderDetails data = Data(save, selectedStamp);
            PreparePreview(data?.Preview);
            GUI.BeginGroup(rect);
            try
            {
                float w = rect.width;
                Rect preview = new Rect(0, 0, w, S(132));
                if (previewTexture != null && Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(preview, previewTexture, ScaleMode.ScaleAndCrop, true, 0, ContentColor(Color.white), 0, S(7));
                else
                {
                    Theme.RoundRect(preview, Theme.Rgb(0x121B20), S(7));
                    Label(new Rect(S(8), S(42), w - S(16), S(28)), "No preview available", meta, SecondaryInk, true);
                    Tip(preview, "A colony preview is recorded when this save is loaded or saved with Reframed UI.");
                }
                GUI.DrawTexture(new Rect(0, S(42), w, S(90)), previewFade);
                float actionSize = C(32), deleteLeft = w - S(12) - actionSize;
                float playLeft = deleteLeft - S(8) - actionSize, actionTop = S(140) - actionSize / 2;
                Rect titleRect = new Rect(S(12), S(121), Mathf.Max(S(1), playLeft - S(24)), S(24));
                Label(titleRect, Ellipsis(Path.GetFileNameWithoutExtension(save.FileName), title, titleRect.width), title, Color.white);
                Tip(titleRect, Path.GetFileNameWithoutExtension(save.FileName) + "\n" + save.CompatibilityTip);
                Image(new Rect(S(20) - C(8), S(152) - C(8), C(16), C(16)), SaveBrowserIcons.House, ColonyColor(data?.ColonyId ?? Unknown));
                string colonyName = data?.ColonyName ?? "Unknown colony";
                Rect colonyRect = new Rect(S(32), S(142), Mathf.Max(S(1), playLeft - S(40)), S(22));
                Label(colonyRect, Ellipsis(colonyName, meta, colonyRect.width), meta, SecondaryInk);
                Tip(colonyRect, colonyName);
                if (IconButton(new Rect(playLeft, actionTop, actionSize, actionSize), Icon.Play, "Load game", green: true)) LoadSelected(save);
                if (IconButton(new Rect(deleteLeft, actionTop, actionSize, actionSize), Icon.Delete, "Delete save")) ConfirmDelete(save);
                float second = S(122), firstWidth = S(100);
                Stat(new Rect(S(12), S(174), firstWidth, S(43)), "Play time", data?.PlayTime);
                Stat(new Rect(second, S(174), w - second - S(10), S(43)), "Colonists", data?.Preview?.Colonists?.ToString(CultureInfo.InvariantCulture));
                string storyteller = data?.Storyteller;
                if (!string.IsNullOrEmpty(storyteller)) storyteller = DefDatabase<StorytellerDef>.GetNamedSilentFail(storyteller)?.LabelCap.ToString() ?? storyteller;
                Stat(new Rect(S(12), S(222), firstWidth, S(60)), "Storyteller", storyteller);
                Stat(new Rect(second, S(222), w - second - S(10), S(50)), "Colony wealth", data?.Preview?.Wealth?.ToString("0", CultureInfo.InvariantCulture));
                if (data?.Error != null) Tip(preview, data.Error);
            }
            finally { GUI.EndGroup(); }
        }
        private void PreparePreview(SavePreviewRecord record)
        {
            byte[] png = record?.PreviewPng;
            if (ReferenceEquals(png, decodedPreview)) return;
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null; decodedPreview = png;
            if (png == null || png.Length == 0) return;
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (ImageConversion.LoadImage(texture, png, true)) previewTexture = texture;
                else UnityEngine.Object.Destroy(texture);
            }
            catch (Exception ex) { Log.Warning("[ReframedUI] Preview unavailable: " + ex.Message); }
        }
        private void Stat(Rect rect, string label, string value)
        {
            Label(new Rect(rect.x, rect.y, rect.width, S(18)), label, meta, SecondaryInk);
            Label(new Rect(rect.x, rect.y + S(21), rect.width, rect.height - S(21)), value ?? "Unavailable", rect.height > S(45) ? wrapped : body, ListInk);
            Tip(rect, label + ": " + (value ?? "Unavailable for this save"));
        }
        private void Flat(Rect rect, Color color, bool border = false)
        {
            if (border) { Theme.RoundRect(rect, Edge, S(4)); Theme.RoundRect(rect.ContractedBy(S(.65f)), color, S(3.4f)); }
            else Theme.RoundRect(rect, color, S(3));
        }
        private void TabFace(Rect rect, Color color)
        {
            // Open bottom: the shared header divider is the only lower edge.
            Theme.ButtonFace(rect, color, radius: C(4), openBottom: true, rim: Edge);
        }
        private static string Ellipsis(string value, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(value) || style.CalcSize(new GUIContent(value)).x <= width) return value;
            const string end = "…";
            int[] boundaries = StringInfo.ParseCombiningCharacters(value);
            int low = 0, high = boundaries.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                int length = mid == boundaries.Length ? value.Length : boundaries[mid];
                if (style.CalcSize(new GUIContent(value.Substring(0, length) + end)).x <= width) low = mid;
                else high = mid - 1;
            }
            return value.Substring(0, low == boundaries.Length ? value.Length : boundaries[low]) + end;
        }
        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color); texture.Apply(false, true); return texture;
        }
        private static GUIStyle ScrollStyle(Color color)
        {
            var style = new GUIStyle { border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset() };
            var texture = Solid(color);
            style.normal.background = style.hover.background = style.active.background = style.focused.background = texture;
            return style;
        }
        private bool IconButton(Rect rect, Icon icon, string tip, bool active = false, bool green = false)
        {
            bool hover = contentInteractive && GUI.enabled && Mouse.IsOver(rect);
            bool arrow = icon == Icon.Left || icon == Icon.Right;
            bool header = icon == Icon.Back || icon == Icon.Search || arrow;
            Color face = green ? Theme.Rgb(hover ? 0x176B38u : 0x0E542Au) :
                header ? (active || hover ? Theme.Rgb(0x212932) : Header) : Theme.Rgb(hover ? 0x303B47u : 0x212932u);
            if (arrow) { if (hover) Flat(rect, face); }
            else if (header) TabFace(rect, face); else Theme.ButtonFace(rect, face, !GUI.enabled, radius: C(3));
            Texture2D texture = icon == Icon.Back ? SaveBrowserIcons.Back : icon == Icon.Search ? SaveBrowserIcons.Search :
                icon == Icon.Left ? SaveBrowserIcons.Left : icon == Icon.Right ? SaveBrowserIcons.Right :
                icon == Icon.Play ? SaveBrowserIcons.Play : SaveBrowserIcons.Delete;
            float size = C(arrow || icon == Icon.Back ? 16 : header ? 18 : 20);
            Image(new Rect(rect.center.x - size / 2, rect.center.y - size / 2, size, size), texture,
                header ? SecondaryInk : GUI.enabled ? Color.white : SecondaryInk);
            Tip(rect, tip);
            return Widgets.ButtonInvisible(rect);
        }
        private void Image(Rect rect, Texture2D texture, Color color)
        {
            if (texture == null) return;
            using (new Theme.GuiScope(true)) { GUI.color = ContentColor(color); GUI.DrawTexture(rect, texture); }
        }
        // These are surface fades, not icon geometry. Icons are immutable PNG assets.
        private static Texture2D Fade(Color color, bool horizontal, float opacity)
        {
            const int n = 128;
            var texture = new Texture2D(horizontal ? n : 1, horizontal ? 1 : n, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[n];
            for (int i = 0; i < n; i++) { Color c = color; c.a = opacity * (horizontal ? i / (float)(n - 1) : 1 - i / (float)(n - 1)); pixels[i] = c; }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }
        private void Tip(Rect rect, string value)
        { if (contentInteractive && !string.IsNullOrEmpty(value) && Mouse.IsOver(rect)) hoveredTip = value; }
        private void DrawTooltip(Rect bounds)
        {
            if (hoveredTip != previousTip) { previousTip = hoveredTip; tipSince = Time.realtimeSinceStartup; }
            if (hoveredTip == null || Time.realtimeSinceStartup - tipSince < .65f) return;
            string text = hoveredTip.Length <= 320 ? hoveredTip : hoveredTip.Substring(0, 317) + "…";
            float padding = C(8), width = Mathf.Min(S(260), bounds.width - S(24));
            float height = tooltipStyle.CalcHeight(new GUIContent(text), width - padding * 2) + padding * 2;
            Vector2 mouse = Event.current.mousePosition;
            var rect = new Rect(Mathf.Clamp(mouse.x + S(12), S(8), bounds.width - width - S(8)),
                Mathf.Clamp(mouse.y + S(18), S(8), bounds.height - height - S(8)), width, height);
            MenuTooltipStyle.Draw(rect, text, tooltipStyle, padding, ControlScale);
        }
        private static Color PanelColor(Color color)
        {
            // User-directed departure from the flat concept backing; content remains opaque.
            color.a = Theme.EntryPanelOpacity;
            return color;
        }
        private Color ColonyColor(string id)
        {
            if (id == Unknown) return Theme.Rgb(0xA3AFB8);
            if (colonyColors.TryGetValue(id, out Color color)) return color;
            uint[] colors = { 0xF8B464, 0x619DE2, 0xA6CF56, 0xBD83B5, 0x62B3AA };
            color = Theme.Rgb(colors[colonyColors.Count % colors.Length]);
            colonyColors.Add(id, color);
            return color;
        }
        private static string Age(DateTime written)
        {
            TimeSpan age = DateTime.Now - written;
            if (age.TotalMinutes < 1) return "Just now";
            if (age.TotalHours < 1) return (int)age.TotalMinutes + " min ago";
            if (age.TotalDays < 1) { int hours = (int)age.TotalHours; return hours + (hours == 1 ? " hr ago" : " hrs ago"); }
            if (age.TotalDays < 7) { int days = (int)age.TotalDays; return days + (days == 1 ? " day ago" : " days ago"); }
            if (age.TotalDays < 60) { int weeks = (int)(age.TotalDays / 7); return weeks + (weeks == 1 ? " week ago" : " weeks ago"); }
            return written.ToString("d");
        }

        private HeaderDetails Data(SaveFileInfo save, FileStamp? expected = null)
        {
            if (!metadata.TryGetValue(save.FileInfo.FullName, out MetadataResult result)) return null;
            if (expected.HasValue && !expected.Value.Matches(result.Stamp)) return null;
            return result.Details;
        }
        private void RebuildVisible()
        {
            visible.Clear();
            foreach (var save in files)
            {
                string id = Data(save)?.ColonyId ?? Unknown;
                if (colony != All && colony != id) continue;
                if (save.FileName.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                visible.Add(save);
            }
            if (Selected() == null) selectedPath = null;
        }
        private SaveFileInfo Selected() => visible.Find(save => SamePath(selectedPath, save.FileInfo.FullName));
        private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private void Select(SaveFileInfo save)
        {
            try
            {
                FileStamp stamp = FileStamp.Read(save.FileInfo.FullName);
                selectedPath = stamp.Path; selectedStamp = stamp; notice = null;
                if ((metadata.TryGetValue(stamp.Path, out MetadataResult old) && !stamp.Matches(old.Stamp)) ||
                    (old == null && scanTask?.IsCompleted == true)) BeginMetadataScan(stamp.Path);
            }
            catch (Exception ex) { FileUnavailable(ex); }
        }
        private void CancelScan()
        {
            if (scanCancel == null) return;
            try { scanCancel.Cancel(); } catch (ObjectDisposedException) { }
            scanCancel = null;
        }
        private void BeginMetadataScan(string first = null)
        {
            CancelScan(); int current = ++generation;
            var cancellation = new CancellationTokenSource(); scanCancel = cancellation;
            CancellationToken token = cancellation.Token;
            var paths = files.Select(file => file.FileInfo.FullName).ToList();
            if (first != null) { paths.RemoveAll(path => SamePath(path, first)); paths.Insert(0, first); }
            var playerDefs = new HashSet<string>(DefDatabase<FactionDef>.AllDefs.Where(def => def.isPlayer).Select(def => def.defName));
            scanTask = Task.Run(async () =>
            {
                bool entered = false;
                try
                {
                    await metadataGate.WaitAsync(token).ConfigureAwait(false); entered = true;
                    foreach (string path in paths)
                    {
                        token.ThrowIfCancellationRequested();
                        var result = new MetadataResult { Generation = current, Path = path };
                        try { result.Stamp = FileStamp.Read(path); result.Details = HeaderDetails.Read(result.Stamp, playerDefs, token); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception) { result.Details = new HeaderDetails { Error = "Save details unavailable." }; }
                        token.ThrowIfCancellationRequested(); completed.Enqueue(result);
                    }
                }
                catch (OperationCanceledException) { }
                finally { if (entered) metadataGate.Release(); cancellation.Dispose(); }
            });
        }
        private void PumpMetadata()
        {
            bool changed = false; int handled = 0;
            while (handled++ < 32 && completed.TryDequeue(out MetadataResult result))
            {
                if (result.Generation != generation) continue;
                metadata[result.Path] = result; changed = true;
            }
            if (changed)
            {
                // A provisional Unknown category may disappear as the background scan resolves identities.
                // Keep every save reachable instead of leaving a selected tab that no longer exists.
                if (colony != All && !files.Any(save => (Data(save)?.ColonyId ?? Unknown) == colony)) colony = All;
                RebuildVisible();
            }
        }
        private void Refresh()
        {
            string previous = selectedPath; ReloadFiles(); metadata.Clear(); selectedPath = null;
            RebuildVisible(); var retained = visible.Find(save => SamePath(previous, save.FileInfo.FullName));
            if (retained != null) Select(retained);
            BeginMetadataScan(previous); notice = listError;
        }
        private bool Validate(FileStamp expected)
        {
            try
            {
                string nativePath = Path.GetFullPath(GenFilePaths.FilePathForSavedGame(Path.GetFileNameWithoutExtension(expected.Path)));
                if (!SamePath(nativePath, expected.Path) || !expected.Matches(FileStamp.Read(expected.Path)))
                { Refresh(); notice = "This save changed. Select it again before continuing."; selectedPath = null; return false; }
                return true;
            }
            catch (Exception ex) { FileUnavailable(ex); return false; }
        }
        private void LoadSelected(SaveFileInfo save)
        {
            if (Validate(selectedStamp)) DoFileInteraction(Path.GetFileNameWithoutExtension(save.FileName));
        }
        private void ConfirmDelete(SaveFileInfo save)
        {
            FileStamp target = selectedStamp;
            if (!Validate(target)) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmDelete".Translate(save.FileName), () =>
            {
                if (!Validate(target)) return;
                try { new FileInfo(target.Path).Delete(); selectedPath = null; Refresh(); notice = "Save deleted."; }
                catch (Exception ex) { FileUnavailable(ex); }
            }, destructive: true));
        }
        private void FileUnavailable(Exception ex)
        {
            selectedPath = null; notice = "This save could not be accessed. Reopen the browser to refresh.";
            Log.Warning("[ReframedUI] Save file unavailable: " + ex.Message);
        }
        private readonly struct FileStamp
        {
            internal readonly string Path;
            internal readonly long Length, Written;
            private FileStamp(FileInfo file) { Path = file.FullName; Length = file.Length; Written = file.LastWriteTimeUtc.Ticks; }
            internal static FileStamp Read(string path)
            {
                var file = new FileInfo(path); file.Refresh();
                if (!file.Exists) throw new FileNotFoundException("Save no longer exists.", path);
                return new FileStamp(file);
            }
            internal bool Matches(FileStamp other) => SamePath(Path, other.Path) && Length == other.Length && Written == other.Written;
        }
        private sealed class HeaderDetails
        {
            internal string Scenario, Storyteller, PlayTime, Error, ColonyName, ColonyId, WorldName;
            internal SavePreviewRecord Preview;
            private static string Bounded(string value) => value.Length <= 256 ? value : value.Substring(0, 253) + "…";
            internal static HeaderDetails Read(FileStamp stamp, HashSet<string> playerDefs, CancellationToken token)
            {
                var data = new HeaderDetails(); string seed = null, worldId = null, factionDef = null, factionName = null;
                try
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = 4194304, IgnoreComments = true, IgnoreWhitespace = true };
                    var elapsed = Stopwatch.StartNew(); var path = new List<string>();
                    using (var stream = new FileStream(stamp.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (XmlReader reader = XmlReader.Create(stream, settings))
                    {
                        while (reader.Read())
                        {
                            token.ThrowIfCancellationRequested();
                            if (elapsed.ElapsedMilliseconds > 500) throw new TimeoutException();
                            if (reader.NodeType == XmlNodeType.EndElement)
                            {
                                if (reader.Depth == 5 && path.Count > 5 && path[3] == "factionManager" && path[4] == "allFactions" && playerDefs.Contains(factionDef ?? ""))
                                { data.ColonyName = string.IsNullOrWhiteSpace(factionName) ? null : Bounded(factionName); break; }
                                continue;
                            }
                            if (reader.NodeType != XmlNodeType.Element) continue;
                            while (path.Count > reader.Depth) path.RemoveAt(path.Count - 1);
                            path.Add(reader.Name);
                            if (path.Count < 2 || path[0] != "savegame" || path[1] != "game") continue;
                            if (reader.Depth == 2 && reader.Name == "maps") break;
                            if (reader.Depth == 3 && path[2] == "world" && reader.Name == "ideoManager") break;
                            if (reader.Depth == 3)
                            {
                                if (path[2] == "info" && reader.Name == "realPlayTimeInteracting")
                                {
                                    if (double.TryParse(reader.ReadString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
                                        !double.IsInfinity(seconds) && !double.IsNaN(seconds) && seconds >= 0 && seconds <= 3155760000d)
                                    { var time = TimeSpan.FromSeconds(seconds); data.PlayTime = (long)time.TotalDays + "d " + time.Hours + "h " + time.Minutes + "m"; }
                                }
                                else if (path[2] == "scenario" && reader.Name == "name") data.Scenario = Bounded(reader.ReadString());
                                else if (path[2] == "storyteller" && reader.Name == "def") data.Storyteller = Bounded(reader.ReadString());
                            }
                            else if (reader.Depth == 4 && path[2] == "world" && path[3] == "info")
                            {
                                if (reader.Name == "name") data.WorldName = Bounded(reader.ReadString());
                                else if (reader.Name == "seedString") seed = reader.ReadString();
                                else if (reader.Name == "persistentRandomValue")
                                { string value = reader.ReadString(); if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) worldId = value; }
                            }
                            else if (reader.Depth == 5 && path[3] == "factionManager" && path[4] == "allFactions" && reader.Name == "li")
                            { factionDef = factionName = null; }
                            else if (reader.Depth == 6 && path[3] == "factionManager" && path[4] == "allFactions")
                            {
                                if (reader.Name == "def") factionDef = reader.ReadString();
                                else if (reader.Name == "name") factionName = reader.ReadString();
                            }
                        }
                    }

                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { data.Error = "Some details are unavailable."; }
                try { if (!stamp.Matches(FileStamp.Read(stamp.Path))) return new HeaderDetails { Error = "Save changed; reopen to refresh details." }; }
                catch (Exception) { return new HeaderDetails { Error = "Save details unavailable." }; }
                if (worldId != null && seed != null)
                    using (var hash = SHA256.Create()) data.ColonyId = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(worldId + "|" + seed))).Replace("-", "").ToLowerInvariant();
                token.ThrowIfCancellationRequested();
                data.Preview = SavePreviewStore.Read(stamp.Path);
                token.ThrowIfCancellationRequested();
                return data;
            }
        }
        private sealed class MetadataResult { internal int Generation; internal string Path; internal FileStamp Stamp; internal HeaderDetails Details; }
        private sealed class Tab { internal string Id, Name, World; internal float Width; }
        private enum Icon { Back, Search, Play, Delete, Left, Right }
    }
}
