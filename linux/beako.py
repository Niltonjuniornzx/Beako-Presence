#!/usr/bin/env python3
"""Beako Presence Linux: interface de jogos e simulação de processos locais."""
from __future__ import annotations

import base64
import struct
import argparse
import queue
import json
import os
import pathlib
import shutil
import signal
import subprocess
import unicodedata
import threading
import tkinter as tk
import urllib.request
from tkinter import messagebox, simpledialog, ttk, font as tkfont

VERSION = "3.7.3"
CONFIG_DIR = pathlib.Path.home() / ".config" / "beako-presence"
APP_DIR = pathlib.Path(__file__).resolve().parent
CACHE = CONFIG_DIR / "catalog.json"
CONFIRMED_CACHE = CONFIG_DIR / "confirmed.json"
SETTINGS = CONFIG_DIR / "settings.json"
BUNDLED_CONFIRMED = APP_DIR / "data" / "confirmed.json"
CATALOG_URL = "https://raw.githubusercontent.com/Niltonjuniornzx/dcgames/main/data/catalog.json"
CONFIRMED_URL = "https://raw.githubusercontent.com/Niltonjuniornzx/dcgames/main/data/confirmed.json"
CATALOG_MIRROR = "https://cdn.jsdelivr.net/gh/Niltonjuniornzx/dcgames@main/data/catalog.json"

# Mesma paleta que Skin no BeakoPresence.cs (Windows).
BG = "#0d0f11"
SURFACE = "#15171b"
CARD = "#191c21"
CARD_HOVER = "#22272e"
CARD_SELECTED = "#142c23"
GREEN = "#29c487"
GREEN_HOVER = "#45d79d"
TEXT = "#eef1f5"
MUTED = "#8c98a8"
BORDER = "#2a2f37"
DANGER = "#f0626e"
DEEP_GREEN = "#08251e"
FONT = "DejaVu Sans"
PAGE_SIZE = 12
MAX_VARIANTS = 6


def load_json(path, default):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, ValueError):
        return default


def store_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(path)


def catalog_filename(value):
    """Keep safe relative executable paths, normalizing Windows separators."""
    if not isinstance(value, str) or not 1 <= len(value) <= 1024:
        return None
    value = value.replace("\\", "/")
    parts = value.split("/")
    if not value.lower().endswith(".exe"):
        return None
    for part in parts:
        if (not part or part in (".", "..") or len(part) > 160
                or part != part.strip() or part.endswith(".")
                or any(ord(c) < 32 or ord(c) == 127 or c in ':*?"<>|[]{}^$' for c in part)):
            return None
    return value


def safe_name(name):
    return catalog_filename(name) is not None


def candidates_from(entry, platform="linux"):
    """Order native Linux/Proton records first; use Windows as fallback only."""
    exe_list = entry.get("executables") or []
    if not isinstance(exe_list, list):
        return [], "indefinido"
    native, fallback = [], []
    for exe in exe_list:
        if isinstance(exe, str):
            name, os_name, verified = exe, "win32", False
        elif isinstance(exe, dict):
            name = exe.get("name")
            os_name = str(exe.get("os") or "win32").lower()
            verified = bool(exe.get("verified"))
        else:
            continue
        name = catalog_filename(name)
        if not name:
            continue
        if os_name in (platform, "proton", "steamplay"):
            native.append((name, verified))
        elif os_name in ("win32", "windows", "win64"):
            fallback.append((name, verified))
    # Prefer tested Linux processes, but keep Windows names as additional
    # alternatives for Proton instead of discarding them entirely.
    native.sort(key=lambda pair: not pair[1])
    fallback.sort(key=lambda pair: not pair[1])
    use = native + fallback
    unique, seen = [], set()
    for name, _verified in use:
        if name.casefold() not in seen:
            unique.append(name)
            seen.add(name.casefold())
    return unique, "Linux / Proton" if native else ("Windows / Proton" if fallback else "indefinido")


def parse_games(catalog, confirmed):
    """Merge the DCGames feed and verified names; no invented executable names."""
    if isinstance(catalog, dict):
        catalog = catalog.get("games", [])
    if not isinstance(catalog, list):
        raise ValueError("Formato inválido no catálogo DCGames")
    if isinstance(confirmed, dict):
        confirmed = confirmed.get("games", [])
    if not isinstance(confirmed, list):
        confirmed = []
    by_name = {}
    for entry in catalog:
        if not isinstance(entry, dict) or not isinstance(entry.get("name"), str):
            continue
        name = entry["name"].strip()
        if not name:
            continue
        exes, platform = candidates_from(entry)
        gid = str(entry.get("id") or "dcgames:" + name.casefold())
        key = name.casefold()
        by_name[key] = {"id": gid, "name": name, "executables": exes, "os": platform, "verified": False}
    for entry in confirmed:
        if not isinstance(entry, dict) or not isinstance(entry.get("name"), str):
            continue
        name = entry["name"].strip()
        if not name:
            continue
        # In confirmed.json only use records explicitly tested for Linux.
        records = [e for e in (entry.get("executables") if isinstance(entry.get("executables"), list) else [])
                   if isinstance(e, dict) and e.get("verified") and
                   str(e.get("os", "")).lower() in ("linux", "proton")]
        names = [e.get("name") for e in records if safe_name(e.get("name"))]
        if not names:
            continue
        key = name.casefold()
        existing = by_name.get(key)
        if existing:
            seen = set()
            ordered = []
            for filename in names + existing["executables"]:
                if filename.casefold() not in seen:
                    ordered.append(filename)
                    seen.add(filename.casefold())
            existing["executables"] = ordered
            existing["os"] = "Linux verificado"
            existing["verified"] = True
        else:
            by_name[key] = {
                "id": "confirmed:" + key, "name": name,
                "executables": list(dict.fromkeys(names)), "os": "Linux verificado", "verified": True
            }
    return sorted(by_name.values(), key=lambda item: item["name"].casefold())


