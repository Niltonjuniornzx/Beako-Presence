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
[assembly: System.Reflection.AssemblyCompany("Beako")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 Beako")]
[assembly: System.Reflection.AssemblyTrademark("")]
[assembly: System.Reflection.AssemblyCulture("")]
[assembly: System.Reflection.AssemblyVersion("3.6.2.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.6.2.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("3.6.2")]

static class Skin {
    public static readonly Color Background = Color.FromArgb(13, 15, 17);
    public static readonly Color Surface = Color.FromArgb(22, 24, 29);
    public static readonly Color SurfaceHover = Color.FromArgb(32, 35, 42);
    public static readonly Color Card = Color.FromArgb(25, 28, 33);
    public static readonly Color CardHover = Color.FromArgb(31, 34, 42);
    public static readonly Color CardSelected = Color.FromArgb(21, 40, 33);
    public static readonly Color Green = Color.FromArgb(41, 196, 135);
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
    public int Radius = 5; public Color BorderColor = Skin.Border;
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
        Rectangle badge = new Rectangle(10, 4, 24, 24);
        if (avatar != null) {
            var state = g.Save();
            using (GraphicsPath circle = new GraphicsPath()) { circle.AddEllipse(badge); g.SetClip(circle, CombineMode.Intersect); }
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(avatar, badge); g.Restore(state);
            using (Pen ring = new Pen(Color.FromArgb(120, Skin.Green), 1.6f)) g.DrawEllipse(ring, badge);
        }
        using (Font title = new Font("Segoe UI Semibold", 9f), small = new Font("Segoe UI", 8f)) {
            TextRenderer.DrawText(g, "Beako Presence", title, new Point(41, 7), Skin.Text);
            
        }
        using (Pen line = new Pen(Skin.Border)) g.DrawLine(line, 0, Height - 1, Width, Height - 1);
    }
}
// Barra de controles desenhada em uma unica superficie: sem fundos nativos de Button.
class WindowControls : Control {
    int hover = -1;
    public Action MinimizeAction, MaximizeAction, CloseAction;
    public WindowControls() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Skin.Background; Width = 108; Height = 32; TabStop = false;
    }
    int Hit(Point p) { if (p.X < 0 || p.X >= Width || p.Y < 0 || p.Y >= Height) return -1; return Math.Min(2, p.X * 3 / Math.Max(1, Width)); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = Hit(e.Location); if (h != hover) { hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e) {
        base.OnMouseClick(e); if (e.Button != MouseButtons.Left) return;
        int h = Hit(e.Location); if (h == 0 && MinimizeAction != null) MinimizeAction();
        else if (h == 1 && MaximizeAction != null) MaximizeAction();
        else if (h == 2 && CloseAction != null) CloseAction();
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics;
        using (SolidBrush bg = new SolidBrush(Skin.Background)) g.FillRectangle(bg, ClientRectangle);
        int w = Width / 3;
        for (int i = 0; i < 3; i++) {
            Rectangle r = new Rectangle(i * w, 0, i == 2 ? Width - i * w : w, Height);
            if (hover == i) using (SolidBrush b = new SolidBrush(i == 2 ? Color.FromArgb(155, 39, 54) : Color.FromArgb(39, 46, 59))) g.FillRectangle(b, r);
            int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            using (Pen pen = new Pen(i == 2 ? Color.FromArgb(255, 139, 151) : Skin.Text, 1.2f)) {
                if (i == 0) g.DrawLine(pen, cx - 4, cy + 1, cx + 4, cy + 1);
                else if (i == 1) g.DrawRectangle(pen, cx - 4, cy - 3, 8, 6);
                else { g.DrawLine(pen, cx - 3, cy - 3, cx + 3, cy + 3); g.DrawLine(pen, cx + 3, cy - 3, cx - 3, cy + 3); }
            }
        }
    }
}
class DarkButton : Button {
    bool hover;
    public bool ChromeControl;
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
        Rectangle r = ChromeControl ? ClientRectangle : new Rectangle(1, 1, Width - 3, Height - 3);
        using (GraphicsPath path = Skin.Round(r, Radius)) {
            if (OutlineOnly) {
                Color border = hover && Enabled ? HoverBorder : (Enabled ? Color.FromArgb(70, 78, 90) : Color.FromArgb(40, 44, 52));
                { if (hover && Enabled) using (SolidBrush tint = new SolidBrush(Color.FromArgb(24, HoverBorder))) e.Graphics.FillPath(tint, path); using (Pen pen = new Pen(border, 1.5f)) e.Graphics.DrawPath(pen, path); }
            } else {
                Color color = !Enabled ? Color.FromArgb(28, 30, 34) : hover ? (FlatAppearance.MouseOverBackColor.IsEmpty ? Skin.SurfaceHover : FlatAppearance.MouseOverBackColor) : BackColor;
                if (!ChromeControl || hover) using (SolidBrush brush = new SolidBrush(color)) e.Graphics.FillPath(brush, path);
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
        Panel bar = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Skin.Background, Padding = new Padding(16, 0, 8, 0) };
        Label caption = new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(120, 136, 153), Font = new Font("Segoe UI", 9) };
        MouseEventHandler drag = delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
        bar.MouseDown += drag; caption.MouseDown += drag;
        EventHandler toggle = delegate { if (WindowState != FormWindowState.Maximized) MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
        if (maximize) caption.DoubleClick += toggle;
        bar.Controls.Add(caption);
        FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Right, Width = maximize ? 105 : 70, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        DarkButton min = ChromeButton("—"); min.Click += delegate { WindowState = FormWindowState.Minimized; }; actions.Controls.Add(min);
        if (maximize) { DarkButton max = ChromeButton("□"); max.Click += toggle; actions.Controls.Add(max); }
        DarkButton close = ChromeButton("×"); close.Click += delegate { Close(); }; close.ForeColor = Color.FromArgb(255, 139, 151); actions.Controls.Add(close);
        bar.Controls.Add(actions); actions.BringToFront();
        Controls.Add(bar); bar.SendToBack();
        /* Bordas nativas redimensionaveis */
    }
    DarkButton ChromeButton(string text) { return new DarkButton { Text = text, Width = 33, Height = 29, Margin = new Padding(1, 1, 1, 0), FlatStyle = FlatStyle.Flat, BackColor = Skin.Background, ForeColor = Color.FromArgb(176, 187, 204), Cursor = Cursors.Hand, Font = new Font("Segoe UI", 12), FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(39, 46, 59) } }; }
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
    static readonly System.Threading.SemaphoreSlim coverSlots = new System.Threading.SemaphoreSlim(2);
    readonly JavaScriptSerializer coverJson = new JavaScriptSerializer();
    public bool Compact;
    readonly Font nameFont = new Font("Segoe UI Semibold", 9f), smallFont = new Font("Segoe UI", 8.5f), initialsFont = new Font("Segoe UI Semibold", 20f), pillFont = new Font("Segoe UI Semibold", 7.5f);
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
    string IconFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence", "icons"); } }
    Image Thumbnail(Game game) {
        Image image;
        if (thumbnails.TryGetValue(game.Id, out image)) return image;
        foreach (string path in new [] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", game.Id + ".png"), Path.Combine(IconFolder, game.Id + ".png") }) {
            if (!File.Exists(path)) continue;
            try { using (Image source = Image.FromFile(path)) image = new Bitmap(source); thumbnails[game.Id] = image; return image; } catch { }
        }
        thumbnails[game.Id] = null;
        if (iconRequests.Add(game.Id)) FetchIcon(game);
        return null;
    }
    async void FetchIcon(Game game) {
        await coverSlots.WaitAsync();
        try {
            if (IsDisposed || !game.Id.All(char.IsDigit)) return;
            if (String.IsNullOrEmpty(game.IconHash)) {
                using (WebClient metadata = new WebClient()) {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    Task<string> pending = metadata.DownloadStringTaskAsync("https://discord.com/api/v10/applications/" + game.Id + "/rpc");
                    if (await Task.WhenAny(pending, Task.Delay(8000)) != pending) { metadata.CancelAsync(); try { await pending; } catch { } return; }
                    var record = coverJson.DeserializeObject(await pending) as Dictionary<string, object>;
                    if (record != null && record.ContainsKey("icon")) game.IconHash = Convert.ToString(record["icon"]);
                }
            }
            if (IsDisposed || String.IsNullOrEmpty(game.IconHash) || !game.IconHash.All(c => char.IsLetterOrDigit(c) || c == '_')) return;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string url = "https://cdn.discordapp.com/app-icons/" + game.Id + "/" + game.IconHash + ".png?size=128";
            using (WebClient client = new WebClient()) {
                Task<byte[]> request = client.DownloadDataTaskAsync(url);
                if (await Task.WhenAny(request, Task.Delay(10000)) != request) { client.CancelAsync(); try { await request; } catch { } return; }
                byte[] bytes = await request;
                if (bytes.Length == 0 || bytes.Length > 3000000 || IsDisposed) return;
                using (MemoryStream stream = new MemoryStream(bytes))
                using (Image source = Image.FromStream(stream)) {
                    Image bitmap = new Bitmap(source, new Size(64, 64));
                    thumbnails[game.Id] = bitmap;
                    try { Directory.CreateDirectory(IconFolder); bitmap.Save(Path.Combine(IconFolder, game.Id + ".png"), System.Drawing.Imaging.ImageFormat.Png); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                if (!IsDisposed) Invalidate();
            }
        } catch { /* Keep initials on network or decoding failures. */ }
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
            string exeDetail = game.EstimatedExe ? "Sem executável Windows na lista do Discord" : game.CommunityPath ? "Caminho comunidade: " + game.Exe : game.Exes != null && game.Exes.Length > 1 ? game.Exe + " · +" + (game.Exes.Length - 1) + " nomes oficiais" : game.Exe;
            TextRenderer.DrawText(g, exeDetail, smallFont, new Rectangle(rect.X + 12, rect.Y + 112, rect.Width - 24, 18), game.EstimatedExe ? Skin.Muted : Skin.Green, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
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
            using (GraphicsPath rowPath = Skin.Round(bounds, 5)) {
                using (SolidBrush brush = new SolidBrush(selected ? Skin.CardSelected : hover == i ? Skin.SurfaceHover : Skin.Card)) e.Graphics.FillPath(brush, rowPath);
                if (selected) using (Pen pen = new Pen(Skin.Green, 1.5f)) e.Graphics.DrawPath(pen, rowPath);
            }
            Rectangle box = new Rectangle(bounds.Right - 34, bounds.Y + (bounds.Height - 18) / 2, 18, 18);
            using (GraphicsPath boxPath = Skin.Round(box, 5)) {
                if (selected) using (SolidBrush fill = new SolidBrush(Skin.Green)) e.Graphics.FillPath(fill, boxPath);
                using (Pen border = new Pen(selected ? Skin.Green : Color.FromArgb(110, 118, 130), 1.4f)) e.Graphics.DrawPath(border, boxPath);
            }
            if (selected) using (Pen pen = new Pen(Color.White, 2f)) e.Graphics.DrawLines(pen, new [] { new Point(box.X + 4, box.Y + 9), new Point(box.X + 8, box.Y + 13), new Point(box.X + 14, box.Y + 5) });
            Rectangle icon = new Rectangle(12, bounds.Y + (bounds.Height - 44) / 2, 44, 44);
            Image image = Thumbnail(game);
            if (image != null) Skin.DrawRounded(e.Graphics, image, icon, 10);
            else {
                using (GraphicsPath ip = Skin.Round(icon, 10)) using (SolidBrush brush = new SolidBrush(Skin.Tint(game.Name))) e.Graphics.FillPath(brush, ip);
                string initials = new string(game.Name.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();
                TextRenderer.DrawText(e.Graphics, initials, nameFont, icon, Color.FromArgb(230, 255, 255, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int x = 70; int width = Math.Max(1, bounds.Width - x - 48);
            TextRenderer.DrawText(e.Graphics, game.Name, nameFont, new Rectangle(x, bounds.Y + 14, width, 22), Skin.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string detail = Subtitle != null ? Subtitle(game) : live ? "Processo ativo" : game.EstimatedExe ? "Sem executável Windows na lista do Discord" : game.CommunityPath ? "Caminho comunidade: " + game.Exe : game.Exes != null && game.Exes.Length > 1 ? game.Exe + " · +" + (game.Exes.Length - 1) + " nomes oficiais" : game.Exe;
            TextRenderer.DrawText(e.Graphics, detail ?? "", smallFont, new Rectangle(x, bounds.Y + 38, width, 20), live || Compact ? Skin.Green : Skin.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues && i == keyboardIndex) using (Pen pen = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dot }) e.Graphics.DrawRectangle(pen, bounds.X + 3, bounds.Y + 3, bounds.Width - 7, bounds.Height - 7);
        }
        PaintScrollbar(e.Graphics);
    }

}

class Game { public string Id, Name, Exe, IconHash; public string[] Exes; public bool EstimatedExe, CommunityPath; public override string ToString() { return Name; } }
class BeakoPresence : DarkWindow {
    static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeakoPresence");
    // ---- Log ----
    // Eventos do app (catalogo, sessoes, correcao FC 27, erros) vao para
    // %LOCALAPPDATA%\BeakoPresence\beako.log, girando para .old acima de 512 KB.
    static readonly object LogLock = new object();
    static string LogPath { get { return Path.Combine(Root, "beako.log"); } }
    static void Log(string message) {
        try {
            lock (LogLock) {
                Directory.CreateDirectory(Root);
                if (File.Exists(LogPath)) {
                    try { if (new FileInfo(LogPath).Length > 524288) { string old = LogPath + ".old"; if (File.Exists(old)) File.Delete(old); File.Move(LogPath, old); } } catch { }
                }
                File.AppendAllText(LogPath, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message + "\r\n", new System.Text.UTF8Encoding(false));
            }
        } catch { /* Log nunca pode derrubar o app. */ }
    }
    // ---- Diagnóstico ----
    // Mesmo formato do FC27-DIAGNOSTICO.bat, integrado ao app: versão, Discord,
    // estrutura Steam do FC 27, registro, sessões ativas e cauda do log.
    string BuildDiagnostics() {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        b.AppendLine("=== Diagnóstico Beako Presence (FC 27 / appid " + Fc27AppId + ") ===");
        b.AppendLine("Versão do app: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
        bool discord = false;
        try { discord = Process.GetProcessesByName("Discord").Length > 0; } catch { }
        b.AppendLine(discord ? "[OK]   Discord aberto" : "[!!]   Discord NÃO aberto (abra o Discord desktop)");
        string steam = SteamRootPath();
        b.AppendLine(steam != null ? "[OK]   Steam em: " + steam : "[--]   Steam não encontrada no registro; estrutura usa o caminho padrão");
        string root = Fc27Root();
        b.AppendLine(root != null ? "[OK]   Raiz em uso para o FC 27: " + root : "[!!]   Nenhuma raiz Steam em uso (sessão FC 27 não foi iniciada)");
        string manifest = Fc27ManifestPath();
        string manifestText = manifest != null ? Fc27ManifestText(manifest) : null;
        if (manifestText == null) b.AppendLine("[!!]   appmanifest_" + Fc27AppId + ".acf AUSENTE (inicie a sessão do FC 27 no app)");
        else if (manifestText.IndexOf("InstalledDepots", StringComparison.OrdinalIgnoreCase) >= 0) b.AppendLine("[OK]   appmanifest_" + Fc27AppId + ".acf presente (jogo REAL instalado — o app não mexe nele)");
        else b.AppendLine("[OK]   appmanifest_" + Fc27AppId + ".acf presente (criado pelo app)");
        string exe = root == null ? null : Path.Combine(Path.Combine(Path.Combine(root, "steamapps"), "common"), Path.Combine(Fc27InstallDir, Fc27ExeName));
        b.AppendLine(exe != null && File.Exists(exe) ? "[OK]   FC27.exe: " + exe : "[!!]   FC27.exe ausente");
        Process[] fc27 = new Process[0];
        try {
            fc27 = Process.GetProcessesByName("FC27");
            if (fc27.Length > 0) b.AppendLine("[OK]   FC27.exe em execução (PID " + fc27[0].Id + ")");
            else b.AppendLine("[!!]   FC27.exe NÃO está em execução (inicie a sessão e deixe a janela aberta)");
        } catch { b.AppendLine("[--]   Não foi possível verificar o FC27.exe"); }
        finally { foreach (Process p in fc27) { try { p.Dispose(); } catch { } } }
        string ponteiro = null;
        try { using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam")) { object v = key == null ? null : key.GetValue("SteamPath"); ponteiro = v as string; } } catch { }
        b.AppendLine(ponteiro != null ? "[OK]   Registro HKCU SteamPath: " + ponteiro : "[--]   Sem ponteiro HKCU SteamPath (normal quando há Steam instalada)");
        b.AppendLine(running.Count == 0 ? "[--]   Nenhuma sessão em execução" : "[OK]   Sessões ativas: " + String.Join(", ", new List<string>(runningNames.Values).ToArray()));
        b.AppendLine();
        b.AppendLine("Se tudo acima está [OK] e a quest não avança:");
        b.AppendLine("  1) ACEITE a missão no Discord");
        b.AppendLine("  2) SAIA do Discord de verdade: ícone na bandeja perto do relógio, botão direito, Sair");
        b.AppendLine("  3) Abra o Discord de novo com a sessão do FC 27 rodando");
        b.AppendLine("  4) Espere 1 a 2 minutos e olhe a quest de novo");
        b.AppendLine();
        b.AppendLine("---- últimas linhas do log (" + LogPath + ") ----");
        try {
            if (File.Exists(LogPath)) {
                string[] linhas = File.ReadAllLines(LogPath);
                for (int i = Math.Max(0, linhas.Length - 8); i < linhas.Length; i++) b.AppendLine(linhas[i]);
            } else b.AppendLine("(log ainda vazio)");
        } catch { b.AppendLine("(log indisponível)"); }
        return b.ToString();
    }
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 50000000 };
    List<Game> games = new List<Game>();
    readonly Dictionary<string, List<Process>> running = new Dictionary<string, List<Process>>();
    readonly Dictionary<string, DateTime> started = new Dictionary<string, DateTime>();
    TextBox search = new TextBox(); GamePicker list = new GamePicker(); Label status = new Label();
    Label discordState = new Label(), catalogCount = new Label(); 
    HashSet<string> savedSelection = new HashSet<string>(); bool restoring;
    Button start = new DarkButton(), stop = new DarkButton(), all = new DarkButton(), sync = new DarkButton();
    Label session = new Label(), selectedCount = new Label(); GamePicker active = new GamePicker();
    ListBox marked = new ListBox(); Button unmark = new DarkButton(), clearMarked = new DarkButton();
    bool updatingMarked;
    readonly Dictionary<string, string> runningNames = new Dictionary<string, string>(); Timer timer = new Timer(); bool busy;
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessage")] static extern IntPtr SetCue(IntPtr handle, int message, IntPtr wParam, string text);
    // Cria um "hardlink": outro nome de arquivo apontando para o mesmo EXE, sem gravar bytes novos no disco.
    // O Discord detecta o jogo pelo nome do processo, entao o hardlink funciona igual a copia,
    // mas nao dispara a heuristica de "programa que se copia com outro nome e se executa" (padrao de dropper).
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);
    static string FormatSpan(TimeSpan t) { return ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00"); }
    // Remove pastas e processos de sessão órfãos (sobras de crash/desligamento).
    // Roda só no app principal, antes da interface abrir. É conservador:
    // - pasta com EXE em execução é mantida;
    // - processo de sessão só é encerrado se NENHUMA outra instância do app
    //   principal estiver viva (evita matar sessões de outra janela aberta);
    // - EXE em uso trava a exclusão da pasta, então nada vivo é apagado.
    static void CleanupOrphanSessions() {
        try {
            string sessions = Path.Combine(Root, "sessions");
            if (!Directory.Exists(sessions)) return;
            int self = Process.GetCurrentProcess().Id;
            string selfExe = Application.ExecutablePath;
            var live = new Dictionary<string, Process>(StringComparer.OrdinalIgnoreCase);
            foreach (Process p in Process.GetProcesses()) {
                try {
                    if (p.Id == self) { p.Dispose(); continue; }
                    string path = p.MainModule.FileName;
                    if (!String.IsNullOrEmpty(path) && !live.ContainsKey(path)) live.Add(path, p);
                    else p.Dispose();
                } catch { try { p.Dispose(); } catch { } }
            }
            try {
                bool otherMainAlive = false;
                foreach (string path in live.Keys)
                    if (path.Equals(selfExe, StringComparison.OrdinalIgnoreCase)) { otherMainAlive = true; break; }
                foreach (string dir in Directory.GetDirectories(sessions)) {
                    string[] exes;
                    try { exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories); }
                    catch { continue; }
                    List<Process> liveChildren = new List<Process>();
                    foreach (string exe in exes) { Process proc; if (live.TryGetValue(exe, out proc)) liveChildren.Add(proc); }
                    if (liveChildren.Count == 0) {
                        try { Directory.Delete(dir, true); } catch { }
                    } else if (!otherMainAlive) {
                        foreach (Process proc in liveChildren) { try { if (!proc.HasExited) proc.Kill(); } catch { } }
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            } finally { foreach (Process p in live.Values) try { p.Dispose(); } catch { } }
        } catch { }
    }
    // ---- Correção EA SPORTS FC 27 (método Steam da r/DiscordQuests) ----
    // O Discord publica o FC 27 sem executáveis Windows; a quest só é creditada
    // quando o client enxerga o jogo instalado na Steam. Ao iniciar a sessão do
    // FC 27 o app cria appmanifest_4080220.acf + steamapps\common\EA SPORTS FC 27\
    // + FC27.exe e roda de lá; ao parar (ou fechar o app) remove o que criou.
    // Manifest de jogo comprado (com InstalledDepots) nunca é tocado.
    const string Fc27Id = "1531874756096295054";
    const string Fc27AppId = "4080220";
    const string Fc27InstallDir = "EA SPORTS FC 27";
    const string Fc27ExeName = "FC27.exe";
    static bool IsFc27Id(string id) { return id == Fc27Id || id == "community-easportsfc27"; }
    static bool IsFc27(Game g) { return g != null && (IsFc27Id(g.Id) || NormalizeGameName(g.Name) == "easportsfc27"); }
    static string SteamRootPath() {
        try {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam")) {
                object v = key == null ? null : key.GetValue("SteamPath");
                if (v is string && !String.IsNullOrWhiteSpace((string)v)) return ((string)v).Replace('/', '\\');
            }
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam")) {
                object v = key == null ? null : key.GetValue("InstallPath");
                if (v is string && !String.IsNullOrWhiteSpace((string)v)) return ((string)v).TrimEnd('\\');
            }
        } catch { }
        return null;
    }
    static string Fc27ManifestText(string path) {
        try { return File.ReadAllText(path); } catch { return null; }
    }
    // Caminho padrão de instalação da Steam; usado para emular a estrutura
    // em máquinas SEM Steam (o Discord consulta o mesmo caminho/registro).
    static string Fc27DefaultRoot() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"); }
    static string Fc27RootMarkerPath() { return Path.Combine(Root, "steamfix-root.txt"); }
    static string Fc27Root() {
        string steam = SteamRootPath();
        if (steam != null) return steam;
        try { string m = File.ReadAllText(Fc27RootMarkerPath()).Trim(); if (m.Length > 0) return m; } catch { }
        return null;
    }
    static string Fc27ManifestPath() {
        string steam = Fc27Root();
        if (steam == null) return null;
        return Path.Combine(Path.Combine(steam, "steamapps"), "appmanifest_" + Fc27AppId + ".acf");
    }
    // Cria manifest + pasta do FC 27 na Steam e devolve o FC27.exe a executar.
    // Sem Steam na máquina: cria a estrutura no caminho padrão e registra o
    // ponteiro SteamPath no registro do usuário (marcado para limpeza depois).
    // Jogo comprado (manifest com InstalledDepots) nunca é tocado.
    static string PrepareFc27SteamFix() {
        string steam = SteamRootPath();
        bool emulado = false;
        if (steam == null) { steam = Fc27DefaultRoot(); emulado = true; }
        string steamapps = Path.Combine(steam, "steamapps");
        Directory.CreateDirectory(steamapps);
        if (emulado) RegisterFc27EmulatedRoot(steam);
        string manifestPath = Path.Combine(steamapps, "appmanifest_" + Fc27AppId + ".acf");
        string text = Fc27ManifestText(manifestPath);
        if (text != null && text.IndexOf("InstalledDepots", StringComparison.OrdinalIgnoreCase) >= 0) return null;
        string content = "\"AppState\"\r\n{\r\n    \"appid\"\t\t\"" + Fc27AppId + "\"\r\n    \"name\"\t\t\"EA SPORTS FC\u2122 27\"\r\n    \"StateFlags\"\t\t\"4\"\r\n    \"installdir\"\t\t\"" + Fc27InstallDir + "\"\r\n}\r\n";
        File.WriteAllText(manifestPath, content, new System.Text.UTF8Encoding(false));
        string folder = Path.Combine(Path.Combine(steamapps, "common"), Fc27InstallDir);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, Fc27ExeName);
    }
    static void RegisterFc27EmulatedRoot(string steam) {
        try {
            Directory.CreateDirectory(Root);
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("Software\\Valve\\Steam")) {
                object atual = key.GetValue("SteamPath");
                if (atual is string && !String.IsNullOrWhiteSpace((string)atual)) return;
                key.SetValue("SteamPath", steam.Replace('/', '\\').ToLowerInvariant());
            }
            File.WriteAllText(Fc27RootMarkerPath(), steam.Replace('/', '\\'), new System.Text.UTF8Encoding(false));
        } catch { }
    }
    // Remove apenas o que o PrepareFc27SteamFix criou: manifest mínimo, pasta
    // que contém somente o FC27.exe e, na raiz emulada, o ponteiro do registro
    // e pastas que ficaram vazias. Qualquer outra coisa é preservada.
    static void RemoveFc27SteamFix() {
        string manifestPath = Fc27ManifestPath();
        if (manifestPath != null && File.Exists(manifestPath)) {
            string text = Fc27ManifestText(manifestPath);
            if (text != null && text.IndexOf(Fc27AppId, StringComparison.Ordinal) >= 0 && text.IndexOf("InstalledDepots", StringComparison.OrdinalIgnoreCase) < 0) {
                try { File.Delete(manifestPath); Log("FC 27: appmanifest_4080220.acf removido."); } catch { }
            }
        }
        string steam = Fc27Root();
        if (steam == null) return;
        string folder = Path.Combine(Path.Combine(Path.Combine(steam, "steamapps"), "common"), Fc27InstallDir);
        if (Directory.Exists(folder)) {
            bool ours = true;
            try {
                foreach (FileInfo f in new DirectoryInfo(folder).GetFiles("*", SearchOption.AllDirectories))
                    if (!f.Name.Equals(Fc27ExeName, StringComparison.OrdinalIgnoreCase)) { ours = false; break; }
            } catch { ours = false; }
            if (ours) { try { Directory.Delete(folder, true); Log("FC 27: pasta '" + Fc27InstallDir + "' removida da Steam."); } catch { } }
        }
        try {
            string marker = Fc27RootMarkerPath();
            if (!File.Exists(marker)) return;
            string marcado = File.ReadAllText(marker).Trim().Replace('/', '\\').TrimEnd('\\');
            if (!String.Equals(marcado, Fc27DefaultRoot(), StringComparison.OrdinalIgnoreCase)) return;
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam", true)) {
                if (key != null) {
                    object v = key.GetValue("SteamPath");
                    if (v is string && ((string)v).Replace('/', '\\').Trim().TrimEnd('\\').Equals(marcado, StringComparison.OrdinalIgnoreCase)) {
                        key.DeleteValue("SteamPath", false);
                        bool vazia = key.SubKeyCount == 0 && key.ValueCount == 0;
                        key.Close();
                        if (vazia) Microsoft.Win32.Registry.CurrentUser.DeleteSubKey("Software\\Valve\\Steam", false);
                    }
                }
            }
            foreach (string dir in new[] { Path.Combine(steam, "steamapps", "common"), Path.Combine(steam, "steamapps"), steam }) {
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) { try { Directory.Delete(dir); } catch { break; } } else break;
            }
            File.Delete(marker);
        } catch { }
    }
    // Sobras de crash: sem nenhum FC27.exe vivo, limpa manifest e pasta na abertura do app.
    static void CleanupStaleFc27Fix() {
        try {
            bool alive = false;
            foreach (Process p in Process.GetProcessesByName("FC27")) { alive = true; try { p.Dispose(); } catch { } }
            if (alive) { Log("FC 27: sobras mantidas, ha FC27.exe vivo."); return; }
            RemoveFc27SteamFix();
            Log("FC 27: limpeza de sobras executada na abertura do app.");
        } catch { }
    }
    // Fonte do stub do FC 27: exatamente o mesmo código do primeiro teste que
    // registrou atividade no Discord (janela nativa "EA SPORTS FC 27", 380x190).
    const string Fc27StubSource = @"
using System;
using System.Drawing;
using System.Windows.Forms;

static class Program {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Form f = new Form();
        f.Text = ""EA SPORTS FC 27"";
        f.Size = new Size(380, 190);
        f.FormBorderStyle = FormBorderStyle.FixedSingle;
        f.MaximizeBox = false;
        f.StartPosition = FormStartPosition.CenterScreen;
        Label l = new Label();
        l.Text = ""Simulacao de atividade\nEA SPORTS FC 27 (appid 4080220)\n\nDeixe esta janela aberta.\nFeche para encerrar."";
        l.Dock = DockStyle.Fill;
        l.TextAlign = ContentAlignment.MiddleCenter;
        f.Controls.Add(l);
        Application.Run(f);
    }
}
";
    static string FindCsc() {
        string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] candidatos = {
            Path.Combine(windir, "Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe"),
            Path.Combine(windir, "Microsoft.NET\\Framework\\v4.0.30319\\csc.exe")
        };
        foreach (string c in candidatos) if (File.Exists(c)) return c;
        return null;
    }
    // Compila o stub do FC 27 e o entrega em targetExe. Falso quando csc não
    // existe ou o destino está travado (neste caso o chamador encerra FC27 e tenta de novo).
    static bool CompileFc27Stub(string targetExe) {
        try {
            string csc = FindCsc();
            if (csc == null) return false;
            string src = Path.Combine(Path.GetTempPath(), "fc27_stub.cs");
            string tmp = Path.Combine(Path.GetTempPath(), "fc27_stub.exe");
            File.WriteAllText(src, Fc27StubSource, new System.Text.UTF8Encoding(false));
            ProcessStartInfo psi = new ProcessStartInfo(csc, "/nologo /target:winexe /out:\"" + tmp + "\" /r:System.Windows.Forms.dll /r:System.Drawing.dll \"" + src + "\"");
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi)) {
                if (p == null || !p.WaitForExit(60000)) { try { if (p != null && !p.HasExited) p.Kill(); } catch { } return false; }
            }
            if (!File.Exists(tmp)) return false;
            File.Copy(tmp, targetExe, true);
            try { File.Delete(tmp); File.Delete(src); } catch { }
            return true;
        } catch (IOException) { return false; }
          catch (Exception) { return false; }
    }
    // Reabre a Steam depois dos arquivos já criados, replicando a condição do
    // primeiro teste: a Steam relendo o manifest com tudo no lugar. Roda em
    // segundo plano para não travar a interface.
    static void RestartSteamForFc27() {
        try {
            string steam = SteamRootPath();
            if (steam == null) return;
            string exe = Path.Combine(steam, "steam.exe");
            if (!File.Exists(exe)) return;
            if (Process.GetProcessesByName("steam").Length == 0) return;
            Process.Start(new ProcessStartInfo(exe, "-shutdown") { UseShellExecute = false, CreateNoWindow = true });
            for (int i = 0; i < 30; i++) {
                System.Threading.Thread.Sleep(1000);
                if (Process.GetProcessesByName("steam").Length == 0) break;
            }
            if (Process.GetProcessesByName("steam").Length == 0) Process.Start(exe);
        } catch { }
    }
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length >= 3 && args[0] == "--process") {
            int parentPid = 0; if (args.Length >= 4) int.TryParse(args[3], out parentPid);
            // FC 27: o Discord não tem executável na lista oficial e reconhece a
            // atividade pela janela com o título estilizado do jogo ("EA SPORTS
            // FC 27", em caixa alta). Sessão do FC 27 usa janela nativa simples
            // em vez da barrinha compacta, que não registra no client.
            if (IsFc27Id(args[2])) {
                Form stub = new Form {
                    Text = "EA SPORTS FC 27", Size = new Size(380, 190),
                    FormBorderStyle = FormBorderStyle.FixedSingle, MaximizeBox = false,
                    StartPosition = FormStartPosition.CenterScreen,
                    BackColor = Skin.Background, ForeColor = Skin.Text
                };
                Label texto = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Skin.Text, Font = new Font("Segoe UI", 10f), Text = "Simulação de atividade\nEA SPORTS FC 27 (appid 4080220)\n\nDeixe esta janela aberta.\nFeche para encerrar." };
                stub.Controls.Add(texto);
                // Segunda janela com a grafia estilizada da loja (com o símbolo de
                // marca): cobre as duas formas de nome que o client pode casar.
                Form loja = new Form {
                    Text = "EA SPORTS FC\u2122 27", Size = new Size(380, 110),
                    FormBorderStyle = FormBorderStyle.FixedSingle, MaximizeBox = false,
                    StartPosition = FormStartPosition.Manual,
                    BackColor = Skin.Surface, ForeColor = Skin.Text
                };
                Label sub = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Skin.Muted, Font = new Font("Segoe UI", 9f), Text = "Atividade registrada — deixe esta janela aberta tambem." };
                loja.Controls.Add(sub);
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                loja.SetBounds(area.Right - 404, area.Bottom - 134, 380, 110);
                stub.FormClosed += delegate { try { loja.Close(); } catch { } };
                Timer vigia = new Timer { Interval = 1000 };
                vigia.Tick += delegate {
                    if (parentPid <= 0) return;
                    try { using (Process parent = Process.GetProcessById(parentPid)) { if (parent.HasExited) stub.Close(); } }
                    catch (ArgumentException) { stub.Close(); } catch { }
                };
                vigia.Start();
                loja.Show();
                ApplicationContext contexto = new ApplicationContext { MainForm = stub };
                Application.Run(contexto);
                return;
            }
            if (args.Length >= 5 && args[4] == "--silent") {
                while (true) {
                    try { using (Process parent = Process.GetProcessById(parentPid)) { if (parent.HasExited) return; } }
                    catch { return; }
                    System.Threading.Thread.Sleep(1000);
                }
            }
            DateTime childStart = DateTime.Now;
            DarkWindow f = new DarkWindow { Text = args[1], Size = new Size(240, 48), ShowInTaskbar = true, StartPosition = FormStartPosition.CenterScreen, BackColor = Skin.Background, ForeColor = Skin.Text, Font = new Font("Segoe UI", 9), AutoScaleMode = AutoScaleMode.None };
            // Sessao ultracompacta: uma unica linha, arrastavel e com botao fechar.
            f.MinimumSize = new Size(240, 48);
            f.MaximumSize = new Size(240, 48);
            f.MaximizeBox = false;
            Panel strip = new Panel { Dock = DockStyle.Fill, BackColor = Skin.Background };
            Label gameTitle = new Label { Dock = DockStyle.Fill, Text = args[1], AutoEllipsis = true, ForeColor = Skin.Text, Font = new Font("Segoe UI Semibold", 9f), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
            Label info = new Label { Dock = DockStyle.Right, Width = 65, ForeColor = Skin.Muted, Font = new Font("Segoe UI", 8f), TextAlign = ContentAlignment.MiddleCenter };
            Label close = new Label { Dock = DockStyle.Right, Width = 25, Text = "×", ForeColor = Color.FromArgb(255, 139, 151), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 11f) };
            Label minimize = new Label { Dock = DockStyle.Right, Width = 25, Text = "—", ForeColor = Skin.Muted, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f) };
            minimize.Click += delegate { f.WindowState = FormWindowState.Minimized; };
            minimize.MouseEnter += delegate { minimize.BackColor = Color.FromArgb(38, 43, 54); };
            minimize.MouseLeave += delegate { minimize.BackColor = Skin.Background; };
            close.Click += delegate { f.Close(); };
            close.MouseEnter += delegate { close.BackColor = Color.FromArgb(75, 35, 43); };
            close.MouseLeave += delegate { close.BackColor = Skin.Background; };
            Panel indicator = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Skin.Green };
            strip.Controls.Add(gameTitle); strip.Controls.Add(info); strip.Controls.Add(minimize); strip.Controls.Add(close);
            f.Controls.Add(strip); f.Controls.Add(indicator);
            f.EnableDrag(strip); f.EnableDrag(gameTitle); f.EnableDrag(info);
            Action refreshInfo = delegate { info.Text = "● " + FormatSpan(DateTime.Now - childStart); };
            refreshInfo();
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
            } catch { /* Se a capa ainda nao estiver em cache, mantem icone padrao. */ }
            // A sessao usa a propria barra compacta, sem titulo extra.
            // Janela de atividade normal, com ícone próprio na barra de tarefas.
            Application.Run(f);
            if (sessionIcon != null) sessionIcon.Dispose();
            return;
        }
        CleanupOrphanSessions();
        CleanupStaleFc27Fix();
        Log("App iniciado (Windows " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + " com correção FC 27).");
        Application.Run(new BeakoPresence());
    }
    static Label Title(string text, DockStyle dock, int width) { return new Label { Text = text, Dock = dock, Width = width, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Skin.Text, Font = new Font("Segoe UI Semibold", 15f), Margin = new Padding(0) }; }
    static Label Subtle(DockStyle dock, int width) { return new Label { Dock = dock, Width = width, TextAlign = ContentAlignment.MiddleRight, ForeColor = Skin.Muted, Font = new Font("Segoe UI", 9f), Margin = new Padding(0) }; }
    static void Style(DarkButton b, bool primary) {
        b.Dock = DockStyle.Fill; b.Radius = 10; b.Cursor = Cursors.Hand; b.Font = new Font("Segoe UI Semibold", 10f); b.ForeColor = Skin.Text;
        if (primary) { b.BackColor = Skin.Green; b.ForeColor = Color.FromArgb(6, 32, 22); b.FlatAppearance.MouseOverBackColor = Skin.GreenHover; }
        else { b.OutlineOnly = true; b.BackColor = Skin.Background; }
    }
    static DarkButton WindowControlButton(string symbol, bool isClose) {
        DarkButton button = new DarkButton { Text = symbol, Width = 46, Height = 38, ChromeControl = true,
            BackColor = Skin.Background, ForeColor = isClose ? Color.FromArgb(255, 139, 151) : Skin.Text,
            Cursor = Cursors.Hand, Margin = new Padding(0), Padding = new Padding(0),
            Font = new Font("Segoe UI", 12f, FontStyle.Regular), TabStop = false };
        button.FlatAppearance.MouseOverBackColor = isClose ? Color.FromArgb(145, 35, 48) : Color.FromArgb(39, 46, 59);
        button.FlatAppearance.MouseDownBackColor = button.FlatAppearance.MouseOverBackColor;
        return button;
    }
    public BeakoPresence() {
        Text = "Beako Presence"; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Size = new Size(1160, 760); MinimumSize = new Size(1000, 650); Padding = new Padding(6);
        AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        Directory.CreateDirectory(Root); BackColor = Skin.Background;
        TableLayoutPanel body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Skin.Background, Margin = new Padding(0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        HeaderBar header = new HeaderBar { Dock = DockStyle.Fill, Margin = new Padding(0) };
        status.Dock = DockStyle.Right; status.Width = 150; status.ForeColor = Skin.Muted; status.Font = new Font("Segoe UI", 9f); status.TextAlign = ContentAlignment.MiddleRight; status.Padding = new Padding(0, 0, 16, 0); status.Text = "Pronto"; status.BackColor = Skin.Background;
        // Status interno mantido para operacoes, mas oculto da barra superior.
        status.Visible = false;
        // Uma unica barra: titulo, icone e controles na mesma linha.
        WindowControls windowButtons = new WindowControls { Dock = DockStyle.Right, Margin = new Padding(0) };
        windowButtons.MinimizeAction = delegate { WindowState = FormWindowState.Minimized; };
        Action toggleWindow = delegate { if (WindowState != FormWindowState.Maximized) MaximizedBounds = Screen.FromControl(this).WorkingArea; WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
        windowButtons.MaximizeAction = delegate { toggleWindow(); };
        windowButtons.CloseAction = delegate { Close(); };
        header.Controls.Add(windowButtons); windowButtons.BringToFront();
        EnableDrag(header); header.DoubleClick += delegate { toggleWindow(); };
        body.Controls.Add(header, 0, 0);
        TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Background, Padding = new Padding(20, 16, 20, 12), Margin = new Padding(0) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        // ---- catálogo
        TableLayoutPanel catalog = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Skin.Background, Margin = new Padding(0, 0, 16, 0) };
        catalog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); catalog.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); catalog.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        Panel catTitle = new Panel { Dock = DockStyle.Fill, BackColor = Skin.Background, Margin = new Padding(0) };
        catalogCount = Subtle(DockStyle.Right, 220); catalogCount.BackColor = Skin.Background; Label catLabel = Title("Jogos", DockStyle.Left, 140); catLabel.BackColor = Skin.Background;
        catTitle.Controls.Add(catalogCount); catTitle.Controls.Add(catLabel); catalog.Controls.Add(catTitle, 0, 0);
        FlatPanel searchPanel = new FlatPanel { Dock = DockStyle.Fill, Radius = 5, BackColor = Skin.Card, Margin = new Padding(0, 2, 0, 8), Padding = new Padding(6, 14, 14, 8) };
        Label glass = new Label { Text = "⌕", Dock = DockStyle.Left, Width = 34, TextAlign = ContentAlignment.TopCenter, ForeColor = Skin.Muted, BackColor = Skin.Card, Font = new Font("Segoe UI Symbol", 13f), Padding = new Padding(0, 0, 0, 0) };
        search.AutoSize = false; search.BorderStyle = BorderStyle.None; search.BackColor = Skin.Card; search.ForeColor = Skin.Text; search.Dock = DockStyle.Fill; search.Font = new Font("Segoe UI", 10.5f);
        search.HandleCreated += delegate { SetCue(search.Handle, 0x1501, new IntPtr(1), "Buscar jogos..."); };
        search.Enter += delegate { searchPanel.BorderColor = Skin.Green; searchPanel.Invalidate(); }; search.Leave += delegate { searchPanel.BorderColor = Skin.Border; searchPanel.Invalidate(); };
        searchPanel.Controls.Add(search); searchPanel.Controls.Add(glass); catalog.Controls.Add(searchPanel, 0, 1);
        list.Dock = DockStyle.Fill; list.Cards = false; list.BackColor = Skin.Background; list.Margin = new Padding(0); list.IsActive = delegate(Game g) { return running.ContainsKey(g.Id); }; catalog.Controls.Add(list, 0, 2);
        TableLayoutPanel buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Background, Margin = new Padding(0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        start.Text = "▶  Iniciar atividade"; sync.Text = "↻ Atualizar";
        Style((DarkButton)start, true); Style((DarkButton)sync, false);
        start.Margin = new Padding(0, 8, 8, 0); sync.Margin = new Padding(0, 8, 0, 0);
        buttons.Controls.Add(start, 0, 0); buttons.Controls.Add(sync, 1, 0); catalog.Controls.Add(buttons, 0, 3);
        selectedCount.Text = "Selecione os jogos para iniciar uma atividade"; selectedCount.ForeColor = Skin.Muted; selectedCount.Font = new Font("Segoe UI", 9f); selectedCount.TextAlign = ContentAlignment.MiddleLeft; selectedCount.Dock = DockStyle.Fill; selectedCount.BackColor = Skin.Background; catalog.Controls.Add(selectedCount, 0, 4);
        content.Controls.Add(catalog, 0, 0);
        // ---- atividades
        TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Skin.Background, Margin = new Padding(0) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.Controls.Add(Title("Jogos marcados", DockStyle.Fill, 0), 0, 0);
        FlatPanel markedPanel = new FlatPanel { Dock = DockStyle.Fill, Radius = 5, BackColor = Skin.Card, Padding = new Padding(10), Margin = new Padding(0, 2, 0, 8) };
        TableLayoutPanel markedLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Skin.Card };
        markedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        markedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        markedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        markedLayout.Controls.Add(new Label { Text = "✓ SELECIONADOS", Dock = DockStyle.Fill, ForeColor = Skin.Text, Font = new Font("Segoe UI Semibold", 10f) }, 0, 0);
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
        FlatPanel sessionPanel = new FlatPanel { Dock = DockStyle.Fill, Radius = 5, BackColor = Skin.Card, Padding = new Padding(10), Margin = new Padding(0, 2, 0, 0) };
        TableLayoutPanel sessionLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Skin.Card, Margin = new Padding(0) };
        sessionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); sessionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); sessionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        active.Dock = DockStyle.Fill; active.Compact = true; active.BackColor = Skin.Card; active.Margin = new Padding(0); active.Subtitle = delegate(Game g) { return started.ContainsKey(g.Id) ? "Ativo há " + FormatSpan(DateTime.Now - started[g.Id]) : "Encerrado"; }; sessionLayout.Controls.Add(active, 0, 0);
        discordState.Dock = DockStyle.Fill; discordState.Text = "● Discord: verificando..."; discordState.ForeColor = Skin.Muted; discordState.BackColor = Skin.Card; discordState.Font = new Font("Segoe UI", 9f); discordState.TextAlign = ContentAlignment.MiddleLeft; discordState.Padding = new Padding(6, 0, 0, 0); sessionLayout.Controls.Add(discordState, 0, 1);
        sessionPanel.Controls.Add(sessionLayout); right.Controls.Add(sessionPanel, 0, 3);
        TableLayoutPanel stopButtons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Background, Margin = new Padding(0) }; stopButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); stopButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        stop.Text = "Parar selecionados"; all.Text = "Parar todos"; Style((DarkButton)stop, false); Style((DarkButton)all, false); ((DarkButton)stop).HoverBorder = Skin.Danger; ((DarkButton)all).HoverBorder = Skin.Danger;
        stop.Margin = new Padding(0, 8, 8, 0); all.Margin = new Padding(0, 8, 0, 0); stopButtons.Controls.Add(stop, 0, 0); stopButtons.Controls.Add(all, 1, 0); right.Controls.Add(stopButtons, 0, 4);
        // ---- ferramentas: logs, diagnóstico e atualização online do app
        TableLayoutPanel tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Skin.Background, Margin = new Padding(0) };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        DarkButton logsBtn = new DarkButton(), diagBtn = new DarkButton();
        logsBtn.Text = "Logs"; diagBtn.Text = "Diagnóstico";
        Style((DarkButton)logsBtn, false); Style((DarkButton)diagBtn, false);
        logsBtn.Margin = new Padding(0, 2, 5, 0); diagBtn.Margin = new Padding(5, 2, 0, 0);
        tools.Controls.Add(logsBtn, 0, 0); tools.Controls.Add(diagBtn, 1, 0);
        right.Controls.Add(tools, 0, 5);
        logsBtn.Click += delegate {
            try {
                if (!File.Exists(LogPath)) Log("Log aberto pela primeira vez.");
                Process.Start(new ProcessStartInfo(LogPath) { UseShellExecute = true });
            } catch (Exception ex) { status.Text = "Não foi possível abrir o log: " + ex.Message; }
        };
        diagBtn.Click += delegate {
            using (DarkWindow dialog = new DarkWindow { Text = "Diagnóstico", Size = new Size(640, 500), StartPosition = FormStartPosition.CenterParent, Font = new Font("Segoe UI", 10) }) {
                TableLayoutPanel painel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), RowCount = 2, ColumnCount = 1 };
                painel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); painel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
                TextBox text = new TextBox { Text = BuildDiagnostics(), Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Skin.Background, ForeColor = Color.FromArgb(231, 237, 245), Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9.5f) };
                DarkButton ok = new DarkButton { Text = "Fechar", Dock = DockStyle.Fill, BackColor = Skin.Green, ForeColor = Color.FromArgb(10, 40, 28), FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
                painel.Controls.Add(text, 0, 0); painel.Controls.Add(ok, 0, 1); dialog.Controls.Add(painel); dialog.AddChrome("Diagnóstico", false); dialog.AcceptButton = ok; dialog.ShowDialog(this);
            }
        };
        Func<Task> checkAppUpdate = async delegate {
            try {
                string text = await Download("https://api.github.com/repos/Niltonjuniornzx/Beako-Presence/releases/latest");
                var release = json.DeserializeObject(text) as Dictionary<string, object>;
                string tag = release != null && release.ContainsKey("tag_name") ? Convert.ToString(release["tag_name"]) : "";
                // A tag do release acompanha a versão do app Linux (ex.: v3.7.3),
                // mas o app Windows pode ser mais antigo dentro do mesmo release.
                // Comparamos pela versão do asset "Beako-Presence-Windows-<ver>.zip".
                string oficial = "";
                int pos = text.IndexOf("Beako-Presence-Windows-", StringComparison.Ordinal);
                if (pos >= 0) {
                    int fim = text.IndexOf(".zip", pos, StringComparison.Ordinal);
                    if (fim > pos) oficial = text.Substring(pos + 23, fim - pos - 23);
                }
                Version local = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version, nova;
                if (oficial.Length == 0) status.Text = "Sem release Windows no GitHub (tag " + tag + ").";
                else if (Version.TryParse(oficial, out nova) && nova > local) {
                    status.Text = "Nova versão oficial disponível: " + nova.ToString(3) + ".";
                    Log("Atualização oficial disponível no GitHub: " + nova.ToString(3) + " (local " + local.ToString(3) + ").");
                    ShowNotice("Atualização disponível", "Há uma versão Windows mais recente no GitHub: " + nova.ToString(3) + " (esta cópia é " + local.ToString(3) + ").\n\nA versão oficial não inclui a correção do FC 27 desta cópia. Consulte https://github.com/Niltonjuniornzx/Beako-Presence/releases/latest para baixar e substituir o executável.");
                } else status.Text = "App em dia (" + local.ToString(3) + "; oficial Windows mais recente: " + oficial + ").";
            } catch (Exception ex) { status.Text = "Falha ao verificar atualização: " + ex.Message; Log("Falha ao verificar atualização: " + ex.Message); }
        };
        content.Controls.Add(right, 1, 0); body.Controls.Add(content, 0, 1);
        // Layout responsivo: empilha as secoes quando a janela fica estreita.
        // Em larguras intermediarias, reserva mais espaco para os controles da direita.
        bool stackedLayout = false;
        Action updateResponsiveLayout = delegate {
            int available = content.ClientSize.Width - content.Padding.Horizontal;
            bool stack = available < 750;
            if (stack != stackedLayout) {
                stackedLayout = stack;
                content.SuspendLayout();
                content.Controls.Remove(catalog);
                content.Controls.Remove(right);
                content.ColumnStyles.Clear(); content.RowStyles.Clear();
                if (stack) {
                    content.ColumnCount = 1; content.RowCount = 2;
                    content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    content.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
                    content.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
                    catalog.Margin = new Padding(0, 0, 0, 5);
                    right.Margin = new Padding(0, 5, 0, 0);
                    content.Controls.Add(catalog, 0, 0);
                    content.Controls.Add(right, 0, 1);
                } else {
                    content.ColumnCount = 2; content.RowCount = 1;
                    content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
                    content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
                    content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    catalog.Margin = new Padding(0, 0, 16, 0);
                    right.Margin = new Padding(0);
                    content.Controls.Add(catalog, 0, 0);
                    content.Controls.Add(right, 1, 0);
                }
                content.ResumeLayout(true);
            }
            // Evita titulos fixos ocuparem todo o espaco dos paineis.
            catalogCount.Visible = !stack && available >= 920;
            session.Visible = !stack && available >= 920;
            status.Visible = false;
            if (stack) {
                right.RowStyles[1].Height = Math.Max(90, Math.Min(155, (int)(content.ClientSize.Height * .18)));
                right.RowStyles[4].Height = 44;
            } else {
                right.RowStyles[1].Height = 155;
                right.RowStyles[4].Height = 54;
            }
        };
        content.SizeChanged += delegate { updateResponsiveLayout(); };
        Shown += delegate { updateResponsiveLayout(); };
        Controls.Add(body);
        list.SelectedIndexChanged += delegate {
            int n = list.SelectedItems.Count, estimated = list.SelectedItems.Count(g => g.EstimatedExe);
            selectedCount.Text = n == 0 ? "Selecione os jogos para iniciar uma atividade" : n == 1 ? "1 jogo selecionado" : n + " jogos selecionados";
            if (estimated > 0) selectedCount.Text += " · " + estimated + " sem processo Windows cadastrado";
            if (!restoring) SaveSettings(); RefreshMarked(); UpdateButtons();
        };
        active.SelectedIndexChanged += delegate { UpdateButtons(); };
        search.TextChanged += delegate { Filter(); }; start.Click += delegate { Launch(); }; stop.Click += delegate {
            List<Game> targets = active.SelectedItems.ToList();
            if (targets.Count == 0) targets = list.SelectedItems.Where(g => running.ContainsKey(g.Id)).ToList();
            foreach (Game g in targets) Stop(g.Id); RefreshActive(); status.Text = targets.Count + " sessão(ões) encerrada(s).";
        }; all.Click += delegate { StopAll(); };
        sync.Click += async delegate { await Sync(); };
        timer.Interval = 2500; timer.Tick += delegate {
            bool changed = false;
            foreach (string id in running.Keys.ToArray()) {
                foreach (Process process in running[id].ToArray()) { bool exited = false; try { exited = process.HasExited; } catch { exited = true; } if (exited) { process.Dispose(); running[id].Remove(process); changed = true; } }
                if (running[id].Count == 0) { bool eraFc27 = IsFc27Id(id); running.Remove(id); started.Remove(id); runningNames.Remove(id); changed = true; if (eraFc27) RemoveFc27SteamFix(); }
            }
            if (changed) RefreshActive();
            if (running.Count > 0) active.Invalidate(); UpdateWorkspace(); session.Text = running.Count == 0 ? "Nenhuma sessão ativa" : running.Count == 1 ? "1 sessão ativa" : running.Count + " sessões ativas";
        }; timer.Start(); LoadSettings(); Filter(); FormClosing += delegate { Log("App encerrando; encerrando todas as sessões."); timer.Stop(); SaveSettings(); StopAll(); }; UpdateButtons();
        Shown += async delegate {
            Task updateCheck = checkAppUpdate();
            string cache = Path.Combine(Root, "catalog.json");
            if (!File.Exists(cache) && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalog.json"))) cache = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "catalog.json");
            if (File.Exists(cache)) { try { Parse(File.ReadAllText(cache)); Filter(); status.Text = "Catálogo carregado."; } catch (Exception ex) { status.Text = "Cache inválido: " + ex.Message; selectedCount.Text = status.Text; } }
            // Atualiza sozinho quando não há cache ou ele tem mais de 7 dias.
            if (games.Count == 0 || (DateTime.Now - File.GetLastWriteTime(cache)).TotalDays > 7) await Sync();
            await updateCheck;
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
        object parsed = json.DeserializeObject(text);
        var envelope = parsed as Dictionary<string, object>;
        object[] data = (envelope != null && envelope.ContainsKey("games") ? envelope["games"] : parsed) as object[];
        if (data == null) throw new IOException("Resposta do catálogo inválida.");
        Dictionary<string, object> iconIndex = null;
        try { iconIndex = json.DeserializeObject(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon-index.json"))) as Dictionary<string, object>; } catch { }
        List<Game> next = new List<Game>();
        foreach (object item in data) {
            try {
            var entry = item as Dictionary<string, object>; if (entry == null || !entry.ContainsKey("id") || !entry.ContainsKey("name")) continue;
            string id = Convert.ToString(entry["id"]), gameName = Convert.ToString(entry["name"]);
            if (String.IsNullOrWhiteSpace(id) || !id.All(char.IsDigit) || String.IsNullOrWhiteSpace(gameName)) continue;
            var exes = entry.ContainsKey("executables") ? entry["executables"] as object[] : null;
            var win = (exes ?? new object[0]).OfType<Dictionary<string, object>>().Where(e => e.ContainsKey("os") && new[] { "win32", "windows", "win64" }.Contains(Convert.ToString(e["os"]).ToLowerInvariant()) && e.ContainsKey("name")).OrderBy(e => e.ContainsKey("is_launcher") && Convert.ToBoolean(e["is_launcher"])).ToList();
            string[] executableNames = win.Select(e => Convert.ToString(e["name"])).Where(SafeExe).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            bool estimated = executableNames.Length == 0;
            string iconHash = entry.ContainsKey("icon_hash") ? Convert.ToString(entry["icon_hash"]) : entry.ContainsKey("icon") ? Convert.ToString(entry["icon"]) : iconIndex != null && iconIndex.ContainsKey(id) ? Convert.ToString(iconIndex[id]) : "";
            next.Add(new Game { Id = id, Name = gameName, Exe = executableNames.FirstOrDefault() ?? "", Exes = executableNames, EstimatedExe = estimated, IconHash = iconHash });
            } catch (ArgumentException) { continue; }
              catch (InvalidCastException) { continue; }
              catch (FormatException) { continue; }
        }
        MergeCommunityPaths(next);
        ApplyKnownWindowsExecutables(next);
        if (next.Count == 0) throw new IOException("A resposta não contém jogos Windows válidos.");
        games = next.OrderBy(g => g.Name).ToList();
    }
    static void ApplyKnownWindowsExecutables(List<Game> catalog) {
        // Discord currently publishes FC 27 without Windows executable names.
        // Keep this known process mapping local so a catalog sync does not erase it.
        Game fc27 = catalog.FirstOrDefault(g => g.Id == "1531874756096295054" || NormalizeGameName(g.Name) == "easportsfc27");
        if (fc27 == null) return;
        fc27.Exes = new[] { "EA SPORTS FC 27/FC27.exe" }.Concat(fc27.Exes ?? new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        fc27.Exe = fc27.Exes[0];
        fc27.EstimatedExe = false;
        fc27.CommunityPath = true;
    }
    static string NormalizeGameName(string value) {
        return new string((value ?? "").ToLowerInvariant().Where(Char.IsLetterOrDigit).ToArray());
    }
    void MergeCommunityPaths(List<Game> catalog) {
        try {
            string path = Path.Combine(Root, "community-paths.json");
            if (!File.Exists(path)) path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "community-paths.json");
            if (!File.Exists(path)) return;
            var root = json.DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            object[] entries;
            if (root == null || !root.ContainsKey("entries") || (entries = root["entries"] as object[]) == null) return;
            foreach (object raw in entries) {
                var entry = raw as Dictionary<string, object>; if (entry == null || !entry.ContainsKey("name") || !entry.ContainsKey("path")) continue;
                if (entry.ContainsKey("template") && Convert.ToBoolean(entry["template"])) continue;
                string name = Convert.ToString(entry["name"]), primary = Convert.ToString(entry["path"]);
                List<string> paths = new List<string>();
                if (SafeExe(primary)) paths.Add(primary);
                object[] alternatives = entry.ContainsKey("alts") ? entry["alts"] as object[] : null;
                if (alternatives != null) foreach (object candidate in alternatives) { string alt = Convert.ToString(candidate); if (SafeExe(alt) && !paths.Contains(alt, StringComparer.OrdinalIgnoreCase)) paths.Add(alt); }
                if (String.IsNullOrWhiteSpace(name) || paths.Count == 0) continue;
                string key = NormalizeGameName(name);
                Game existing = catalog.FirstOrDefault(g => NormalizeGameName(g.Name) == key);
                if (existing == null) {
                    catalog.Add(new Game { Id = "community-" + key, Name = name, Exe = paths[0], Exes = paths.ToArray(), EstimatedExe = false, CommunityPath = true });
                } else {
                    existing.Exes = paths.Concat(existing.Exes ?? new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    existing.Exe = existing.Exes[0]; existing.EstimatedExe = false; existing.CommunityPath = true;
                }
            }
        } catch { /* Community supplement is optional; keep the official catalog usable. */ }
    }
    static bool SafeExe(string name) {
        // Dados de terceiros podem conter caracteres que fazem Path.IsPathRooted lancar ArgumentException.
        // Um registro ruim nao deve impedir o carregamento de todo o catalogo.
        try {
            if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || Path.IsPathRooted(name) || name.Contains(":") || name.Contains("\"")) return false;
            string[] parts = name.Replace('\\', '/').Split('/');
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && parts.All(p => p.Length > 0 && p != "." && p != ".." && p.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !p.EndsWith(".") && !p.EndsWith(" "));
        } catch (ArgumentException) { return false; }
          catch (NotSupportedException) { return false; }
    }

    void Filter() {
        list.BeginUpdate(); list.Items.Clear();
        foreach (Game g in games.Where(g => (g.Name + " " + String.Join(" ", g.Exes ?? new[] { g.Exe })).IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)) list.Items.Add(g);
        list.EndUpdate(); catalogCount.Text = games.Count == 0 ? "Catálogo não carregado" : (list.Items.Count == games.Count ? games.Count + " jogos" : list.Items.Count + " de " + games.Count + " jogos");
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
        if (busy) return; busy = true; sync.Enabled = false; status.Text = "Baixando catálogo do Discord...";
        try {
            string text = await Download("https://discord.com/api/v10/applications/detectable");
            try {
                string communityText = await Download("https://raw.githubusercontent.com/Sinnisterly/DiscordQuestCompleter/main/dqc/data/overrides.json");
                var supplement = json.DeserializeObject(communityText) as Dictionary<string, object>;
                if (supplement != null && supplement.ContainsKey("entries") && supplement["entries"] is object[]) {
                    string communityCache = Path.Combine(Root, "community-paths.json");
                    File.WriteAllText(communityCache + ".tmp", communityText); File.Copy(communityCache + ".tmp", communityCache, true); File.Delete(communityCache + ".tmp");
                }
            } catch { /* Use the bundled or previously cached community paths when this source is unavailable. */ }
            Parse(text);
            string cache = Path.Combine(Root, "catalog.json"); File.WriteAllText(cache + ".tmp", text); File.Copy(cache + ".tmp", cache, true); File.Delete(cache + ".tmp");
            Filter(); int known = games.Count(g => !g.EstimatedExe); status.Text = games.Count + " jogos carregados · " + known + " com processo Windows oficial · " + (games.Count - known) + " sem processo cadastrado."; if (list.SelectedItems.Count == 0) selectedCount.Text = status.Text;
            Log("Catálogo sincronizado online: " + games.Count + " jogos, " + known + " com processo Windows.");
        } catch (Exception ex) { status.Text = "Falha no catálogo: " + ex.Message; selectedCount.Text = status.Text; catalogCount.Text = games.Count == 0 ? "Falha ao carregar" : games.Count + " jogos (cache)"; Log("Falha na sincronização do catálogo: " + ex.Message); }
        finally { busy = false; sync.Enabled = true; }
    }
    void Launch() {
        Game[] selected = list.SelectedItems.Cast<Game>().ToArray();
        if (selected.Length == 0) { status.Text = "Marque as caixas dos jogos que deseja abrir."; return; }
        List<string> errors = new List<string>(); int count = 0;
        foreach (Game game in selected) {
            if (game.EstimatedExe) { errors.Add(game.Name + ": a lista detectável do Discord não informa processo Windows para este jogo."); continue; }
            if (running.ContainsKey(game.Id)) {
                if (running[game.Id].Any(p => { try { return !p.HasExited; } catch { return false; } })) continue;
                Stop(game.Id);
            }
            List<Process> children = new List<Process>();
            // FC 27: sessão roda de steamapps\common (método Steam), não da pasta de sessões.
            if (IsFc27(game)) {
                string steamTarget = PrepareFc27SteamFix();
                if (steamTarget != null) {
                    Log("FC 27: estrutura Steam preparada (manifest + pasta em " + steamTarget + ").");
                    try {
                        // Stub idêntico ao do primeiro teste que registrou atividade:
                        // executável minúsculo compilado na hora, sem argumentos e com
                        // janela nativa "EA SPORTS FC 27". Cópia do app é só plano B.
                        if (!CompileFc27Stub(steamTarget)) {
                            Log("FC 27: compilação do stub falhou ou destino travado; encerrando FC27.exe antigo e tentando de novo.");
                            foreach (Process stale in Process.GetProcessesByName("FC27")) { try { stale.Kill(); stale.WaitForExit(2000); } catch { } finally { try { stale.Dispose(); } catch { } } }
                            if (!CompileFc27Stub(steamTarget)) File.Copy(Application.ExecutablePath, steamTarget, true);
                        }
                        Process child = Process.Start(new ProcessStartInfo(steamTarget) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(steamTarget) });
                        if (child == null) throw new IOException("Processo não iniciado.");
                        children.Add(child);
                        Log("FC 27: sessão iniciada (FC27.exe rodando de steamapps); Steam sendo reaberta em segundo plano.");
                        Task.Run((Action)RestartSteamForFc27);
                    } catch (Exception ex) {
                        Log("FC 27: falha ao iniciar sessão — " + ex.Message);
                        errors.Add(game.Name + " (Steam): " + ex.Message + (ex is UnauthorizedAccessException ? " — feche o app e execute como administrador." : ""));
                    }
                    if (children.Count > 0) {
                        running.Add(game.Id, children); started.Add(game.Id, DateTime.Now); runningNames[game.Id] = game.Name; count++;
                        continue;
                    }
                }
                // Sem Steam (ou jogo real instalado): cai no fluxo normal abaixo.
            }
            foreach (string exe in game.Exes ?? new[] { game.Exe }) try {
                string folder = Path.Combine(Root, "sessions", game.Id.Replace(":", "_")); string target = Path.GetFullPath(Path.Combine(folder, exe.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !SafeExe(exe)) throw new IOException("Executável inválido.");
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Application.ExecutablePath, target, true);
                string args = "--process \"" + game.Name.Replace("\"", "'").TrimEnd('\\') + "\" " + game.Id + " " + Process.GetCurrentProcess().Id + (children.Count == 0 ? "" : " --silent");
                Process child = Process.Start(new ProcessStartInfo(target, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target) });
                if (child == null) throw new IOException("Processo não iniciado.");
                children.Add(child);
            } catch (Exception ex) { errors.Add(game.Name + " (" + exe + "): " + ex.Message); }
            if (children.Count > 0) { running.Add(game.Id, children); started.Add(game.Id, DateTime.Now); runningNames[game.Id] = game.Name; count++; }
        }
        RefreshActive();
        bool fc27Rodando = selected.Any(g => IsFc27(g) && running.ContainsKey(g.Id));
        status.Text = count + " jogos simulados com " + running.Values.Sum(p => p.Count) + " nomes de processo. Verifique o Discord." + (fc27Rodando ? " FC 27: Steam reaberta; se a quest não puxar, saia do Discord pela bandeja e reabra." : "");
        if (errors.Count > 0) ShowNotice("Falhas ao iniciar", String.Join("\n", errors));
    }
    void Stop(string id) {
        if (!running.ContainsKey(id)) return;
        foreach (Process child in running[id].ToArray()) {
            try { if (!child.HasExited) { child.Kill(); child.WaitForExit(1500); } }
            catch (Exception ex) { status.Text = "Erro ao parar: " + ex.Message; }
            finally { try { child.Dispose(); } catch { } }
        }
        string nomeSessao = runningNames.ContainsKey(id) ? runningNames[id] : id;
        running.Remove(id); started.Remove(id); runningNames.Remove(id);
        Log("Sessão encerrada: " + nomeSessao + " (" + id + ").");
        if (IsFc27Id(id)) RemoveFc27SteamFix();
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


