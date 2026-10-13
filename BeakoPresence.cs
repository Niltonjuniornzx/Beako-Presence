using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Web.Script.Serialization;

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;


[assembly: System.Reflection.AssemblyTitle("Beako Presence")]
[assembly: System.Reflection.AssemblyProduct("Beako Presence")]
[assembly: System.Reflection.AssemblyDescription("Gerenciador de atividades para Discord")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

static class Skin {
    public static readonly Color Background = Color.FromArgb(14, 15, 18);
    public static readonly Color Surface = Color.FromArgb(22, 24, 29);
    public static readonly Color SurfaceHover = Color.FromArgb(32, 35, 42);
    public static readonly Color Card = Color.FromArgb(24, 26, 32);
    public static readonly Color CardHover = Color.FromArgb(31, 34, 42);
    public static readonly Color CardSelected = Color.FromArgb(21, 40, 33);
    public static readonly Color Green = Color.FromArgb(46, 190, 125);
    public static readonly Color GreenHover = Color.FromArgb(62, 210, 142);
    public static readonly Color Danger = Color.FromArgb(240, 98, 110);
    public static readonly Color Muted = Color.FromArgb(138, 146, 158);
    public static readonly Color Border = Color.FromArgb(42, 46, 55);
    public static readonly Color Text = Color.FromArgb(238, 241, 245);
    static readonly Color[] Tints = { Color.FromArgb(38, 64, 56), Color.FromArgb(48, 50, 82), Color.FromArgb(76, 56, 48), Color.FromArgb(40, 62, 80), Color.FromArgb(74, 46, 66) };
    public static Color Tint(string name) { int sum = 0; foreach (char c in name ?? "") sum += c; return Tints[sum % Tints.Length]; }
    public static void DrawRounded(Graphics g, Image image, Rectangle r, int radius) {
        using (GraphicsPath path = Round(r, radius)) {
            var state = g.Save(); g.SetClip(path, CombineMode.Intersect); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(image, r); g.Restore(state);
        }
    }
    // Preenche o retangulo cortando a imagem (como "cover" do CSS).
    public static void DrawCover(Graphics g, Image image, Rectangle r) {
        float scale = Math.Max((float)r.Width / image.Width, (float)r.Height / image.Height);
        float sw = r.Width / scale, sh = r.Height / scale;
        RectangleF src = new RectangleF((image.Width - sw) / 2f, (image.Height - sh) / 2f, sw, sh);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(image, r, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);
    }
    public static GraphicsPath Round(Rectangle bounds, int radius) {
        GraphicsPath path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;
        int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (d <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    public static void Clip(Control control, int radius) {
        Region old = control.Region;
        using (GraphicsPath path = Round(new Rectangle(0, 0, control.Width, control.Height), radius))
            control.Region = new Region(path);
        if (old != null) old.Dispose();
    }
}
class FlatPanel : Panel {
    public int Radius = 12; public Color BorderColor = Skin.Border;
    public FlatPanel() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaintBackground(PaintEventArgs e) { using (SolidBrush b = new SolidBrush(Parent != null ? Parent.BackColor : Skin.Background)) e.Graphics.FillRectangle(b, ClientRectangle); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = Skin.Round(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
        using (SolidBrush fill = new SolidBrush(BackColor))
        using (Pen p = new Pen(BorderColor)) { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(p, path); }
    }
}
class HeaderBar : Panel {
    Image avatar;
    public HeaderBar() {
        DoubleBuffered = true; BackColor = Skin.Background; Padding = new Padding(0, 0, 0, 1);
        try {
            Image src = null;
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BeakoPresence-Avatar.png");
            if (File.Exists(file)) src = Image.FromFile(file); else using (Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath)) src = ico.ToBitmap();
            Bitmap small = new Bitmap(72, 72);
            using (Graphics g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.AntiAlias; g.DrawImage(src, new Rectangle(0, 0, 72, 72)); }
            src.Dispose(); avatar = small;
        } catch { }
    }
    protected override void Dispose(bool disposing) { if (disposing && avatar != null) avatar.Dispose(); base.Dispose(disposing); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle badge = new Rectangle(20, 7, 40, 40);
        if (avatar != null) {
            var state = g.Save();
            using (GraphicsPath circle = new GraphicsPath()) { circle.AddEllipse(badge); g.SetClip(circle, CombineMode.Intersect); }
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(avatar, badge); g.Restore(state);
            using (Pen ring = new Pen(Color.FromArgb(120, Skin.Green), 1.6f)) g.DrawEllipse(ring, badge);
        }
        using (Font title = new Font("Segoe UI Semibold", 13f), small = new Font("Segoe UI", 8.5f)) {
            TextRenderer.DrawText(g, "Beako Presence", title, new Point(70, 6), Skin.Text);
            TextRenderer.DrawText(g, "Atividades para o Discord", small, new Point(71, 29), Skin.Muted);
        }
        using (Pen line = new Pen(Skin.Border)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);
    }
}
class DarkButton : Button {
    bool hover;
    public int Radius = 0;
    public bool OutlineOnly; public Color HoverBorder = Skin.Green;
    public DarkButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if (Region != null) { Region old = Region; Region = null; old.Dispose(); } }
    protected override void OnPaintBackground(PaintEventArgs e) { using (SolidBrush b = new SolidBrush(Parent != null ? Parent.BackColor : Skin.Background)) e.Graphics.FillRectangle(b, ClientRectangle); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        using (SolidBrush backdrop = new SolidBrush(Parent != null ? Parent.BackColor : Skin.Background)) e.Graphics.FillRectangle(backdrop, ClientRectangle);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
        using (GraphicsPath path = Skin.Round(r, Radius)) {
            if (OutlineOnly) {
                Color border = hover && Enabled ? HoverBorder : (Enabled ? Color.FromArgb(70, 78, 90) : Color.FromArgb(40, 44, 52));
                { if (hover && Enabled) using (SolidBrush tint = new SolidBrush(Color.FromArgb(24, HoverBorder))) e.Graphics.FillPath(tint, path); using (Pen pen = new Pen(border, 1.5f)) e.Graphics.DrawPath(pen, path); }
            } else {
                Color color = !Enabled ? Color.FromArgb(28, 30, 34) : hover ? (FlatAppearance.MouseOverBackColor.IsEmpty ? Skin.SurfaceHover : FlatAppearance.MouseOverBackColor) : BackColor;
                using (SolidBrush brush = new SolidBrush(color)) e.Graphics.FillPath(brush, path);
            }
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.FromArgb(90, 98, 108), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
class DarkWindow : Form {
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr w, IntPtr l);
    public DarkWindow() { FormBorderStyle = FormBorderStyle.None; BackColor = Skin.Background; ForeColor = Color.FromArgb(232, 239, 246); DoubleBuffered = true; }
    public void EnableDrag(Control c) { c.MouseDown += delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left && WindowState == FormWindowState.Normal) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } }; }
    public void AddChrome(string title, bool maximize) {
        Panel bar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Skin.Background, Padding = new Padding(16, 0, 8, 0) };
        Label caption = new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(120, 136, 153), Font = new Font("Segoe UI", 9) };
        MouseEventHandler drag = delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
        bar.MouseDown += drag; caption.MouseDown += drag;
        EventHandler toggle = delegate { if (WindowState != FormWindowState.Maximized) MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
        if (maximize) caption.DoubleClick += toggle;
        bar.Controls.Add(caption);
        FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = maximize ? 126 : 84, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        DarkButton min = ChromeButton("—"); min.Click += delegate { WindowState = FormWindowState.Minimized; }; actions.Controls.Add(min);
        if (maximize) { DarkButton max = ChromeButton("□"); max.Click += toggle; actions.Controls.Add(max); }
        DarkButton close = ChromeButton("×"); close.Click += delegate { Close(); }; close.ForeColor = Color.FromArgb(255, 139, 151); actions.Controls.Add(close);
        bar.Controls.Add(actions); actions.BringToFront();
        Controls.Add(bar); bar.SendToBack();
        /* Bordas nativas redimensionaveis */
    }
    DarkButton ChromeButton(string text) { return new DarkButton { Text = text, Width = 40, Height = 34, Margin = new Padding(1, 1, 1, 0), FlatStyle = FlatStyle.Flat, BackColor = Skin.Background, ForeColor = Color.FromArgb(176, 187, 204), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 12), FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(39, 46, 59) } }; }
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if (m.Msg != 0x84 || WindowState != FormWindowState.Normal) return;
        Point p = PointToClient(new Point((short)(m.LParam.ToInt64() & 0xffff), (short)((m.LParam.ToInt64() >> 16) & 0xffff)));
        int edge = 8; bool left = p.X < edge, right = p.X >= Width - edge, top = p.Y < edge, bottom = p.Y >= Height - edge;
        int hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
        if (hit != 0) m.Result = new IntPtr(hit);
    }
}
class GamePicker : Control {
    public readonly List<Game> Items = new List<Game>();
    readonly Dictionary<string, Game> chosen = new Dictionary<string, Game>();
    public List<Game> SelectedItems { get { return chosen.Values.ToList(); } }
    public Func<Game, bool> IsActive;
    public Func<Game, string> Subtitle;
    readonly Dictionary<string, Image> thumbnails = new Dictionary<string, Image>();
    readonly HashSet<string> iconRequests = new HashSet<string>();
    static readonly System.Threading.SemaphoreSlim coverSlots = new System.Threading.SemaphoreSlim(3);
    readonly JavaScriptSerializer coverJson = new JavaScriptSerializer();
    public bool Compact;
    readonly Font nameFont = new Font("Segoe UI Semibold", 10f), smallFont = new Font("Segoe UI", 8.5f), initialsFont = new Font("Segoe UI Semibold", 20f), pillFont = new Font("Segoe UI Semibold", 7.5f);
    public bool Cards;
    public event EventHandler SelectedIndexChanged;
    int offset, hover = -1, keyboardIndex, dragY, dragOffset; bool dragging, batching;
    int IndexAt(Point p) {
        int index = Cards ? ((p.Y + offset) / RowHeight) * CardColumns + Math.Min(CardColumns - 1, p.X / Math.Max(1, Width / CardColumns)) : (p.Y + offset) / RowHeight;
        return index >= 0 && index < Items.Count ? index : -1;
    }
    void Scroll(int index) {
        int row = Cards ? index / CardColumns : index, top = row * RowHeight;
        if (top < offset) offset = top; else if (top + RowHeight > offset + Height) offset = top + RowHeight - Height;
        offset = Math.Max(0, Math.Min(MaxOffset, offset));
    }
    int RowHeight { get { return Cards ? 176 : 70; } }
    int CardColumns { get { return Math.Max(1, Width / 165); } }
    int TotalRows { get { return Cards ? (Items.Count + CardColumns - 1) / CardColumns : Items.Count; } }
    int MaxOffset { get { return Math.Max(0, TotalRows * RowHeight - Height); } }
    public GamePicker() { SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); TabStop = true; Cursor = Cursors.Hand; BackColor = Skin.Background; }
    public void BeginUpdate() { batching = true; }
    public void EndUpdate() { batching = false; offset = Math.Min(offset, MaxOffset); Notify(); }
    public void Select(Game g) { chosen[g.Id] = g; Notify(); }
    public void ClearSelection() { chosen.Clear(); Notify(); }
    public void Unselect(string id) { if (chosen.Remove(id)) Notify(); }
    public void SetSelected(int index, bool value) { if (index < 0 || index >= Items.Count) return; Game g = Items[index]; if (value) chosen[g.Id] = g; else chosen.Remove(g.Id); Notify(); }
    void Notify() { if (batching) return; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
    void Toggle(int index) { if (index < 0 || index >= Items.Count) return; SetSelected(index, !chosen.ContainsKey(Items[index].Id)); }
    protected override void OnMouseWheel(MouseEventArgs e) { offset = Math.Max(0, Math.Min(MaxOffset, offset - (e.Delta / 120) * RowHeight * 3)); Invalidate(); base.OnMouseWheel(e); }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e); Focus(); if (e.Button != MouseButtons.Left) return;
        if (e.X >= Width - 14 && MaxOffset > 0) { dragging = true; Capture = true; dragY = e.Y; dragOffset = offset; return; }
        int hit = IndexAt(e.Location); if (hit < 0) return; keyboardIndex = hit; Toggle(hit);
    }
    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; base.OnMouseUp(e); }
    protected override void OnMouseMove(MouseEventArgs e) {
        if (dragging) { offset = Math.Max(0, Math.Min(MaxOffset, dragOffset + (int)((e.Y - dragY) * (double)(TotalRows * RowHeight) / Math.Max(1, Height)))); Invalidate(); return; }
        int index = IndexAt(e.Location); if (index != hover) { hover = index; Invalidate(); } base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.PageUp || keyData == Keys.PageDown || keyData == Keys.Home || keyData == Keys.End || keyData == Keys.Space || keyData == Keys.Enter || base.IsInputKey(keyData); }
    protected override void OnKeyDown(KeyEventArgs e) {
        if (Items.Count == 0) { base.OnKeyDown(e); return; }
        int step = Cards ? CardColumns : 1, page = Math.Max(1, Height / RowHeight) * step, next = keyboardIndex;
        switch (e.KeyCode) {
            case Keys.Space: case Keys.Enter: Toggle(keyboardIndex); e.Handled = true; return;
            case Keys.Down: next += step; break;
            case Keys.Up: next -= step; break;
            case Keys.Right: if (Cards) next += 1; break;
            case Keys.Left: if (Cards) next -= 1; break;
            case Keys.PageDown: next += page; break;
            case Keys.PageUp: next -= page; break;
            case Keys.Home: next = 0; break;
            case Keys.End: next = Items.Count - 1; break;
            default: base.OnKeyDown(e); return;
        }
        keyboardIndex = Math.Max(0, Math.Min(Items.Count - 1, next)); Scroll(keyboardIndex); Invalidate(); e.Handled = true;
    }
    void PaintScrollbar(Graphics g) {
        if (MaxOffset <= 0) return;
        int content = TotalRows * RowHeight, thumb = Math.Max(26, (int)((double)Height * Height / content)), y = (int)((double)offset / MaxOffset * (Height - thumb));
        using (SolidBrush brush = new SolidBrush(Color.FromArgb(dragging ? 130 : 80, 140, 150, 158))) g.FillRectangle(brush, Width - 7, y, 5, thumb);
    }
    string CoverFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence", "covers"); } }
    Image Thumbnail(Game game) {
        Image image;
        if (thumbnails.TryGetValue(game.Id, out image)) return image;
        string custom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "covers", game.Id + ".jpg");
        string cached = Path.Combine(CoverFolder, game.Id + ".jpg");
        foreach (string path in new [] { custom, cached }) {
            if (File.Exists(path)) {
                try { using (Image source = Image.FromFile(path)) { image = new Bitmap(source); thumbnails[game.Id] = image; return image; } } catch { }
            }
        }
        string icon = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence", "icons", game.Id + ".png");
        if (File.Exists(icon)) {
            try { using (Image source = Image.FromFile(icon)) image = new Bitmap(source); } catch { }
        }
        thumbnails[game.Id] = image;
        if (iconRequests.Add(game.Id)) FetchCover(game);
        return image;
    }
    async void FetchCover(Game game) {
        await coverSlots.WaitAsync();
        try {
            if (IsDisposed) return;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string url = null;
            // Steam: pesquisa pelo nome e valida a correspondencia antes de usar a arte.
            using (WebClient search = new WebClient()) {
                search.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 BeakoPresence";
                string endpoint = "https://store.steampowered.com/api/storesearch/?term=" + Uri.EscapeDataString(game.Name) + "&l=english&cc=us";
                Task<string> request = search.DownloadStringTaskAsync(endpoint);
                if (await Task.WhenAny(request, Task.Delay(7000)) == request) {
                    var result = coverJson.DeserializeObject(await request) as Dictionary<string, object>;
                    object raw;
                    if (result != null && result.TryGetValue("items", out raw)) {
                        var entries = raw as object[];
                        if (entries != null) foreach (object item in entries.Take(6)) {
                            var entry = item as Dictionary<string, object>;
                            if (entry == null || !entry.ContainsKey("name") || !entry.ContainsKey("id")) continue;
                            string actual = Convert.ToString(entry["name"]);
                            Func<string,string> normalize = v => new string(v.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                            if (normalize(actual) != normalize(game.Name)) continue;
                            int appId;
                            if (int.TryParse(Convert.ToString(entry["id"]), out appId) && appId > 0)
                                url = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/" + appId + "/header.jpg";
                            break;
                        }
                    }
                } else search.CancelAsync();
            }
            if (url == null && !String.IsNullOrEmpty(game.IconHash) && game.Id.All(char.IsDigit) &&
                game.IconHash.All(c => char.IsLetterOrDigit(c) || c == '_'))
                url = "https://cdn.discordapp.com/app-icons/" + game.Id + "/" + game.IconHash + ".png?size=256";
            if (url == null) return;
            using (WebClient client = new WebClient()) {
                Task<byte[]> request = client.DownloadDataTaskAsync(url);
                if (await Task.WhenAny(request, Task.Delay(10000)) != request) { client.CancelAsync(); return; }
                byte[] bytes = await request;
                if (bytes.Length == 0 || bytes.Length > 3000000 || IsDisposed) return;
                using (MemoryStream stream = new MemoryStream(bytes))
                using (Image source = Image.FromStream(stream)) {
                    Image bitmap = new Bitmap(source);
                    Image old;
                    if (thumbnails.TryGetValue(game.Id, out old) && old != null) old.Dispose();
                    thumbnails[game.Id] = bitmap;
                    Directory.CreateDirectory(CoverFolder);
                    // O cache usa JPG mesmo quando a origem e PNG.
                    bitmap.Save(Path.Combine(CoverFolder, game.Id + ".jpg"), System.Drawing.Imaging.ImageFormat.Jpeg);
                }
                if (!IsDisposed) Invalidate();
            }
        } catch { /* Sem rede ou imagem: exibe iniciais ou icone em cache. */ }
        finally { coverSlots.Release(); }
    }
    void PaintCards(PaintEventArgs e) {
        Graphics g = e.Graphics;
        if (Items.Count == 0) { TextRenderer.DrawText(g, "Nenhum jogo encontrado", Font, ClientRectangle, Skin.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
        int cols = CardColumns, cell = Math.Max(1, (Width - 12) / cols);
        for (int i = 0; i < Items.Count; i++) {
            int x = (i % cols) * cell + 4, y = (i / cols) * RowHeight - offset + 4;
            if (y + RowHeight < 0 || y > Height) continue;
            Game game = Items[i]; bool picked = chosen.ContainsKey(game.Id), live = IsActive != null && IsActive(game);
            Rectangle rect = new Rectangle(x, y, cell - 12, RowHeight - 12);
            Rectangle banner = new Rectangle(rect.X, rect.Y, rect.Width, 84);
            using (GraphicsPath card = Skin.Round(rect, 12)) {
                using (SolidBrush b = new SolidBrush(picked ? Skin.CardSelected : hover == i ? Skin.CardHover : Skin.Card)) g.FillPath(b, card);
                var state = g.Save(); g.SetClip(card, CombineMode.Intersect); g.IntersectClip(banner);
                Image image = Thumbnail(game);
                using (SolidBrush tint = new SolidBrush(Skin.Tint(game.Name))) g.FillRectangle(tint, banner);
                if (image != null && (float)image.Width / image.Height > 1.5f) Skin.DrawCover(g, image, banner);
                else if (image != null) { int s = 60; Skin.DrawRounded(g, image, new Rectangle(banner.X + (banner.Width - s) / 2, banner.Y + (banner.Height - s) / 2, s, s), 14); }
                else {
                    string initials = new string(game.Name.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
                    TextRenderer.DrawText(g, initials, initialsFont, banner, Color.FromArgb(225, 255, 255, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                using (LinearGradientBrush fade = new LinearGradientBrush(new Rectangle(banner.X, banner.Bottom - 26, banner.Width, 27), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(110, 0, 0, 0), 90f)) g.FillRectangle(fade, banner.X, banner.Bottom - 26, banner.Width, 27);
                g.Restore(state);
                Color borderColor = picked ? Skin.Green : hover == i ? Color.FromArgb(78, 86, 100) : Skin.Border;
                using (Pen pen = new Pen(borderColor, picked ? 2f : 1f)) g.DrawPath(pen, card);
            }
            if (picked) {
                Rectangle badge = new Rectangle(rect.Right - 30, rect.Y + 8, 22, 22);
                using (SolidBrush b = new SolidBrush(Skin.Green)) g.FillEllipse(b, badge);
                using (Pen tick = new Pen(Color.White, 2f)) g.DrawLines(tick, new [] { new Point(badge.X + 6, badge.Y + 11), new Point(badge.X + 10, badge.Y + 15), new Point(badge.X + 16, badge.Y + 7) });
            }
            if (live) {
                Rectangle pill = new Rectangle(rect.X + 8, rect.Y + 8, 52, 18);
                using (GraphicsPath pp = Skin.Round(pill, 9)) using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 22, 120, 78))) g.FillPath(b, pp);
                TextRenderer.DrawText(g, "● ATIVO", pillFont, pill, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            TextRenderer.DrawText(g, game.Name, nameFont, new Rectangle(rect.X + 12, rect.Y + 91, rect.Width - 24, 22), Skin.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, game.Exe, smallFont, new Rectangle(rect.X + 12, rect.Y + 112, rect.Width - 24, 18), Skin.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            string line = live ? "Ativo agora" : picked ? "Selecionado" : "Clique para selecionar";
            TextRenderer.DrawText(g, line, smallFont, new Rectangle(rect.X + 12, rect.Y + 134, rect.Width - 24, 18), live || picked ? Skin.Green : Color.FromArgb(104, 112, 126), TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Focused && ShowFocusCues && i == keyboardIndex) using (Pen pen = new Pen(Color.FromArgb(140, 255, 255, 255)) { DashStyle = DashStyle.Dot }) using (GraphicsPath ring = Skin.Round(new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6), 10)) g.DrawPath(pen, ring);
        }
        PaintScrollbar(g);
    }
    protected override void Dispose(bool disposing) { if (disposing) { foreach (Image image in thumbnails.Values) if (image != null) image.Dispose(); nameFont.Dispose(); smallFont.Dispose(); initialsFont.Dispose(); pillFont.Dispose(); } base.Dispose(disposing); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(BackColor);
        if (Cards) { PaintCards(e); return; }
        if (Items.Count == 0) { TextRenderer.DrawText(e.Graphics, Compact ? "Suas sessões aparecem aqui\nMarque jogos e clique em Iniciar atividade" : "Nenhum jogo encontrado", Font, ClientRectangle, Color.FromArgb(132, 142, 150), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
        int row = RowHeight;
        for (int i = offset / row; i < Items.Count && i * row - offset < Height; i++) {
            Game game = Items[i]; bool selected = chosen.ContainsKey(game.Id); bool live = IsActive != null && IsActive(game);
            Rectangle bounds = new Rectangle(0, i * row - offset + 3, Math.Max(1, Width - 10), row - 6);
            using (GraphicsPath rowPath = Skin.Round(bounds, 10)) {
                using (SolidBrush brush = new SolidBrush(selected ? Skin.CardSelected : hover == i ? Skin.SurfaceHover : Color.FromArgb(30, 33, 40))) e.Graphics.FillPath(brush, rowPath);
                if (selected) using (Pen pen = new Pen(Skin.Green, 1.5f)) e.Graphics.DrawPath(pen, rowPath);
            }
            Rectangle box = new Rectangle(14, bounds.Y + (bounds.Height - 18) / 2, 18, 18);
            using (GraphicsPath boxPath = Skin.Round(box, 5)) {
                if (selected) using (SolidBrush fill = new SolidBrush(Skin.Green)) e.Graphics.FillPath(fill, boxPath);
                using (Pen border = new Pen(selected ? Skin.Green : Color.FromArgb(110, 118, 130), 1.4f)) e.Graphics.DrawPath(border, boxPath);
            }
            if (selected) using (Pen pen = new Pen(Color.White, 2f)) e.Graphics.DrawLines(pen, new [] { new Point(box.X + 4, box.Y + 9), new Point(box.X + 8, box.Y + 13), new Point(box.X + 14, box.Y + 5) });
            Rectangle icon = new Rectangle(42, bounds.Y + (bounds.Height - 44) / 2, 44, 44);
            Image image = Thumbnail(game);
            if (image != null) Skin.DrawRounded(e.Graphics, image, icon, 10);
            else {
                using (GraphicsPath ip = Skin.Round(icon, 10)) using (SolidBrush brush = new SolidBrush(Skin.Tint(game.Name))) e.Graphics.FillPath(brush, ip);
                string initials = new string(game.Name.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
                TextRenderer.DrawText(e.Graphics, initials, nameFont, icon, Color.FromArgb(230, 255, 255, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int x = 98; int width = Math.Max(1, bounds.Width - x - 14);
            TextRenderer.DrawText(e.Graphics, game.Name, nameFont, new Rectangle(x, bounds.Y + 14, width, 22), Skin.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string detail = Subtitle != null ? Subtitle(game) : live ? "Processo ativo" : game.Exe;
            TextRenderer.DrawText(e.Graphics, detail ?? "", smallFont, new Rectangle(x, bounds.Y + 38, width, 20), live || Compact ? Skin.Green : Skin.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues && i == keyboardIndex) using (Pen pen = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dot }) e.Graphics.DrawRectangle(pen, bounds.X + 3, bounds.Y + 3, bounds.Width - 7, bounds.Height - 7);
        }
        PaintScrollbar(e.Graphics);
    }

}

class Game { public string Id, Name, Exe, IconHash; public override string ToString() { return Name; } }
class BeakoPresence : DarkWindow {
    static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence");
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 50000000 };
    List<Game> games = new List<Game>();
    readonly Dictionary<string, Process> running = new Dictionary<string, Process>();
    readonly Dictionary<string, DateTime> started = new Dictionary<string, DateTime>();
    TextBox search = new TextBox(); GamePicker list = new GamePicker(); Label status = new Label();
    Label discordState = new Label(), catalogCount = new Label(); Timer searchDelay = new Timer { Interval = 180 };
    HashSet<string> savedSelection = new HashSet<string>(); bool restoring;
    Button start = new DarkButton(), stop = new DarkButton(), all = new DarkButton(), sync = new DarkButton(), steam = new DarkButton();
    Label session = new Label(), selectedCount = new Label(); GamePicker active = new GamePicker();
    ListBox marked = new ListBox(); Button unmark = new DarkButton(), clearMarked = new DarkButton();
    bool updatingMarked; 
    readonly Dictionary<string, string> runningNames = new Dictionary<string, string>(); Timer timer = new Timer(); bool busy;
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessage")] static extern IntPtr SetCue(IntPtr handle, int message, IntPtr wParam, string text);
    static string FormatSpan(TimeSpan t) { return ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00"); }
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 3 && args[0] == "--process") {
            int parentPid = 0; if (args.Length >= 4) int.TryParse(args[3], out parentPid);
            DateTime childStart = DateTime.Now;
            DarkWindow f = new DarkWindow { Text = args[1], Size = new Size(400, 170), ShowInTaskbar = true, StartPosition = FormStartPosition.CenterScreen, BackColor = Skin.Background, ForeColor = Skin.Text, Font = new Font("Segoe UI", 11), AutoScaleMode = AutoScaleMode.Dpi };
            Label info = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
            Action refreshInfo = delegate { info.Text = "Sessão de atividade: " + args[1] + "\nAtiva há " + FormatSpan(DateTime.Now - childStart) + "\nFeche esta janela para encerrar."; };
            refreshInfo(); f.Controls.Add(info);
            // Encerra sozinho se o app principal for fechado ou travar (evita processos órfãos).
            Timer watchdog = new Timer { Interval = 1000 };
            watchdog.Tick += delegate {
                refreshInfo();
                if (parentPid <= 0) return;
                try { using (Process parent = Process.GetProcessById(parentPid)) { if (parent.HasExited) f.Close(); } }
                catch (ArgumentException) { f.Close(); } catch { }
            };
            watchdog.Start();
            // Icone individual da atividade: barra de tarefas e bandeja do Windows.
            string gameId = args.Length >= 3 ? args[2] : "";
            Icon sessionIcon = null;
            NotifyIcon tray = null;
            try {
                if (gameId.Length > 0 && gameId.All(char.IsDigit)) {
                    string covers = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence", "covers");
                    string icons = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence", "icons");
                    foreach (string file in new [] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", gameId + ".png"), Path.Combine(icons, gameId + ".png"), Path.Combine(covers, gameId + ".jpg") }) {
                        if (!File.Exists(file)) continue;
                        using (Image img = Image.FromFile(file))
                        using (Bitmap bmp = new Bitmap(img, new Size(64, 64))) {
                            IntPtr hIcon = bmp.GetHicon();
                            try { using (Icon temp = Icon.FromHandle(hIcon)) sessionIcon = (Icon)temp.Clone(); }
                            finally { DestroyIcon(hIcon); }
                        }
                        break;
                    }
                }
                f.Icon = sessionIcon ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                tray = new NotifyIcon { Icon = sessionIcon ?? f.Icon, Text = ("Beako · " + args[1]).Substring(0, Math.Min(63, ("Beako · " + args[1]).Length)), Visible = true };
                NotifyIcon currentTray = tray;
                currentTray.DoubleClick += delegate { f.Show(); f.WindowState = FormWindowState.Normal; f.Activate(); };
                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add("Abrir atividade", delegate { f.Show(); f.WindowState = FormWindowState.Normal; f.Activate(); });
                menu.MenuItems.Add("Encerrar atividade", delegate { f.Close(); });
                currentTray.ContextMenu = menu;
            } catch { /* Se a capa ainda nao estiver em cache, mantem icone padrao. */ }
            f.AddChrome("BEAKO · SESSÃO", false);
            try { Application.Run(f); }
            finally { if (tray != null) { tray.Visible = false; tray.Dispose(); } if (sessionIcon != null) sessionIcon.Dispose(); }
            return;
        }
        Application.Run(new BeakoPresence());
    }
    static Label Title(string text, DockStyle dock, int width) { return new Label { Text = text, Dock = dock, Width = width, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Skin.Text, Font = new Font("Segoe UI Semibold", 15f), Margin = new Padding(0) }; }
    static Label Subtle(DockStyle dock, int width) { return new Label { Dock = dock, Width = width, TextAlign = ContentAlignment.MiddleRight, ForeColor = Skin.Muted, Font = new Font("Segoe UI", 9f), Margin = new Padding(0) }; }
    static void Style(DarkButton b, bool primary) {
        b.Dock = DockStyle.Fill; b.Radius = 10; b.Cursor = Cursors.Hand; b.Font = new Font("Segoe UI Semibold", 10f); b.ForeColor = Skin.Text;
        if (primary) { b.BackColor = Skin.Green; b.ForeColor = Color.FromArgb(6, 32, 22); b.FlatAppearance.MouseOverBackColor = Skin.GreenHover; }
        else { b.OutlineOnly = true; b.BackColor = Skin.Background; }
    }
    public BeakoPresence() {
        Text = "Beako Presence"; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Size = new Size(1020, 660); MinimumSize = new Size(800, 520);
        AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        Directory.CreateDirectory(Root); BackColor = Skin.Background;
        TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Skin.Background, Margin = new Padding(0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        HeaderBar header = new HeaderBar { Dock = DockStyle.Fill, Margin = new Padding(0) };
        status.Dock = DockStyle.Right; status.Width = 360; status.ForeColor = Skin.Muted; status.Font = new Font("Segoe UI", 9f); status.TextAlign = ContentAlignment.MiddleRight; status.Padding = new Padding(0, 0, 22, 0); status.Text = "Pronto"; status.BackColor = Skin.Background;
        header.Controls.Add(status); EnableDrag(header); body.Controls.Add(header, 0, 0);
        TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Background, Padding = new Padding(22, 14, 22, 18), Margin = new Padding(0) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        // ---- catálogo
        TableLayoutPanel catalog = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Skin.Background, Margin = new Padding(0, 0, 18, 0) };
        catalog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); catalog.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        Panel catTitle = new Panel { Dock = DockStyle.Fill, BackColor = Skin.Background, Margin = new Padding(0) };
        catalogCount = Subtle(DockStyle.Right, 220); catalogCount.BackColor = Skin.Background; Label catLabel = Title("Jogos", DockStyle.Left, 140); catLabel.BackColor = Skin.Background;
        catTitle.Controls.Add(catalogCount); catTitle.Controls.Add(catLabel); catalog.Controls.Add(catTitle, 0, 0);
        FlatPanel searchPanel = new FlatPanel { Dock = DockStyle.Fill, Radius = 12, BackColor = Skin.Card, Margin = new Padding(0, 2, 0, 8), Padding = new Padding(6, 14, 14, 8) };
        Label glass = new Label { Text = "⌕", Dock = DockStyle.Left, Width = 34, TextAlign = ContentAlignment.TopCenter, ForeColor = Skin.Muted, BackColor = Skin.Card, Font = new Font("Segoe UI Symbol", 13f), Padding = new Padding(0, 0, 0, 0) };
        search.AutoSize = false; search.BorderStyle = BorderStyle.None; search.BackColor = Skin.Card; search.ForeColor = Skin.Text; search.Dock = DockStyle.Fill; search.Font = new Font("Segoe UI", 10.5f);
        search.HandleCreated += delegate { SetCue(search.Handle, 0x1501, new IntPtr(1), "Buscar jogos..."); };
        search.Enter += delegate { searchPanel.BorderColor = Skin.Green; searchPanel.Invalidate(); }; search.Leave += delegate { searchPanel.BorderColor = Skin.Border; searchPanel.Invalidate(); };
        searchPanel.Controls.Add(search); searchPanel.Controls.Add(glass); catalog.Controls.Add(searchPanel, 0, 1);
        list.Dock = DockStyle.Fill; list.Cards = true; list.BackColor = Skin.Background; list.Margin = new Padding(0); list.IsActive = delegate(Game g) { return running.ContainsKey(g.Id); }; catalog.Controls.Add(list, 0, 2);
        TableLayoutPanel buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Skin.Background, Margin = new Padding(0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        start.Text = "▶  Iniciar atividade"; steam.Text = "+ Steam"; sync.Text = "↻ Atualizar";
        Style((DarkButton)start, true); Style((DarkButton)steam, false); Style((DarkButton)sync, false);
        start.Margin = new Padding(0, 8, 8, 0); steam.Margin = new Padding(0, 8, 8, 0); sync.Margin = new Padding(0, 8, 0, 0);
        buttons.Controls.Add(start, 0, 0); buttons.Controls.Add(steam, 1, 0); buttons.Controls.Add(sync, 2, 0); catalog.Controls.Add(buttons, 0, 3);
        selectedCount.Text = "Selecione os jogos para iniciar uma atividade"; selectedCount.ForeColor = Skin.Muted; selectedCount.Font = new Font("Segoe UI", 9f); selectedCount.TextAlign = ContentAlignment.MiddleLeft; selectedCount.Dock = DockStyle.Fill; selectedCount.BackColor = Skin.Background; catalog.Controls.Add(selectedCount, 0, 4);
        content.Controls.Add(catalog, 0, 0);
        // ---- atividades
        TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Skin.Background, Margin = new Padding(0) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        FlatPanel markedPanel = new FlatPanel { Dock = DockStyle.Fill, Radius = 12, BackColor = Skin.Card, Padding = new Padding(10), Margin = new Padding(0, 2, 0, 8) };
        TableLayoutPanel markedLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Skin.Card };
        markedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        markedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        markedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        markedLayout.Controls.Add(new Label { Text = "✓ Jogos marcados", Dock = DockStyle.Fill, ForeColor = Skin.Text, Font = new Font("Segoe UI Semibold", 10f) }, 0, 0);
        marked.Dock = DockStyle.Fill; marked.BorderStyle = BorderStyle.None; marked.BackColor = Skin.Card; marked.ForeColor = Skin.Text;
        marked.Font = new Font("Segoe UI", 9f); marked.SelectionMode = SelectionMode.MultiExtended;
        markedLayout.Controls.Add(marked, 0, 1);
        TableLayoutPanel markedActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Card };
        markedActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); markedActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        unmark.Text = "Desmarcar"; clearMarked.Text = "Limpar todos";
        Style((DarkButton)unmark, false); Style((DarkButton)clearMarked, false);
        unmark.Margin = new Padding(0, 2, 5, 0); clearMarked.Margin = new Padding(5, 2, 0, 0);
        markedActions.Controls.Add(unmark, 0, 0); markedActions.Controls.Add(clearMarked, 1, 0);
        markedLayout.Controls.Add(markedActions, 0, 2); markedPanel.Controls.Add(markedLayout);
        right.Controls.Add(markedPanel, 0, 1);
        marked.SelectedIndexChanged += delegate { if (!updatingMarked) unmark.Enabled = marked.SelectedItems.Count > 0; };
        unmark.Click += delegate {
            Game[] remove = marked.SelectedItems.Cast<Game>().ToArray();
            foreach (Game g in remove) list.Unselect(g.Id);
            RefreshMarked();
        };
        clearMarked.Click += delegate { list.ClearSelection(); RefreshMarked(); };
        Panel actTitle = new Panel { Dock = DockStyle.Fill, BackColor = Skin.Background, Margin = new Padding(0) };
        session = Subtle(DockStyle.Right, 150); session.BackColor = Skin.Background; session.Text = "Nenhuma sessão ativa"; Label actLabel = Title("Atividades", DockStyle.Left, 160); actLabel.BackColor = Skin.Background;
        actTitle.Controls.Add(session); actTitle.Controls.Add(actLabel); right.Controls.Add(actTitle, 0, 2);
        FlatPanel sessionPanel = new FlatPanel { Dock = DockStyle.Fill, Radius = 14, BackColor = Skin.Card, Padding = new Padding(10), Margin = new Padding(0, 2, 0, 0) };
        TableLayoutPanel sessionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Skin.Card, Margin = new Padding(0) };
        sessionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); sessionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); sessionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        active.Dock = DockStyle.Fill; active.Compact = true; active.BackColor = Skin.Card; active.Margin = new Padding(0); active.Subtitle = delegate(Game g) { return started.ContainsKey(g.Id) ? "Ativo há " + FormatSpan(DateTime.Now - started[g.Id]) : "Encerrado"; }; sessionLayout.Controls.Add(active, 0, 0);
        discordState.Dock = DockStyle.Fill; discordState.Text = "● Discord: verificando..."; discordState.ForeColor = Skin.Muted; discordState.BackColor = Skin.Card; discordState.Font = new Font("Segoe UI", 9f); discordState.TextAlign = ContentAlignment.MiddleLeft; discordState.Padding = new Padding(6, 0, 0, 0); sessionLayout.Controls.Add(discordState, 0, 1);
        sessionPanel.Controls.Add(sessionLayout); right.Controls.Add(sessionPanel, 0, 3);
        TableLayoutPanel stopButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Background, Margin = new Padding(0) }; stopButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); stopButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        stop.Text = "Parar selecionados"; all.Text = "Parar todos"; Style((DarkButton)stop, false); Style((DarkButton)all, false); ((DarkButton)stop).HoverBorder = Skin.Danger; ((DarkButton)all).HoverBorder = Skin.Danger;
        stop.Margin = new Padding(0, 8, 8, 0); all.Margin = new Padding(0, 8, 0, 0); stopButtons.Controls.Add(stop, 0, 0); stopButtons.Controls.Add(all, 1, 0); right.Controls.Add(stopButtons, 0, 4);
        content.Controls.Add(right, 1, 0); body.Controls.Add(content, 0, 1);
        Controls.Add(body); AddChrome("", true);
        list.SelectedIndexChanged += delegate { int n = list.SelectedItems.Count; selectedCount.Text = n == 0 ? "Selecione os jogos para iniciar uma atividade" : n == 1 ? "1 jogo selecionado" : n + " jogos selecionados"; if (!restoring) SaveSettings(); RefreshMarked(); UpdateButtons(); };
        active.SelectedIndexChanged += delegate { UpdateButtons(); };
        search.TextChanged += delegate { searchDelay.Stop(); searchDelay.Start(); }; searchDelay.Tick += delegate { searchDelay.Stop(); Filter(); }; start.Click += delegate { Launch(); }; stop.Click += delegate {
            List<Game> targets = active.SelectedItems.ToList();
            if (targets.Count == 0) targets = list.SelectedItems.Where(g => running.ContainsKey(g.Id)).ToList();
            foreach (Game g in targets) Stop(g.Id); RefreshActive(); status.Text = targets.Count + " sessão(ões) encerrada(s).";
        }; all.Click += delegate { StopAll(); };
        sync.Click += async delegate { await Sync(); }; steam.Click += async delegate { await Steam(); };
        timer.Interval = 2500; timer.Tick += delegate {
            foreach (string id in running.Keys.ToArray()) { bool exited = false; try { exited = running[id].HasExited; } catch { exited = true; } if (exited) { running[id].Dispose(); running.Remove(id); started.Remove(id); runningNames.Remove(id); RefreshActive(); } }
            if (running.Count > 0) active.Invalidate(); UpdateWorkspace(); session.Text = running.Count == 0 ? "Nenhuma sessão ativa" : running.Count == 1 ? "1 sessão ativa" : running.Count + " sessões ativas";
        }; timer.Start(); LoadSettings(); FormClosing += delegate { timer.Stop(); searchDelay.Stop(); SaveSettings(); StopAll(); }; UpdateButtons();
        Shown += async delegate {
            string cache = Path.Combine(Root, "catalog.json");
            if (File.Exists(cache)) { try { Parse(File.ReadAllText(cache)); Filter(); status.Text = "Catálogo carregado."; } catch { status.Text = "Atualize o catálogo."; } }
            // Atualiza sozinho quando não há cache ou ele tem mais de 7 dias.
            if (games.Count == 0 || (DateTime.Now - File.GetLastWriteTime(cache)).TotalDays > 7) await Sync();
        };
    }
    async Task<string> Download(string url) {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (WebClient client = new WebClient()) {
            client.Encoding = System.Text.Encoding.UTF8; client.Headers[HttpRequestHeader.UserAgent] = "BeakoPresence/1.0";
            Task<string> fetch = client.DownloadStringTaskAsync(url);
            if (await Task.WhenAny(fetch, Task.Delay(25000)) != fetch) { client.CancelAsync(); throw new IOException("Tempo de conexão esgotado."); }
            return await fetch;
        }
    }
    void Parse(string text) {
        object[] data = json.DeserializeObject(text) as object[];
        if (data == null) throw new IOException("Resposta do catálogo inválida.");
        List<Game> next = new List<Game>();
        foreach (object item in data) {
            var entry = item as Dictionary<string, object>; if (entry == null || !entry.ContainsKey("executables") || !entry.ContainsKey("id") || !entry.ContainsKey("name")) continue;
            var exes = entry["executables"] as object[]; if (exes == null) continue;
            var win = exes.OfType<Dictionary<string, object>>().Where(e => e.ContainsKey("os") && Convert.ToString(e["os"]) == "win32" && e.ContainsKey("name")).OrderBy(e => e.ContainsKey("is_launcher") && Convert.ToBoolean(e["is_launcher"])).ToList();
            foreach (var exe in win) {
                string id = Convert.ToString(entry["id"]), name = Convert.ToString(exe["name"]);
                if (!id.All(char.IsDigit) || id.Length == 0 || !SafeExe(name)) continue;
                next.Add(new Game { Id = id, Name = Convert.ToString(entry["name"]), Exe = name, IconHash = entry.ContainsKey("icon") ? Convert.ToString(entry["icon"]) : "" }); break;
            }
        }
        games = next.OrderBy(g => g.Name).ToList();
    }
    static bool SafeExe(string name) {
        if (String.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(":") || name.Contains("\"")) return false;
        string[] parts = name.Replace('\\', '/').Split('/');
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && parts.All(p => p.Length > 0 && p != "." && p != ".." && p.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !p.EndsWith(".") && !p.EndsWith(" "));
    }
    void Filter() {
        list.BeginUpdate(); list.Items.Clear();
        foreach (Game g in games.Where(g => g.Name.IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).Take(500)) list.Items.Add(g);
        list.EndUpdate(); catalogCount.Text = list.Items.Count == games.Count ? games.Count + " jogos" : list.Items.Count + " de " + games.Count + " jogos";
        if (savedSelection.Count > 0 && games.Count > 0) {
            restoring = true;
            foreach (Game g in games) if (savedSelection.Contains(g.Id)) list.Select(g);
            savedSelection.Clear(); restoring = false; RefreshMarked(); SaveSettings();
        }
    }
    string SettingsFile { get { return Path.Combine(Root, "settings.json"); } }
    void LoadSettings() {
        try {
            if (!File.Exists(SettingsFile)) return;
            var data = json.DeserializeObject(File.ReadAllText(SettingsFile)) as Dictionary<string, object>; if (data == null) return;
            object v;
            if (data.TryGetValue("width", out v) && data.ContainsKey("height")) {
                int w = Convert.ToInt32(v), h = Convert.ToInt32(data["height"]);
                Rectangle area = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Max(MinimumSize.Width, Math.Min(w, area.Width)), Math.Max(MinimumSize.Height, Math.Min(h, area.Height)));
            }
            if (data.TryGetValue("selected", out v)) { var ids = v as object[]; if (ids != null) foreach (object id in ids) savedSelection.Add(Convert.ToString(id)); }
        } catch { /* Configuração corrompida: começa do zero. */ }
    }
    void SaveSettings() {
        if (restoring) return;
        try {
            Size size = WindowState == FormWindowState.Normal ? Size : RestoreBounds.Size;
            var data = new Dictionary<string, object> { { "width", size.Width }, { "height", size.Height }, { "selected", list.SelectedItems.Select(g => g.Id).ToArray() } };
            Directory.CreateDirectory(Root); File.WriteAllText(SettingsFile, json.Serialize(data));
        } catch { }
    }
    void RefreshMarked() {
        updatingMarked = true;
        try {
            string[] previous = marked.SelectedItems.Cast<Game>().Select(g => g.Id).ToArray();
            marked.BeginUpdate(); marked.Items.Clear();
            foreach (Game g in list.SelectedItems.OrderBy(g => g.Name)) marked.Items.Add(g);
            for (int i = 0; i < marked.Items.Count; i++)
                if (previous.Contains(((Game)marked.Items[i]).Id)) marked.SetSelected(i, true);
            marked.EndUpdate();
            unmark.Enabled = marked.SelectedItems.Count > 0;
            clearMarked.Enabled = marked.Items.Count > 0;
        } finally { updatingMarked = false; }
    }
    void UpdateButtons() {
        start.Enabled = list.SelectedItems.Count > 0;
        stop.Enabled = active.SelectedItems.Count > 0 || list.SelectedItems.Any(g => running.ContainsKey(g.Id));
        all.Enabled = running.Count > 0;
    }
    async Task Sync() {
        if (busy) return; busy = true; sync.Enabled = steam.Enabled = false; status.Text = "Baixando catálogo do Discord...";
        try {
            string text = await Download("https://discord.com/api/applications/detectable"); Parse(text);
            string cache = Path.Combine(Root, "catalog.json"); File.WriteAllText(cache + ".tmp", text); File.Copy(cache + ".tmp", cache, true); File.Delete(cache + ".tmp");
            Filter(); status.Text = games.Count + " jogos com executável Windows disponíveis.";
        } catch (Exception ex) { status.Text = "Falha: " + ex.Message; }
        finally { busy = false; sync.Enabled = steam.Enabled = true; }
    }
    async Task Steam() {
        if (busy) return;
        using (DarkWindow dialog = new DarkWindow { Text = "ID da Steam", Size = new Size(370, 200), StartPosition = FormStartPosition.CenterParent }) {
            TextBox idBox = new TextBox { Left = 24, Top = 75, Width = 320, BorderStyle = BorderStyle.None, BackColor = Skin.Surface, ForeColor = Color.White, Font = new Font("Segoe UI", 12) };
            Button ok = new DarkButton { Text = "Buscar jogo", Left = 24, Top = 118, Width = 320, Height = 40, BackColor = Skin.Green, ForeColor = Color.FromArgb(10, 40, 28), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK, Font = new Font("Segoe UI", 11) };
            dialog.Controls.Add(new Label { Text = "Informe o número do jogo na Steam", Left = 24, Top = 45, Width = 320, ForeColor = Color.FromArgb(155, 173, 188) }); dialog.AddChrome("BEAKO · BUSCA STEAM", false); dialog.Controls.Add(idBox); dialog.Controls.Add(ok); dialog.AcceptButton = ok; dialog.ActiveControl = idBox; dialog.KeyPreview = true; dialog.KeyDown += delegate(object o, KeyEventArgs k) { if (k.KeyCode == Keys.Escape) dialog.Close(); };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string id = idBox.Text.Trim(); if (id.Length == 0 || !id.All(char.IsDigit)) { status.Text = "Informe somente o número do AppID Steam."; return; }
            busy = true; sync.Enabled = steam.Enabled = false; status.Text = "Consultando Steam...";
            try {
                var data = json.DeserializeObject(await Download("https://store.steampowered.com/api/appdetails?appids=" + id + "&l=portuguese")) as Dictionary<string, object>;
                var result = (Dictionary<string, object>)data[id];
                if (!Convert.ToBoolean(result["success"])) throw new IOException("ID não encontrado na Steam.");
                var details = (Dictionary<string, object>)result["data"]; string name = Convert.ToString(details["name"]);
                search.Text = name; searchDelay.Stop(); Filter(); status.Text = list.Items.Count > 0 ? "Encontrado: " + name : "Steam: " + name + ". Sem correspondência exata; tente parte do nome na busca.";
            } catch (Exception ex) { status.Text = "Falha: " + ex.Message; }
            finally { busy = false; sync.Enabled = steam.Enabled = true; }
        }
    }
    void Launch() {
        Game[] selected = list.SelectedItems.Cast<Game>().ToArray();
        if (selected.Length == 0) { status.Text = "Marque as caixas dos jogos que deseja abrir."; return; }
        List<string> errors = new List<string>(); int count = 0;
        foreach (Game game in selected) {
            if (running.ContainsKey(game.Id)) {
                if (!running[game.Id].HasExited) continue;
                Stop(game.Id);
            }
            try {
                string folder = Path.Combine(Root, "sessions", game.Id); string target = Path.GetFullPath(Path.Combine(folder, game.Exe.Replace('/', Path.DirectorySeparatorChar)));
                if (!SafeExe(game.Exe) || !target.StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Executável inválido.");
                // A simulacao usa uma copia identificavel do Beako Presence; nao altera executaveis de jogos instalados.\n                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Application.ExecutablePath, target, true);
                Process child = Process.Start(new ProcessStartInfo(target, "--process \"" + game.Name.Replace("\"", "'").TrimEnd('\\') + "\" " + game.Id + " " + Process.GetCurrentProcess().Id) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target) });
                if (child == null) throw new IOException("Processo não iniciado.");
                running.Add(game.Id, child); started.Add(game.Id, DateTime.Now); runningNames[game.Id] = game.Name; count++;
            } catch (Exception ex) { errors.Add(game.Name + ": " + ex.Message); }
        }
        RefreshActive();
        status.Text = count + " iniciados · " + running.Count + " ativos. O Discord decide qual exibir.";
        if (errors.Count > 0) ShowNotice("Falhas ao iniciar", String.Join("\n", errors));
    }
    void Stop(string id) {
        if (!running.ContainsKey(id)) return;
        Process child = running[id];
        try { if (!child.HasExited) { child.Kill(); child.WaitForExit(1500); } }
        catch (Exception ex) { status.Text = "Erro ao parar: " + ex.Message; return; }
        child.Dispose(); running.Remove(id); started.Remove(id); runningNames.Remove(id);
    }
    void UpdateWorkspace() {
        try {
            Process[] processes = Process.GetProcessesByName("Discord"); bool open = processes.Length > 0;
            foreach (Process process in processes) process.Dispose();
            discordState.Text = open ? "● Discord aberto" : "○ Abra o Discord desktop";
            discordState.ForeColor = open ? Skin.Green : Skin.Muted;
        } catch { discordState.Text = "Discord: não foi possível verificar"; }
    }
    void ShowNotice(string title, string message) {
        using (DarkWindow dialog = new DarkWindow { Text = title, Size = new Size(540, 330), StartPosition = FormStartPosition.CenterParent, Font = new Font("Segoe UI", 10) }) {
            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 2, ColumnCount = 1 };
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            TextBox text = new TextBox { Text = message, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Skin.Background, ForeColor = Color.FromArgb(231, 237, 245), Dock = DockStyle.Fill };
            DarkButton ok = new DarkButton { Text = "Entendi", Dock = DockStyle.Fill, BackColor = Skin.Green, ForeColor = Color.FromArgb(10, 40, 28), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            content.Controls.Add(text, 0, 0); content.Controls.Add(ok, 0, 1); dialog.Controls.Add(content); dialog.AddChrome(title, false); dialog.AcceptButton = ok; dialog.ShowDialog(this);
        }
    }
    void RefreshActive() {
        string[] selected = active.SelectedItems.Cast<Game>().Select(g => g.Id).ToArray();
        active.BeginUpdate(); active.ClearSelection(); active.Items.Clear();
        foreach (string id in running.Keys) { int index = active.Items.Count; Game source = games.FirstOrDefault(g => g.Id == id); active.Items.Add(new Game { Id = id, Name = runningNames[id], IconHash = source != null ? source.IconHash : "" }); if (selected.Contains(id)) active.SetSelected(index, true); }
        active.EndUpdate(); list.Invalidate(); UpdateWorkspace(); UpdateButtons();
    }
    void StopAll() { foreach (string id in running.Keys.ToArray()) Stop(id); RefreshActive(); }
}