def fetch_icon(gid, cache_dir, *, offline=False):
    """Read bounded PNGs; workers never touch Tk widgets."""
    if not isinstance(gid, str) or not gid.isascii() or not gid.isdigit():
        return None
    def valid(data):
        if len(data) < 24 or len(data) > 3_000_000 or data[:8] != b"\x89PNG\r\n\x1a\n":
            return False
        width, height = struct.unpack(">II", data[16:24])
        return 0 < width <= 512 and 0 < height <= 512
    for path in (APP_DIR / "icons" / (gid + ".png"), cache_dir / (gid + ".png")):
        try:
            with path.open("rb") as stream:
                data = stream.read(3_000_001)
            if valid(data):
                return data
        except OSError:
            pass
    if offline:
        return None
    def download(url, limit):
        req = urllib.request.Request(url, headers={"User-Agent": "BeakoPresence-Linux/" + VERSION})
        with urllib.request.urlopen(req, timeout=8) as response:
            data = response.read(limit + 1)
        if len(data) > limit:
            raise ValueError("Resposta muito grande")
        return data
    record = json.loads(download("https://discord.com/api/v10/applications/" + gid + "/rpc", 262144))
    icon_hash = record.get("icon") if isinstance(record, dict) else None
    if not isinstance(icon_hash, str) or not icon_hash or not all(c in "0123456789abcdefABCDEF_" for c in icon_hash):
        return None
    data = download("https://cdn.discordapp.com/app-icons/" + gid + "/" + icon_hash + ".png?size=64", 3_000_000)
    if not valid(data):
        return None
    try:
        cache_dir.mkdir(parents=True, exist_ok=True)
        target = cache_dir / (gid + ".png")
        tmp = target.with_suffix(".tmp")
        tmp.write_bytes(data)
        tmp.replace(target)
    except OSError:
        pass
    return data


class RoundedPanel(tk.Frame):
    """Rounded corners drawn with Tk only; no additional Linux dependencies."""
    def __init__(self, parent, **kwargs):
        kwargs["highlightthickness"] = 0
        super().__init__(parent, **kwargs)
        self._edges = []
        for opts in ({"x":10,"y":0,"relwidth":1,"width":-20,"height":1}, {"x":10,"rely":1,"y":-1,"relwidth":1,"width":-20,"height":1}, {"x":0,"y":10,"relheight":1,"height":-20,"width":1}, {"relx":1,"x":-1,"y":10,"relheight":1,"height":-20,"width":1}):
            edge = tk.Frame(self, bg=self.cget("highlightbackground"))
            edge.place(**opts)
            self._edges.append(edge)
        self._outer = parent.cget("bg")
        self._corners = []
        for relx, rely, anchor in ((0, 0, "nw"), (1, 0, "ne"), (0, 1, "sw"), (1, 1, "se")):
            c = tk.Canvas(self, width=10, height=10, bg=self._outer, bd=0, highlightthickness=0)
            c.place(relx=relx, rely=rely, anchor=anchor)
            c.bind("<Button-1>", lambda e: self.event_generate("<Button-1>"))
            self._corners.append(c)
        self.bind("<Configure>", lambda e: self.paint_corners())
        self.paint_corners()

    def configure(self, cnf=None, **kwargs):
        result = super().configure(cnf, **kwargs)
        if hasattr(self, "_corners"):
            self.paint_corners()
        return result

    def paint_corners(self):
        state = (self.cget("bg"), self.cget("highlightbackground"))
        if getattr(self, "_paint_state", None) == state:
            return
        self._paint_state = state
        for edge in self._edges:
            edge.configure(bg=self.cget("highlightbackground"))
        for c, (x, y) in zip(self._corners, ((0, 0), (-10, 0), (0, -10), (-10, -10))):
            c.delete("all")
            c.create_oval(x, y, x + 19, y + 19, fill=self.cget("bg"), outline=self.cget("highlightbackground"))
            c.tk.call("raise", c._w)


class RoundedButton(tk.Canvas):
    def __init__(self, parent, text, command, *, primary=False, danger=False, compact=False):
        self.normal = GREEN if primary else SURFACE
        self.hover = GREEN_HOVER if primary else CARD_HOVER
        self.fill = self.normal
        self.command = command
        self.title = text
        self.ink = DEEP_GREEN if primary else DANGER if danger else TEXT
        self.primary = primary
        self.font = (FONT, 9 if compact else 10, "bold")
        super().__init__(parent, height=34 if compact else 42, width=120, bg=parent.cget("bg"), bd=0, highlightthickness=0, takefocus=True, cursor="hand2")
        self.bind("<Configure>", self.paint)
        self.bind("<Enter>", lambda e: self.set_hover(True))
        self.bind("<Leave>", lambda e: self.set_hover(False))
        self.bind("<Button-1>", lambda e: self.focus_set())
        self.bind("<ButtonRelease-1>", lambda e: self.invoke() if 0 <= e.x < self.winfo_width() and 0 <= e.y < self.winfo_height() else None)
        self.bind("<Return>", lambda e: self.invoke())
        self.bind("<space>", lambda e: self.invoke())
        self.bind("<FocusIn>", self.paint)
        self.bind("<FocusOut>", self.paint)

    def invoke(self):
        return self.command()

    def set_hover(self, value):
        self.fill = self.hover if value else self.normal
        self.paint()

    def paint(self, event=None):
        self.delete("all")
        w, h, r = self.winfo_width()-1, self.winfo_height()-1, 10
        if w < 20: return
        points = (r,1,w-r,1,w,1,w,r,w,h-r,w,h,w-r,h,r,h,1,h,1,h-r,1,r,1,1)
        self.create_polygon(points, smooth=True, splinesteps=24, fill=self.fill, outline=GREEN if self.focus_get() == self else self.normal if self.primary else BORDER)
        self.create_text(w/2,h/2,text=self.title,fill=self.ink,font=self.font)


class BeakoApp:
    def __init__(self, root, *, offline=False):
        self.root = root
        self.offline = offline
        self.root.title("Beako Presence — Linux")
        self.root.geometry("1160x760")
        self.root.minsize(1000, 650)
        self.root.configure(bg=BG)
        self._closing = False
        self._syncing = False
        self._events = queue.Queue()
        self._icon_jobs = queue.Queue(maxsize=72)
        self._icon_requested = set()
        self._icon_images = {}
        for _ in range(2):
            threading.Thread(target=self.icon_worker, daemon=True).start()
        self.settings = load_json(SETTINGS, {})
        if not isinstance(self.settings, dict):
            self.settings = {}
        if not isinstance(self.settings.get("marked", []), list):
            self.settings["marked"] = []
        if not isinstance(self.settings.get("executable_names", {}), dict):
            self.settings["executable_names"] = {}
        self.marked = set(map(str, self.settings.get("marked", [])))
        self.multi = tk.BooleanVar(value=bool(self.settings.get("multi_names", True)))
        self.games = []
        self.by_id = {}
        self.filtered = []
        self.limit = PAGE_SIZE
        self.active = {}  # game_id -> list[(executable, Popen)]
        self.mark_ids = []
        self._card_pool = []
        self._empty_label = None
        self.cards = {}
        self.card_labels = {}
        self._rendered_count = 0
        self._loading_more = False
        self._search_task = None
        self._scroll_task = None
        self._columns = 0
        self._avatar_image = None
        self.status = tk.StringVar(value="Carregando catálogo DCGames...")
        self.summary = tk.StringVar(value="0 jogos")
        self.counter = tk.StringVar(value="0 selecionados")
        self.active_counter = tk.StringVar(value="0 ativos")
        self.discord_status = tk.StringVar(value="Discord • confira Jogos registrados")
        self.catalog_filter = tk.StringVar(value="Todos")
        self.query = tk.StringVar()
        self.query.trace_add("write", self.queue_search)
        self.build_ui()
        self.load_catalog()
        if not offline:
            self.root.after(100, self.sync)
        self.root.after(2000, self.poll)
        self.root.after(100, self.drain_events)
        self.root.protocol("WM_DELETE_WINDOW", self.close)

    def label(self, parent, text, *, size=10, color=TEXT, bold=False, bg=None, **kwargs):
        return tk.Label(parent, text=text, bg=bg or parent.cget("bg"), fg=color,
                        font=(FONT, size, "bold" if bold else "normal"), **kwargs)

    def button(self, parent, title, cmd, *, primary=False, danger=False, compact=False):
        return RoundedButton(parent, title, cmd, primary=primary, danger=danger, compact=compact)

    def panel(self, parent):
        panel = RoundedPanel(parent, bg=CARD, highlightbackground=BORDER, highlightthickness=1)
        return panel

    def build_ui(self):
        style = ttk.Style()
        style.theme_use("clam")
        style.configure("TCombobox", fieldbackground=CARD, background=SURFACE, foreground=TEXT, arrowcolor=GREEN, padding=5)
        style.map("TCombobox", fieldbackground=[("readonly", CARD)], foreground=[("readonly", TEXT)])
        style.configure("Beako.Vertical.TScrollbar", gripcount=0, background=CARD_HOVER,
                        troughcolor=BG, arrowcolor=MUTED, bordercolor=BG, lightcolor=BG, darkcolor=BG)

        header = tk.Frame(self.root, bg=SURFACE, height=50)
        header.pack(fill="x", padx=0)
        header.pack_propagate(False)
        icon_file = APP_DIR / "assets" / "avatar.png"
        if icon_file.exists():
            try:
                image = tk.PhotoImage(file=str(icon_file))
                self._window_icon = image
                header_file = APP_DIR / "assets" / "avatar-header.png"
                self._avatar_image = tk.PhotoImage(file=str(header_file)) if header_file.exists() else image.subsample(max(1, image.width() // 32))
                tk.Label(header, image=self._avatar_image, bg=SURFACE).pack(side="left", padx=(20, 10))
                self.root.iconphoto(True, self._window_icon)
            except tk.TclError:
                pass
        self.label(header, "Beako Presence", size=13, bold=True, bg=SURFACE).pack(side="left", padx=4)
        self.label(header, "●  LINUX  ·  v" + VERSION, size=9, color=GREEN, bold=True, bg=SURFACE).pack(side="right", padx=22)
        tk.Frame(self.root, bg=BORDER, height=1).pack(fill="x")

        main = tk.Frame(self.root, bg=BG)
        main.pack(fill="both", expand=True, padx=20, pady=(14, 10))
        main.grid_columnconfigure(0, weight=62, uniform="sections")
        main.grid_columnconfigure(1, weight=38, uniform="sections")
        main.grid_rowconfigure(0, weight=1)
        left = tk.Frame(main, bg=BG)
        right = tk.Frame(main, bg=BG)
        left.grid(row=0, column=0, sticky="nsew", padx=(0, 16))
        right.grid(row=0, column=1, sticky="nsew")
        left.grid_rowconfigure(2, weight=1)
        left.grid_columnconfigure(0, weight=1)
        right.grid_columnconfigure(0, weight=1)
        right.grid_rowconfigure(3, weight=1)

        titlebar = tk.Frame(left, bg=BG, height=37)
        titlebar.grid(row=0, column=0, sticky="ew", pady=(0, 12))
        titlebar.pack_propagate(False)
        self.label(titlebar, "Jogos", size=18, bold=True).pack(side="left")
        self.label(titlebar, "DCGames", size=9, color=GREEN, bold=True).pack(side="right", pady=8)
        self.label(titlebar, "", size=9, color=MUTED, textvariable=self.summary).pack(side="right", padx=(0, 12), pady=8)
        search_outer = RoundedPanel(left, bg=CARD, highlightbackground=BORDER, highlightthickness=1)
        search_outer.grid(row=1, column=0, sticky="ew", pady=(0, 12))
        self.label(search_outer, "⌕", size=17, color=GREEN, bg=CARD).pack(side="left", padx=(13, 8), pady=5)
        self.label(search_outer, "Pesquisar jogo ou .exe", size=9, color=MUTED, bg=CARD).pack(
            side="left", padx=(0, 12))
        entry = tk.Entry(search_outer, textvariable=self.query, bg=CARD, fg=TEXT,
                         insertbackground=TEXT, borderwidth=0, highlightthickness=0,
                         font=(FONT, 11))
        entry.pack(side="left", fill="x", expand=True, ipady=9)
        entry.configure(takefocus=True)
        clear_btn = tk.Button(search_outer, text="×", command=lambda: self.query.set(""),
                              bg=CARD, fg=MUTED, font=(FONT, 14, "bold"),
                              activeforeground=TEXT, activebackground=CARD,
                              bd=0, relief="flat", highlightthickness=0, cursor="hand2")
        clear_btn.pack(side="right", padx=(5, 11))
        entry.bind("<FocusIn>", lambda _event: search_outer.configure(highlightbackground=GREEN))
        entry.bind("<FocusOut>", lambda _event: search_outer.configure(highlightbackground=BORDER))
        entry.bind("<Escape>", lambda _event: self.query.set(""))
        entry.bind("<Down>", lambda _event: self.scroll_with_keyboard(4))
        entry.bind("<Next>", lambda _event: self.scroll_with_keyboard(14))
        self.root.bind("<Control-f>", lambda _event: entry.focus_set())
        self.search_entry = entry

        list_outer = tk.Frame(left, bg=BG)
        list_outer.grid(row=2, column=0, sticky="nsew")
        self.canvas = tk.Canvas(list_outer, bg=BG, bd=0, highlightthickness=0,
                                yscrollincrement=16)
        scroller = ttk.Scrollbar(list_outer, orient="vertical", command=self.scroll_catalog, style="Beako.Vertical.TScrollbar")
        self.canvas.configure(yscrollcommand=scroller.set)
        self.canvas.pack(side="left", fill="both", expand=True)
        scroller.pack(side="right", fill="y")
        self.canvas_inner = tk.Frame(self.canvas, bg=BG)
        self.canvas_window = self.canvas.create_window(0, 0, window=self.canvas_inner, anchor="nw")
        self.canvas_inner.bind("<Configure>", self.adjust_scroll)
        self.canvas.bind("<Configure>", self.on_canvas_resize)
        self.canvas.bind("<MouseWheel>", self.on_mousewheel)
        self.canvas.bind("<Button-4>", self.on_mousewheel)
        self.canvas.bind("<Button-5>", self.on_mousewheel)
        self.canvas.bind("<End>", lambda _event: self.scroll_to_end())
        self.canvas.bind("<Next>", lambda _event: self.scroll_with_keyboard(12))

        actions = tk.Frame(main, bg=BG)
        actions.grid(row=1, column=0, sticky="ew", padx=(0, 16), pady=(10, 0))
        actions.grid_columnconfigure(0, weight=7)
        actions.grid_columnconfigure(1, weight=3)
        self.button(actions, "▶  Iniciar atividade", self.start_simulations, primary=True).grid(
            row=0, column=0, sticky="ew", padx=(0, 9))
        self.button(actions, "↻  Atualizar", self.sync).grid(row=0, column=1, sticky="ew")
        self.label(main, "", size=9, color=MUTED,
                   textvariable=self.counter).grid(row=2, column=0, sticky="w", padx=(0, 16), pady=(8, 0))

        right_title = tk.Frame(right, bg=BG, height=37)
        right_title.grid(row=0, column=0, sticky="ew", pady=(0, 12))
        right_title.pack_propagate(False)
        self.label(right_title, "Jogos marcados", size=18, bold=True).pack(side="left")
        selected_panel = self.panel(right)
        selected_panel.grid(row=1, column=0, sticky="nsew")
        selected_panel.grid_columnconfigure(0, weight=1)
        selected_panel.grid_rowconfigure(1, weight=1)
        selected_panel.configure(height=164)
        selected_panel.grid_propagate(False)
        self.label(selected_panel, "✓  Selecionados", size=10, bold=True, bg=CARD).grid(
            row=0, column=0, sticky="w", padx=16, pady=(12, 8))
        self.markbox = tk.Listbox(selected_panel, bg=CARD, fg=TEXT, relief="flat",
                                  selectbackground=CARD_SELECTED, selectforeground=GREEN,
                                  activestyle="none", highlightthickness=0, bd=0,
                                  font=(FONT, 9), selectmode="extended", exportselection=False,
                                  height=4)
        self.markbox.grid(row=1, column=0, sticky="nsew", padx=16)
        self.markbox.bind("<Double-Button-1>", lambda _event: self.start_simulations())
        selected_actions = tk.Frame(selected_panel, bg=CARD)
        selected_actions.grid(row=2, column=0, sticky="ew", padx=16, pady=(8, 12))
        selected_actions.grid_columnconfigure(0, weight=1, uniform="selected_actions")
        selected_actions.grid_columnconfigure(1, weight=1, uniform="selected_actions")
        self.button(selected_actions, "−  Desmarcar", self.unmark_selected, compact=True).grid(
            row=0, column=0, sticky="ew", padx=(0, 5))
        self.button(selected_actions, "Limpar todos", self.clear_marked, compact=True).grid(
            row=0, column=1, sticky="ew", padx=(5, 0))

        activities = tk.Frame(right, bg=BG)
        activities.grid(row=2, column=0, sticky="ew", pady=(14, 9))
        self.label(activities, "Atividades", size=18, bold=True).pack(side="left")
        self.label(activities, "0 ativos", size=9, color=MUTED, bg=BG,
                   textvariable=self.active_counter).pack(side="right", pady=7)
        active_panel = self.panel(right)
        active_panel.grid(row=3, column=0, sticky="nsew")
        active_panel.grid_columnconfigure(0, weight=1)
        active_panel.grid_rowconfigure(1, weight=1)
        self.label(active_panel, "●  SESSÕES EM EXECUÇÃO", size=9, color=GREEN, bold=True, bg=CARD).grid(
            row=0, column=0, sticky="w", padx=16, pady=(12, 8))
        self.activebox = tk.Listbox(active_panel, bg=CARD, fg=TEXT, relief="flat",
                                    selectbackground=CARD_SELECTED, selectforeground=GREEN,
                                    highlightthickness=0, bd=0, activestyle="none",
                                    font=(FONT, 9), selectmode="extended", exportselection=False, height=3)
        self.activebox.grid(row=1, column=0, sticky="nsew", padx=16)
        self.label(active_panel, "●  Discord: confira Jogos registrados", size=9, color=MUTED,
                   textvariable=self.discord_status, bg=CARD).grid(row=2, column=0, sticky="w", padx=16, pady=(8, 12))

        controls = tk.Frame(main, bg=BG)
        controls.grid(row=1, column=1, sticky="ew", pady=(10, 0))
        controls.grid_columnconfigure(0, weight=1, uniform="controls")
        controls.grid_columnconfigure(1, weight=1, uniform="controls")
        self.button(controls, "Parar selecionados", self.stop_selected).grid(
            row=0, column=0, sticky="ew", padx=(0, 5))
        self.button(controls, "Parar todos", self.stop_all, danger=True).grid(
            row=0, column=1, sticky="ew", padx=(5, 0))
        multi_line = tk.Frame(main, bg=BG)
        multi_line.grid(row=2, column=1, sticky="ew", pady=(8, 0))
        box = tk.Checkbutton(multi_line, variable=self.multi, command=self.toggle_multi,
                             text="Vários nomes da DB em paralelo", bg=BG, fg=TEXT,
                             activebackground=BG, activeforeground=TEXT, selectcolor=SURFACE,
                             font=(FONT, 9), bd=0, highlightthickness=0, cursor="hand2")
        box.pack(side="left")
        self.label(multi_line, "máx. 6", size=8, color=MUTED).pack(side="right")

        footer = tk.Frame(self.root, bg=SURFACE, height=37)
        footer.pack(fill="x")
        footer.pack_propagate(False)
        self.label(footer, "●", size=11, color=GREEN, bg=SURFACE).pack(side="left", padx=(19, 8), pady=8)
        self.label(footer, "", size=9, color=MUTED, bg=SURFACE,
                   textvariable=self.status, anchor="w").pack(side="left", fill="x", expand=True, pady=8)
        self.label(footer, "Beako  •  DCGames", size=8, color=MUTED, bg=SURFACE).pack(side="right", padx=19)
        # Pack the footer before the expanding main panel; it stays visible even when resized.
        main.pack_forget()
        footer.pack_forget()
        footer.pack(side="bottom", fill="x")
        main.pack(side="top", fill="both", expand=True, padx=20, pady=(16, 12))

    def adjust_scroll(self, _event=None):
        self.canvas.configure(scrollregion=self.canvas.bbox("all"))
        self.request_more_if_needed()

    def on_canvas_resize(self, event):
        self.canvas.itemconfigure(self.canvas_window, width=max(event.width, 1))
        # Large horizontal cards, with enough space for full names.
        columns = 1
        if columns != self._columns:
            self._columns = columns
            self.draw_cards(reset=True)

    def scroll_catalog(self, *args):
        self.canvas.yview(*args)
        self.request_more_if_needed()

    def scroll_with_keyboard(self, units):
        self.canvas.yview_scroll(units, "units")
        self.request_more_if_needed()
        return "break"

    def scroll_to_end(self):
        self.canvas.yview_moveto(1)
        self.request_more_if_needed()
        return "break"

    def on_mousewheel(self, event):
        if getattr(event, "num", 0) == 4:
            steps = -4
        elif getattr(event, "num", 0) == 5:
            steps = 4
        else:
            steps = -max(-6, min(6, int(event.delta / 120) * 4))
        self.canvas.yview_scroll(steps, "units")
        self.request_more_if_needed()
        return "break"

    def request_more_if_needed(self):
        if self._scroll_task is None:
            self._scroll_task = self.root.after_idle(self._maybe_show_more)

    def _maybe_show_more(self):
        self._scroll_task = None
        if self._loading_more or self._rendered_count >= len(self.filtered):
            return
        if not self.canvas.winfo_ismapped():
            return
        fraction = self.canvas.yview()
        if not fraction or fraction[1] < 0.88:
            return
        self._loading_more = True
        try:
            self.limit = min(len(self.filtered), self.limit + PAGE_SIZE)
            self.draw_cards(reset=False)
        finally:
            self._loading_more = False

    @staticmethod
    def normalize_search(text):
        text = unicodedata.normalize("NFKD", str(text)).casefold()
        return "".join(char for char in text if not unicodedata.combining(char))

    def queue_search(self, *_args):
        if self._search_task is not None:
            self.root.after_cancel(self._search_task)
        self._search_task = self.root.after(160, self.apply_search)

    def apply_search(self):
        if self._search_task is not None:
            self.root.after_cancel(self._search_task)
            self._search_task = None
        query = self.normalize_search(self.query.get().strip())
        if query:
            terms = query.split()
            self.filtered = [game for game in self.games if all(term in self.search_index[game["id"]] for term in terms)]
        else:
            self.filtered = self.games[:]
        mode = self.catalog_filter.get()
        if mode == "Com executável":
            self.filtered = [g for g in self.filtered if self.executable_names(g)]
        elif mode == "Sem executável":
            self.filtered = [g for g in self.filtered if not self.executable_names(g)]
        elif mode == "Selecionados":
            self.filtered = [g for g in self.filtered if g["id"] in self.marked]
        self.limit = PAGE_SIZE
        total = f"{len(self.filtered):,}".replace(",", ".")
        overall = f"{len(self.games):,}".replace(",", ".")
        self.summary.set(f"{total} de {overall} jogos" if query else f"{total} jogos")
        self.draw_cards(reset=True)
        self.canvas.yview_moveto(0)
        self.refresh_marked()

    def draw_cards(self, *, reset=True):
        if not hasattr(self, "canvas_inner"):
            return
        if reset:
            for child in self._card_pool:
                child.grid_forget()
            if self._empty_label is not None:
                self._empty_label.grid_forget()
            self.cards.clear()
            self.card_labels.clear()
            self._rendered_count = 0
        columns = max(1, self._columns)
        if not self.filtered and reset:
            text = "Nenhum resultado por aqui.\nAltere a busca ou o filtro para explorar a biblioteca."
            if self._empty_label is None:
                self._empty_label = self.label(self.canvas_inner, text, size=11, color=MUTED, bg=BG, justify="center")
            self._empty_label.grid(row=0, column=0, columnspan=columns, pady=72)
            self.root.after_idle(self.adjust_scroll)
            return
        for col in range(columns):
            self.canvas_inner.grid_columnconfigure(col, weight=1, uniform="cards")
        # Append only new entries, never rebuild the entire list during scrolling.
        until = min(self.limit, len(self.filtered))
        for pos in range(self._rendered_count, until):
            game = self.filtered[pos]
            row, col = divmod(pos, columns)
            selected = game["id"] in self.marked
            bg = CARD_SELECTED if selected else CARD
            if pos < len(self._card_pool):
                frame = self._card_pool[pos]
                frame.game_id = game["id"]
                icon, title, info, meta = frame.card_widgets
                icon.configure(image="", width=3, height=2)
                title.full_title = game["name"]
                self.fit_card_title(title)
                meta.configure(text=game["executables"][0] if game["executables"] else "Sem executável compatível")
                self.cards[game["id"]] = frame
                self.card_labels[game["id"]] = frame.card_widgets
                self.update_card_selection(game["id"])
                self.request_icon(game["id"])
                frame.grid(row=row, column=col, sticky="nsew", padx=(0, 8), pady=(0, 8))
                continue
            frame = RoundedPanel(self.canvas_inner, bg=bg,
                             highlightbackground=GREEN if selected else BORDER,
                             highlightthickness=1, cursor="hand2", height=66)
            frame.grid(row=row, column=col, sticky="nsew", padx=(0, 8), pady=(0, 8))
            frame.pack_propagate(False)
            frame.game_id = game["id"]
            self._card_pool.append(frame)
            self.cards[game["id"]] = frame
            icon = tk.Label(frame, text=("✓" if selected else ("".join(
                segment[0] for segment in game["name"].split()[:2]).upper()[:2] or "G")),
                font=(FONT, 11, "bold"), bg="#214938" if selected else self.icon_color(game["id"]),
                fg=TEXT, width=3, height=2)
            icon.pack(side="left", padx=(10, 9), pady=10)
            check = self.label(frame, "☑" if selected else "☐", size=17, color=GREEN if selected else MUTED, bg=bg)
            check.pack(side="right", padx=12)
            frame.selection_indicator = check
            info = tk.Frame(frame, bg=bg)
            info.pack(side="left", fill="both", expand=True, pady=(7, 7), padx=(0, 5))
            title = self.label(info, game["name"], size=10, bold=True, bg=bg,
                               anchor="w", justify="left", height=1, width=1, wraplength=0)
            title.full_title = game["name"]
            title.measure_font = tkfont.Font(root=self.root, font=title.cget("font"))
            title.bind("<Configure>", lambda _e, label=title: self.fit_card_title(label))
            title.pack(fill="x")
            amount = len(game["executables"])
            suffix = "  •  ativo" if game["id"] in self.active else (" · sem executável" if not amount else "")
            detail = game["executables"][0] if amount else "Sem executável compatível"
            meta = self.label(info, detail,
                              size=8, color=MUTED, bg=bg, anchor="w", width=1)
            meta.pack(fill="x", pady=(1, 0))
            frame.card_widgets = (icon, title, info, meta)
            self.card_labels[game["id"]] = frame.card_widgets
            self.request_icon(game["id"])
            for widget in (frame, info, icon, title, meta, check):
                widget.bind("<Button-1>", lambda _ev, f=frame: self.toggle_mark(f.game_id))
                widget.bind("<Enter>", lambda _ev, f=frame: f.configure(highlightbackground=GREEN))
                widget.bind("<Leave>", lambda _ev, f=frame: self.update_card_selection(f.game_id))
                widget.bind("<MouseWheel>", self.on_mousewheel)
                widget.bind("<Button-4>", self.on_mousewheel)
                widget.bind("<Button-5>", self.on_mousewheel)
        self._rendered_count = until
        self.root.after_idle(self.adjust_scroll)

    @staticmethod
    def fit_card_title(label):
        text = " ".join(label.full_title.split())
        available = max(0, label.winfo_width() - 6)
        font = label.measure_font
        if font.measure(text) > available:
            low, high = 0, len(text)
            while low < high:
                middle = (low + high + 1) // 2
                if font.measure(text[:middle] + "…") <= available:
                    low = middle
                else:
                    high = middle - 1
            text = text[:low].rstrip() + "…" if available >= font.measure("…") else ""
        if label.cget("text") != text:
            label.configure(text=text)

    def request_icon(self, gid):
        if gid in self._icon_images:
            self.apply_icon(gid)
            return
        if not gid.isascii() or not gid.isdigit() or gid in self._icon_requested:
            return
        try:
            self._icon_jobs.put_nowait(gid)
            self._icon_requested.add(gid)
        except queue.Full:
            pass

    def icon_worker(self):
        while not self._closing:
            try:
                gid = self._icon_jobs.get(timeout=0.5)
            except queue.Empty:
                continue
            try:
                data = fetch_icon(gid, CONFIG_DIR / "icons", offline=self.offline)
                if data and not self._closing:
                    self._events.put(("icon", (gid, data)))
            except Exception:
                pass  # Missing icons leave the initials visible.
            finally:
                self._icon_jobs.task_done()

    def receive_icon(self, gid, data):
        if self._closing:
            return
        try:
            image = tk.PhotoImage(master=self.root, data=base64.b64encode(data))
            scale = max(1, (max(image.width(), image.height()) + 39) // 40)
            self._icon_images[gid] = image.subsample(scale, scale)
            self.apply_icon(gid)
        except tk.TclError:
            pass

    def apply_icon(self, gid):
        widgets = self.card_labels.get(gid)
        image = self._icon_images.get(gid)
        if widgets and image:
            widgets[0].configure(image=image, text="", width=40, height=40)

    @staticmethod
    def icon_color(gid):
        palette = ("#243e37", "#30324e", "#253d4d", "#45412d", "#493249")
        return palette[sum(map(ord, gid)) % len(palette)]

    def update_card_selection(self, gid):
        frame = self.cards.get(gid)
        widgets = self.card_labels.get(gid)
        if frame is None or widgets is None:
            return
        selected = gid in self.marked
        bg = CARD_SELECTED if selected else CARD
        frame.configure(bg=bg, highlightbackground=GREEN if selected else BORDER)
        icon, title, info, meta = widgets
        icon.configure(text="✓" if selected else "".join(
            word[0] for word in self.by_id[gid]["name"].split()[:2]).upper()[:2] or "G",
                       bg="#214938" if selected else self.icon_color(gid))
        frame.selection_indicator.configure(text="☑" if selected else "☐", fg=GREEN if selected else MUTED, bg=bg)
        for widget in (title, info, meta):
            widget.configure(bg=bg)

    def toggle_mark(self, gid):
        if gid in self.marked:
            self.marked.remove(gid)
        else:
            self.marked.add(gid)
        self.refresh_marked()
        self.update_card_selection(gid)
        self.save()
        if self.catalog_filter.get() == "Selecionados":
            self.apply_search()

    def refresh_marked(self):
        if not hasattr(self, "markbox"):
            return
        lookup = self.by_id
        previous = {self.mark_ids[i] for i in self.markbox.curselection() if i < len(self.mark_ids)}
        self.markbox.delete(0, "end")
        self.mark_ids = sorted((gid for gid in self.marked if gid in lookup),
                               key=lambda gid: lookup[gid]["name"].casefold())
        for gid in self.mark_ids:
            game = lookup[gid]
            live = "  ● ATIVO" if gid in self.active else ""
            self.markbox.insert("end", "✓  " + game["name"] + live)
        for index, gid in enumerate(self.mark_ids):
            if gid in previous:
                self.markbox.selection_set(index)
        if len(self.mark_ids) == 1:
            self.markbox.selection_set(0)
        self.counter.set(f"{len(self.mark_ids)} jogo(s) selecionado(s) • clique nos cartões para marcar")

    def selected_marked_game(self):
        selection = self.markbox.curselection()
        if not selection and len(self.mark_ids) == 1:
            selection = (0,)
        if not selection:
            messagebox.showinfo("Beako Presence", "Selecione um jogo na lista de marcados.", parent=self.root)
            return None
        return self.by_id.get(self.mark_ids[selection[0]])

    def unmark_selected(self):
        ids = [self.mark_ids[i] for i in self.markbox.curselection()]
        if not ids and len(self.mark_ids) == 1:
            ids = self.mark_ids[:]
        for gid in ids:
            self.marked.discard(gid)
        self.refresh_marked()
        for gid in ids:
            self.update_card_selection(gid)
        self.save()

    def clear_marked(self):
        old = tuple(self.marked)
        self.marked.clear()
        self.refresh_marked()
        for gid in old:
            self.update_card_selection(gid)
        self.save()

    def toggle_multi(self):
        self.save()
        self.status.set("Modo múltiplo " + ("ativado" if self.multi.get() else "desativado"))

    def executable_names(self, game):
        names = list(game["executables"])
        override = self.settings.get("executable_names", {}).get(game["id"], "")
        override = override.strip() if isinstance(override, str) else ""
        override = catalog_filename(override)
        if override:
            names = [override] + [name for name in names if name.casefold() != override.casefold()]
        return names[:MAX_VARIANTS] if self.multi.get() else names[:1]

    def configure_executable(self):
        game = self.selected_marked_game()
        if not game:
            return
        default = self.settings.get("executable_names", {}).get(game["id"], "")
        if not default:
            default = next(iter(game["executables"]), "")
        text = simpledialog.askstring(
            "Nome do executável", f"Priorizar este executável em {game['name']}:\n\n"
                                   f"Nomes na DB: {', '.join(game['executables'][:8])}",
            initialvalue=default, parent=self.root)
        if text is None:
            return
        text = catalog_filename(text)
        if not text:
            messagebox.showerror("Executável inválido", "Informe um caminho relativo válido, como win/jogo.exe. Não use / inicial ou .. .", parent=self.root)
            return
        self.settings.setdefault("executable_names", {})[game["id"]] = text
        self.save()
        self.status.set("Nome prioritário atualizado para " + game["name"])

    def start_simulations(self):
        if not self.mark_ids:
            messagebox.showinfo("Beako Presence", "Marque um ou mais jogos no catálogo.", parent=self.root)
            return
        script = APP_DIR / "testar.sh"
        if not script.is_file():
            messagebox.showerror("Instalação incompleta", "Arquivo testar.sh não encontrado. Reinstale o Beako.", parent=self.root)
            return
        opened, failures = 0, []
        for gid in self.mark_ids:
            game = self.by_id[gid]
            if not self.executable_names(game):
                failures.append(game["name"] + ": a DB não informa um executável compatível")
                continue
            current = [item for item in self.active.get(gid, []) if item[1].poll() is None]
            current_names = {name.casefold() for name, _proc in current}
            for name in self.executable_names(game):
                if name.casefold() in current_names:
                    continue
                try:
                    proc = subprocess.Popen(["bash", str(script), "--managed", name],
                                            stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                            stderr=subprocess.DEVNULL, start_new_session=True)
                    # Detect immediate errors (missing /usr/bin/sleep, bad installation, etc.).
                    try:
                        proc.wait(timeout=0.075)
                        failures.append(f"{game['name']}: {name} saiu com erro ({proc.returncode})")
                    except subprocess.TimeoutExpired:
                        current.append((name, proc))
                        current_names.add(name.casefold())
                        opened += 1
                except (OSError, ValueError) as exc:
                    failures.append(f"{game['name']}: {exc}")
            if current:
                self.active[gid] = current
        self.refresh_active()
        self.refresh_marked()
        if opened:
            self.status.set(f"{opened} processo(s) aberto(s) com nomes da DB. Confira o Discord.")
        elif not failures:
            self.status.set("Os processos desses jogos já estão ativos.")
        if failures:
            messagebox.showwarning("Falha ao iniciar processo", "\n".join(failures[:7]), parent=self.root)

    def test_in_terminal(self):
        game = self.selected_marked_game()
        if not game:
            return
        names = self.executable_names(game)
        if not names:
            messagebox.showinfo("Executável ausente", "Este jogo está na DB, mas não possui executável. Configure Nome .exe para iniciar.", parent=self.root)
            return
        name = names[0]
        script = APP_DIR / "testar.sh"
        terminal = next((name for name in ("konsole", "gnome-terminal", "xfce4-terminal", "mate-terminal", "xterm")
                         if shutil.which(name)), None)
        if not terminal:
            messagebox.showwarning("Terminal ausente", "Instale um terminal (gnome-terminal, konsole ou xterm).", parent=self.root)
            return
        args = ["bash", str(script), name]
        if terminal == "konsole":
            command = [terminal, "-e"] + args
        elif terminal == "gnome-terminal":
            command = [terminal, "--"] + args
        elif terminal == "xfce4-terminal":
            command = [terminal, "--disable-server", "-x"] + args
        elif terminal == "mate-terminal":
            command = [terminal, "--disable-factory", "--"] + args
        else:
            command = [terminal, "-T", "Beako: " + game["name"], "-e"] + args
        try:
            subprocess.Popen(command, start_new_session=True)
            self.status.set("Teste manual aberto no terminal: " + name)
        except OSError as exc:
            messagebox.showerror("Erro ao abrir terminal", str(exc), parent=self.root)

    def _end_process(self, proc):
        if proc.poll() is not None:
            return
        try:
            os.killpg(proc.pid, signal.SIGTERM)
            proc.wait(timeout=1.5)
        except (ProcessLookupError, ChildProcessError):
            pass
        except subprocess.TimeoutExpired:
            try:
                os.killpg(proc.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        except OSError:
            pass

    def stop_game(self, gid):
        for _name, proc in self.active.pop(gid, []):
            self._end_process(proc)
        self.refresh_active()
        self.refresh_marked()

    def stop_selected(self):
        positions = self.activebox.curselection()
        if not positions:
            messagebox.showinfo("Beako Presence", "Selecione uma atividade para encerrar.", parent=self.root)
            return
        ids = [self.active_ids[index] for index in positions]
        for gid in ids:
            self.stop_game(gid)
        self.status.set(f"{len(ids)} atividade(s) encerrada(s).")

    def stop_all(self):
        for gid in list(self.active):
            self.stop_game(gid)
        self.status.set("Todas as sessões iniciadas pelo Beako foram encerradas.")

    def refresh_active(self):
        if not hasattr(self, "activebox"):
            return
        preserved = set(self.activebox.get(i).split("  •", 1)[0]
                        for i in self.activebox.curselection())
        self.activebox.delete(0, "end")
        ids = sorted(self.active,
                     key=lambda gid: self.by_id.get(gid, {}).get("name", ""))
        self.active_ids = []
        for gid in ids:
            game = self.by_id.get(gid)
            if not game:
                continue
            size = len(self.active.get(gid, []))
            desc = f"{size} nome(s)"
            item = "●  " + game["name"] + "  •  " + desc
            self.activebox.insert("end", item)
            self.active_ids.append(gid)
            if item.split("  •", 1)[0] in preserved:
                self.activebox.selection_set(len(self.active_ids) - 1)
        count = len(self.active_ids)
        self.active_counter.set(f"{count} ativo{'s' if count != 1 else ''}")

    def poll(self):
        changed = False
        for gid, items in list(self.active.items()):
            active = [item for item in items if item[1].poll() is None]
            if len(active) != len(items):
                changed = True
            if active:
                self.active[gid] = active
            else:
                self.active.pop(gid, None)
        if changed:
            self.refresh_active()
            self.refresh_marked()
        self.root.after(2000, self.poll)

    def load_catalog(self):
        catalog = load_json(CACHE, load_json(APP_DIR / "data" / "catalog.json", {}))
        confirmed = load_json(CONFIRMED_CACHE, load_json(BUNDLED_CONFIRMED, {}))
        self.set_catalog(catalog, confirmed, offline=True)

    def set_catalog(self, catalog, confirmed, *, offline=False):
        try:
            games = parse_games(catalog, confirmed)
        except ValueError as exc:
            self.status.set(str(exc))
            return False
        if not games and not offline:
            self.status.set("DCGames retornou lista vazia; cache preservado.")
            return False
        # Keep running games controllable when a feed removes a record.
        known = {game["id"] for game in games}
        games.extend(self.by_id[gid] for gid in self.active if gid not in known and gid in self.by_id)
        self.games = games
        self.by_id = {game["id"]: game for game in games}
        self.marked.intersection_update(self.by_id)
        self.search_index = {g["id"]: self.normalize_search(g["name"] + " " + " ".join(g["executables"])) for g in games}
        self.apply_search()
        self.refresh_marked()
        self.refresh_active()
        if offline:
            if len(games) < 10:
                self.status.set(f"{len(games)} jogos locais · conectando ao catálogo completo DCGames...")
            else:
                self.status.set(f"{len(games)} jogos disponíveis no catálogo salvo.")
        return True

    def drain_events(self):
        if self._closing:
            return
        try:
            while True:
                kind, payload = self._events.get_nowait()
                if kind == "icon":
                    self.receive_icon(*payload)
                    continue
                self._syncing = False
                if kind == "catalog":
                    self.finish_sync(*payload)
                else:
                    self.status.set("Falha no catálogo online: " + payload + " · tente Atualizar")
        except queue.Empty:
            pass
        self.root.after(100, self.drain_events)

    def sync(self):
        if self._syncing or self._closing:
            return
        self._syncing = True
        self.status.set("Sincronizando jogos pelo DCGames...")
        def worker():
            try:
                def download(url):
                    req = urllib.request.Request(url, headers={"User-Agent": "BeakoPresence-Linux/" + VERSION})
                    with urllib.request.urlopen(req, timeout=20) as response:
                        raw = response.read(16 * 1024 * 1024 + 1)
                        if len(raw) > 16 * 1024 * 1024:
                            raise ValueError("Catálogo excede o limite de 16 MB")
                        return json.loads(raw)
                errors = []
                catalog = None
                for source in (CATALOG_URL, CATALOG_MIRROR):
                    try:
                        candidate = download(source)
                        if not isinstance(candidate, dict) or not isinstance(candidate.get("games"), list):
                            raise ValueError("Resposta sem lista de jogos")
                        if not candidate["games"]:
                            raise ValueError("Lista vazia")
                        catalog = candidate
                        break
                    except Exception as error:
                        errors.append(str(error))
                if catalog is None:
                    raise ConnectionError(" / ".join(errors)[:190])
                try:
                    verified = download(CONFIRMED_URL)
                    if not isinstance(verified, dict):
                        raise ValueError("Confirmados inválidos")
                except Exception:
                    verified = load_json(CONFIRMED_CACHE, load_json(BUNDLED_CONFIRMED, {}))
                self._events.put(("catalog", (catalog, verified)))
            except Exception as exc:
                self._events.put(("error", str(exc)))
        threading.Thread(target=worker, daemon=True).start()

    def finish_sync(self, catalog, confirmed):
        if self.set_catalog(catalog, confirmed):
            try:
                store_json(CACHE, catalog)
                store_json(CONFIRMED_CACHE, confirmed)
                self.status.set(f"Catálogo atualizado: {len(self.games)} jogos disponíveis.")
            except OSError as exc:
                self.status.set("Catálogo atualizado; erro no cache: " + str(exc))

    def save(self):
        self.settings["marked"] = sorted(self.marked)
        self.settings["multi_names"] = bool(self.multi.get())
        try:
            store_json(SETTINGS, self.settings)
        except OSError as exc:
            self.status.set("Não foi possível salvar as configurações: " + str(exc))

    def close(self):
        self._closing = True
        self.stop_all()
        self.save()
        self.root.destroy()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--offline", action="store_true", help="Usar apenas o catálogo local")
    parser.add_argument("--version", action="version", version=VERSION)
    args = parser.parse_args()
    CONFIG_DIR.mkdir(parents=True, exist_ok=True)
    root = tk.Tk()
    BeakoApp(root, offline=args.offline)
    root.mainloop()