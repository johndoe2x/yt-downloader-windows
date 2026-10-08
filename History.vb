Imports System.IO
Imports System.Text.Json
Imports System.Diagnostics

' One finished download, kept across restarts in %APPDATA%\YTDownloaderVB\history.json
Public Class HistoryItem
    Public Property Title As String = ""
    Public Property Url As String = ""
    Public Property FilePath As String = ""
    Public Property Bytes As Long = 0
    Public Property Duration As String = ""
    Public Property FormatLabel As String = ""
    Public Property Resolution As String = ""
    Public Property ThumbFile As String = ""
    Public Property IsAudio As Boolean = False
    Public Property DoneAt As DateTime = DateTime.Now
End Class

' ═══════════════════════════════════════════════════════════════
'  HISTORY TAB
'
'  Every finished download is written to history.json. The History tab
'  swaps the live queue for a searchable, sortable list of those records.
'  Thumbnails are cached to disk at finish time (Instagram thumbnail URLs
'  expire); anything without one gets a frame grabbed by ffmpeg.
' ═══════════════════════════════════════════════════════════════
Partial Class Form1

    Private ReadOnly HistoryFile As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YTDownloaderVB", "history.json")
    Private ReadOnly ThumbCacheDir As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YTDownloaderVB", "thumbs")

    Private Const HistoryPageSize As Integer = 50
    Private ReadOnly MediaExts As String() = {".mp4", ".mkv", ".webm", ".mp3", ".m4a", ".mov"}

    Private _history As New List(Of HistoryItem)
    ' At most two thumbnail jobs at once — an import of 50 files must not launch 50 ffmpegs.
    Private ReadOnly _thumbGate As New Threading.SemaphoreSlim(2)
    Private _histShown As Integer = HistoryPageSize

    Private btnTabHistory As Button
    Private pnlHistory As Panel
    Private pnlHistTop As Panel
    Private txtHistSearch As TextBox
    Private cboHistSort As ComboBox
    Private lblHistCount As Label
    Private btnHistClear As Button
    Private flpHistory As FlowLayoutPanel
    Private pnlHistEmpty As Panel
    Private lblHistEmpty As Label
    Private btnHistImport As Button

    ' ── Storage ────────────────────────────────────────────────────

    Private Sub LoadHistory()
        Try
            If File.Exists(HistoryFile) Then
                Dim list = JsonSerializer.Deserialize(Of List(Of HistoryItem))(File.ReadAllText(HistoryFile))
                If list IsNot Nothing Then _history = list
            End If
        Catch
            ' A broken file must not stop the app — keep a copy so nothing is lost.
            Try
                File.Copy(HistoryFile, HistoryFile & ".broken", True)
            Catch
            End Try
        End Try
    End Sub

    Private Sub SaveHistory()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile))
            Dim tmp = HistoryFile & ".tmp"
            File.WriteAllText(tmp, JsonSerializer.Serialize(_history,
                New JsonSerializerOptions With {.WriteIndented = True}))
            File.Move(tmp, HistoryFile, True)
        Catch
        End Try
    End Sub

    ' Called when a row finishes. Runs on the UI thread (needs the thumbnail image).
    Private Sub AddToHistory(entry As DownloadEntry)
        If Me.InvokeRequired Then
            UI(Sub() AddToHistory(entry))
            Return
        End If
        If entry.FilePath = "" OrElse Not File.Exists(entry.FilePath) Then Return

        Dim item As New HistoryItem With {
            .Title = entry.Title,
            .Url = entry.Url,
            .FilePath = entry.FilePath,
            .Bytes = New FileInfo(entry.FilePath).Length,
            .Duration = entry.Duration,
            .FormatLabel = entry.FormatLabel,
            .Resolution = entry.Resolution,
            .IsAudio = entry.IsAudio,
            .DoneAt = DateTime.Now
        }
        Try
            If entry.PicThumb IsNot Nothing AndAlso entry.PicThumb.Image IsNot Nothing Then
                Directory.CreateDirectory(ThumbCacheDir)
                Dim tf = Path.Combine(ThumbCacheDir, Guid.NewGuid().ToString("N") & ".jpg")
                entry.PicThumb.Image.Save(tf, Drawing.Imaging.ImageFormat.Jpeg)
                item.ThumbFile = tf
            End If
        Catch
        End Try

        ' Same file downloaded again → move it to the top instead of listing it twice
        _history.RemoveAll(Function(h) String.Equals(h.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase))
        _history.Insert(0, item)
        SaveHistory()
        UpdateHistoryTabText()
        If _activeTab = "History" Then BuildHistoryList()
    End Sub

    ' ── Setup (called once from Form1_Load) ────────────────────────

    Private Sub InitHistory()
        LoadHistory()

        btnTabHistory = New Button() With {
            .Location = New Point(212, 0),
            .Size = New Size(110, 34),
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9),
            .Cursor = Cursors.Hand,
            .Tag = "History"
        }
        btnTabHistory.FlatAppearance.BorderSize = 0
        AddHandler btnTabHistory.Click, Sub(s, e) SelectTab("History")
        pnlTabs.Controls.Add(btnTabHistory)
        UpdateHistoryTabText()

        ' ── Toolbar: search · sort · count · clear ──
        pnlHistTop = New Panel() With {.Dock = DockStyle.Top, .Height = 44}
        txtHistSearch = New TextBox() With {
            .Location = New Point(12, 11),
            .Size = New Size(240, 23),
            .Font = New Font("Segoe UI", 9.5),
            .BorderStyle = BorderStyle.FixedSingle,
            .PlaceholderText = "Search history…"
        }
        AddHandler txtHistSearch.TextChanged, Sub(s, e)
            _histShown = HistoryPageSize
            BuildHistoryList()
        End Sub

        Dim lblSort As New Label() With {
            .Text = "Sort",
            .AutoSize = True,
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(266, 14)
        }
        cboHistSort = New ComboBox() With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(298, 10),
            .Width = 130
        }
        cboHistSort.Items.AddRange(New Object() {"Newest first", "Oldest first", "Largest first", "Name A–Z"})
        cboHistSort.SelectedIndex = 0
        AddHandler cboHistSort.SelectedIndexChanged, Sub(s, e) BuildHistoryList()

        lblHistCount = New Label() With {
            .AutoSize = True,
            .Font = New Font("Segoe UI", 8.5),
            .Location = New Point(442, 14)
        }

        btnHistClear = New Button() With {
            .Text = "Clear History",
            .Size = New Size(110, 28),
            .Font = New Font("Segoe UI", 8.5),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .Top = 8
        }
        btnHistClear.FlatAppearance.BorderSize = 1
        Dim ttClear As New ToolTip()
        ttClear.SetToolTip(btnHistClear, "Forget the list — your downloaded files are kept")
        AddHandler btnHistClear.Click, AddressOf ClearHistoryClicked

        pnlHistTop.Controls.AddRange(New Control() {txtHistSearch, lblSort, cboHistSort, lblHistCount, btnHistClear})
        AddHandler pnlHistTop.Resize, Sub(s, e) btnHistClear.Left = pnlHistTop.ClientSize.Width - btnHistClear.Width - 12
        AddHandler pnlHistTop.Paint, Sub(s, e)
            Using pen As New Pen(If(_darkMode, Color.FromArgb(45, 45, 45), Color.FromArgb(225, 225, 225)))
                e.Graphics.DrawLine(pen, 0, pnlHistTop.Height - 1, pnlHistTop.Width, pnlHistTop.Height - 1)
            End Using
        End Sub

        ' ── List ──
        flpHistory = New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = True,
            .Padding = New Padding(0, 4, 0, 4)
        }
        AddHandler flpHistory.Resize, Sub(s, e)
            Dim W = HistoryRowWidth()
            For Each c As Control In flpHistory.Controls
                c.Width = W
            Next
        End Sub

        ' ── Empty state ──
        pnlHistEmpty = New Panel() With {.Dock = DockStyle.Fill, .Visible = False}
        lblHistEmpty = New Label() With {
            .AutoSize = False,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI", 11),
            .Height = 60,
            .Dock = DockStyle.Top
        }
        btnHistImport = New Button() With {
            .Text = "Add videos already in my download folder",
            .AutoSize = True,
            .Font = New Font("Segoe UI", 9),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand,
            .Padding = New Padding(10, 4, 10, 4)
        }
        btnHistImport.FlatAppearance.BorderSize = 1
        AddHandler btnHistImport.Click, AddressOf ImportDownloadFolder
        pnlHistEmpty.Controls.AddRange(New Control() {btnHistImport, lblHistEmpty})
        AddHandler pnlHistEmpty.Resize, Sub(s, e) PositionHistoryEmpty()

        pnlHistory = New Panel() With {.Dock = DockStyle.Fill, .Visible = False}
        pnlHistory.Controls.Add(flpHistory)
        pnlHistory.Controls.Add(pnlHistEmpty)
        pnlHistory.Controls.Add(pnlHistTop)
        pnlQueueOuter.Controls.Add(pnlHistory)
        pnlHistory.BringToFront()
    End Sub

    Private Sub PositionHistoryEmpty()
        lblHistEmpty.Top = Math.Max(10, pnlHistEmpty.ClientSize.Height \ 2 - 70)
        btnHistImport.Left = (pnlHistEmpty.ClientSize.Width - btnHistImport.Width) \ 2
        btnHistImport.Top = lblHistEmpty.Bottom + 6
    End Sub

    Private Sub UpdateHistoryTabText()
        If btnTabHistory Is Nothing Then Return
        btnTabHistory.Text = If(_history.Count > 0, $"History ({_history.Count})", "History")
    End Sub

    ' Show/hide the history view. Called from SelectTab.
    Private Sub ShowHistoryView(show As Boolean)
        If pnlHistory Is Nothing Then Return
        pnlHistory.Visible = show
        btnClearCompleted.Visible = Not show
        If show Then
            flpItems.Visible = False
            pnlEmpty.Visible = False
            pnlHistory.BringToFront()
            ApplyHistoryTheme()
            BuildHistoryList()
        End If
    End Sub

    ' ── List building ──────────────────────────────────────────────

    Private Function HistoryRowWidth() As Integer
        Return Math.Max(flpHistory.ClientSize.Width - 6, 560)
    End Function

    Private Function FilteredHistory() As List(Of HistoryItem)
        Dim q = If(txtHistSearch?.Text, "").Trim()
        Dim list As IEnumerable(Of HistoryItem) = _history
        If q <> "" Then
            list = list.Where(Function(h) h.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                          Path.GetFileName(h.FilePath).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
        End If
        Select Case cboHistSort.SelectedIndex
            Case 1 : list = list.OrderBy(Function(h) h.DoneAt)
            Case 2 : list = list.OrderByDescending(Function(h) h.Bytes)
            Case 3 : list = list.OrderBy(Function(h) h.Title, StringComparer.CurrentCultureIgnoreCase)
            Case Else : list = list.OrderByDescending(Function(h) h.DoneAt)
        End Select
        Return list.ToList()
    End Function

    Private Sub BuildHistoryList()
        If flpHistory Is Nothing Then Return
        Dim list = FilteredHistory()

        Dim total = _history.Sum(Function(h) h.Bytes)
        lblHistCount.Text = If(list.Count = _history.Count,
            $"{_history.Count} downloads  ·  {FormatBytes(total)}",
            $"{list.Count} of {_history.Count} match")

        flpHistory.SuspendLayout()
        Dim old = flpHistory.Controls.Cast(Of Control)().ToList()
        flpHistory.Controls.Clear()
        For Each c In old
            c.Dispose()
        Next

        Dim W = HistoryRowWidth()
        For Each h In list.Take(_histShown)
            flpHistory.Controls.Add(BuildHistoryRow(h, W))
        Next

        If list.Count > _histShown Then
            Dim more As New Button() With {
                .Text = $"Show more  ({list.Count - _histShown} left)",
                .Width = W,
                .Height = 36,
                .FlatStyle = FlatStyle.Flat,
                .Font = New Font("Segoe UI", 9),
                .Cursor = Cursors.Hand,
                .ForeColor = If(_darkMode, Color.FromArgb(200, 200, 200), Color.FromArgb(60, 60, 60)),
                .BackColor = If(_darkMode, Color.FromArgb(36, 36, 36), Color.White),
                .Margin = New Padding(3, 6, 3, 6)
            }
            more.FlatAppearance.BorderSize = 0
            AddHandler more.Click, Sub(s, e)
                _histShown += HistoryPageSize
                BuildHistoryList()
            End Sub
            flpHistory.Controls.Add(more)
        End If
        flpHistory.ResumeLayout()

        Dim empty = list.Count = 0
        pnlHistEmpty.Visible = empty
        flpHistory.Visible = Not empty
        If empty Then
            If _history.Count = 0 Then
                lblHistEmpty.Text = "No downloads yet." & vbCrLf & "Everything you download will be listed here, even after you close the app."
                btnHistImport.Visible = True
            Else
                lblHistEmpty.Text = $"Nothing matches ""{txtHistSearch.Text.Trim()}"""
                btnHistImport.Visible = False
            End If
            PositionHistoryEmpty()
        End If
    End Sub

    '  Row h=84
    '  thumb   x=10  y=10  112x63   duration overlay bottom-right
    '  title   x=134 y=12  w=W-134-212
    '  chips   x=134 y=42  size · type · resolution · date   (or "File missing")
    '  icons   right edge, 32x32 each: play · drag · folder · copy link · remove
    Private Function BuildHistoryRow(h As HistoryItem, W As Integer) As Panel
        Dim dark = _darkMode
        Dim exists = h.FilePath <> "" AndAlso File.Exists(h.FilePath)
        Dim rowBack = If(dark, Color.FromArgb(30, 30, 30), Color.White)

        Dim pnl As New Panel() With {
            .Width = W,
            .Height = 84,
            .BackColor = rowBack,
            .Margin = New Padding(3, 3, 3, 0)
        }
        AddHandler pnl.Paint, AddressOf ItemPanelPaint

        Dim pic As New PictureBox() With {
            .Size = New Size(112, 63),
            .Location = New Point(10, 10),
            .BackColor = Color.FromArgb(20, 20, 20),
            .SizeMode = PictureBoxSizeMode.Zoom
        }
        If h.Duration <> "" Then
            Dim lblDur As New Label() With {
                .Text = h.Duration,
                .AutoSize = True,
                .Font = New Font("Segoe UI", 7.5, FontStyle.Bold),
                .ForeColor = Color.White,
                .BackColor = Color.Black,
                .Padding = New Padding(2, 0, 2, 0)
            }
            pic.Controls.Add(lblDur)
            AddHandler lblDur.SizeChanged, Sub(s, e) lblDur.Location = New Point(pic.Width - lblDur.Width - 3, pic.Height - lblDur.Height - 3)
            lblDur.Location = New Point(pic.Width - lblDur.Width - 3, pic.Height - lblDur.Height - 3)
            WireFileDrag(lblDur, Function() h.FilePath)
        End If
        LoadHistoryThumbAsync(h, pic)

        Dim lblTitle As New Label() With {
            .Text = h.Title,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = If(dark, Color.FromArgb(235, 235, 235), Color.FromArgb(18, 18, 18)),
            .AutoEllipsis = True,
            .Location = New Point(134, 12),
            .Size = New Size(W - 134 - 212, 22)
        }

        ' ── Chips ──
        Dim chips As New FlowLayoutPanel() With {
            .Location = New Point(134, 42),
            .Size = New Size(W - 134 - 212, 24),
            .WrapContents = False,
            .BackColor = rowBack,
            .Margin = Padding.Empty
        }
        If exists Then
            chips.Controls.Add(MakeChip(FormatBytes(h.Bytes), dark))
            Dim ext = Path.GetExtension(h.FilePath).TrimStart("."c).ToUpper()
            If ext <> "" Then chips.Controls.Add(MakeChip(ext, dark))
            If h.Resolution <> "" AndAlso Not h.IsAudio Then chips.Controls.Add(MakeChip(h.Resolution.Replace("x", "×"), dark))
        Else
            Dim missing = MakeChip("File missing", dark)
            missing.BackColor = If(dark, Color.FromArgb(80, 30, 30), Color.FromArgb(253, 226, 226))
            missing.ForeColor = If(dark, Color.FromArgb(255, 150, 150), Color.FromArgb(170, 20, 20))
            chips.Controls.Add(missing)
        End If
        Dim lblDate As New Label() With {
            .Text = FriendlyDate(h.DoneAt),
            .AutoSize = True,
            .Font = New Font("Segoe UI", 8.5),
            .ForeColor = If(dark, Color.FromArgb(130, 130, 130), Color.FromArgb(130, 130, 130)),
            .Margin = New Padding(6, 4, 0, 0)
        }
        chips.Controls.Add(lblDate)

        ' ── Icons (right to left) ──
        Dim btnRemove = MakeHistoryIcon(&HE711, "Remove from history (keeps the file)", Color.FromArgb(70, 30, 30), Color.FromArgb(250, 220, 220))
        AddHandler btnRemove.MouseEnter, Sub(s, e) btnRemove.ForeColor = Color.FromArgb(255, 90, 90)
        AddHandler btnRemove.Click, Sub(s, e) RemoveHistoryItem(h)

        Dim btnLink = MakeHistoryIcon(&HE71B, "Copy link", Color.FromArgb(35, 55, 80), Color.FromArgb(220, 235, 255))
        btnLink.Visible = h.Url <> ""
        AddHandler btnLink.Click, Sub(s, e)
            Clipboard.SetText(h.Url)
            FlashCopyButton(btnLink, ChrW(&HE71B).ToString())
        End Sub

        Dim btnFolder = MakeHistoryIcon(&HE838, "Show in folder", Color.FromArgb(40, 70, 40), Color.FromArgb(225, 245, 225))
        AddHandler btnFolder.Click, Sub(s, e) ShowHistoryInFolder(h)

        Dim btnDrag = MakeHistoryIcon(&HE759, "Drag the video into Telegram, WhatsApp, a folder…", Color.FromArgb(35, 55, 80), Color.FromArgb(220, 235, 255))
        btnDrag.Cursor = Cursors.SizeAll
        btnDrag.Visible = exists
        AddHandler btnDrag.Click, Sub(s, e) SetStatus("Hold the ✥ button and drag it into Telegram, WhatsApp or a folder")

        ' File gone → the play slot becomes "download again"
        Dim btnPlay As Button
        If exists Then
            btnPlay = MakeHistoryIcon(&HE768, "Play", Color.FromArgb(40, 70, 40), Color.FromArgb(225, 245, 225))
            AddHandler btnPlay.Click, Sub(s, e) PlayHistoryItem(h)
        Else
            btnPlay = MakeHistoryIcon(&HE896, "Download again (uses the quality picked above)", Color.FromArgb(40, 70, 40), Color.FromArgb(225, 245, 225))
            btnPlay.Visible = h.Url <> ""
            AddHandler btnPlay.Click, Sub(s, e) DownloadHistoryAgain(h)
        End If

        Dim icons = {btnPlay, btnDrag, btnFolder, btnLink, btnRemove}
        Dim placeIcons = Sub(width As Integer)
            Dim x = width - 12
            For i = icons.Length - 1 To 0 Step -1
                x -= 36
                icons(i).Location = New Point(x + 4, 24)
            Next
            lblTitle.Width = width - 134 - 212
            chips.Width = width - 134 - 212
        End Sub
        placeIcons(W)
        AddHandler pnl.Resize, Sub(s, e) placeIcons(pnl.Width)

        ' Grab the thumbnail or title to drag the file out, too; click the thumbnail to play
        For Each c As Control In {btnDrag, pic, lblTitle}
            WireFileDrag(c, Function() h.FilePath)
        Next
        If exists Then
            pic.Cursor = Cursors.Hand
            AddHandler pic.Click, Sub(s, e) PlayHistoryItem(h)
        End If

        Dim ctx = BuildHistoryMenu(h)
        For Each c As Control In {pnl, pic, lblTitle, chips}
            c.ContextMenuStrip = ctx
        Next

        pnl.Controls.Add(pic)
        pnl.Controls.Add(lblTitle)
        pnl.Controls.Add(chips)
        pnl.Controls.AddRange(icons)
        Return pnl
    End Function

    Private Function MakeChip(text As String, dark As Boolean) As Label
        Return New Label() With {
            .Text = text,
            .AutoSize = True,
            .Font = New Font("Segoe UI", 8.25, FontStyle.Bold),
            .ForeColor = If(dark, Color.FromArgb(175, 195, 220), Color.FromArgb(50, 80, 120)),
            .BackColor = If(dark, Color.FromArgb(44, 56, 72), Color.FromArgb(228, 237, 248)),
            .Padding = New Padding(5, 2, 5, 2),
            .Margin = New Padding(0, 0, 6, 0)
        }
    End Function

    Private Function MakeHistoryIcon(glyph As Integer, tip As String, hoverDark As Color, hoverLight As Color) As Button
        Dim b As New Button() With {
            .Text = ChrW(glyph).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = IconRestColor(),
            .BackColor = If(_darkMode, Color.FromArgb(30, 30, 30), Color.White),
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(32, 32),
            .Cursor = Cursors.Hand
        }
        b.FlatAppearance.BorderSize = 0
        b.FlatAppearance.MouseOverBackColor = If(_darkMode, hoverDark, hoverLight)
        Dim tt As New ToolTip()
        tt.SetToolTip(b, tip)
        WireIconHover(b)
        Return b
    End Function

    Private Function FriendlyDate(d As DateTime) As String
        Dim today = DateTime.Today
        If d.Date = today Then Return $"Today {d:HH:mm}"
        If d.Date = today.AddDays(-1) Then Return $"Yesterday {d:HH:mm}"
        If d.Year = today.Year Then Return d.ToString("d MMM, HH:mm")
        Return d.ToString("d MMM yyyy")
    End Function

    Private Function BuildHistoryMenu(h As HistoryItem) As ContextMenuStrip
        Dim ctx As New ContextMenuStrip()
        Dim iPlay As New ToolStripMenuItem("Play")
        AddHandler iPlay.Click, Sub(s, e) PlayHistoryItem(h)
        Dim iFolder As New ToolStripMenuItem("Show in folder")
        AddHandler iFolder.Click, Sub(s, e) ShowHistoryInFolder(h)
        Dim iPath As New ToolStripMenuItem("Copy file path")
        AddHandler iPath.Click, Sub(s, e) Clipboard.SetText(h.FilePath)
        Dim iUrl As New ToolStripMenuItem("Copy link")
        AddHandler iUrl.Click, Sub(s, e) Clipboard.SetText(h.Url)
        Dim iAgain As New ToolStripMenuItem("Download again")
        AddHandler iAgain.Click, Sub(s, e) DownloadHistoryAgain(h)
        Dim iRemove As New ToolStripMenuItem("Remove from history") With {.ForeColor = Color.FromArgb(180, 0, 0)}
        AddHandler iRemove.Click, Sub(s, e) RemoveHistoryItem(h)
        ctx.Items.AddRange(New ToolStripItem() {iPlay, iFolder, iPath, iUrl, iAgain, New ToolStripSeparator(), iRemove})
        AddHandler ctx.Opening, Sub(s, e)
            Dim exists = File.Exists(h.FilePath)
            iPlay.Enabled = exists
            iPath.Enabled = h.FilePath <> ""
            iUrl.Enabled = h.Url <> ""
            iAgain.Enabled = h.Url <> ""
        End Sub
        Return ctx
    End Function

    ' ── Actions ────────────────────────────────────────────────────

    Private Sub PlayHistoryItem(h As HistoryItem)
        If File.Exists(h.FilePath) Then
            Process.Start(New ProcessStartInfo(h.FilePath) With {.UseShellExecute = True})
        Else
            SetStatus("That file was moved or deleted")
            BuildHistoryList()
        End If
    End Sub

    Private Sub ShowHistoryInFolder(h As HistoryItem)
        If File.Exists(h.FilePath) Then
            Process.Start("explorer.exe", $"/select,""{h.FilePath}""")
        Else
            Dim dir = Path.GetDirectoryName(h.FilePath)
            If dir <> "" AndAlso Directory.Exists(dir) Then Process.Start("explorer.exe", $"""{dir}""")
            SetStatus("That file was moved or deleted — opened its folder instead")
        End If
    End Sub

    Private Sub DownloadHistoryAgain(h As HistoryItem)
        If h.Url = "" Then Return
        If Not File.Exists(YtDlpPath) Then
            SetStatus("yt-dlp is not ready yet")
            Return
        End If
        EnqueueUrl(h.Url)
        SelectTab("All")
        SetStatus($"Downloading again: {h.Title}")
    End Sub

    Private Sub RemoveHistoryItem(h As HistoryItem)
        _history.Remove(h)
        DeleteThumb(h)
        SaveHistory()
        UpdateHistoryTabText()
        BuildHistoryList()
        SetStatus("Removed from history — the file itself was not touched")
    End Sub

    Private Sub ClearHistoryClicked(sender As Object, e As EventArgs)
        If _history.Count = 0 Then Return
        If MessageBox.Show($"Forget all {_history.Count} downloads in History?" & vbCrLf & vbCrLf &
                           "Your video and audio files stay where they are.",
                           "Clear History", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) <> DialogResult.OK Then Return
        For Each h In _history
            DeleteThumb(h)
        Next
        _history.Clear()
        SaveHistory()
        UpdateHistoryTabText()
        BuildHistoryList()
    End Sub

    Private Sub DeleteThumb(h As HistoryItem)
        Try
            If h.ThumbFile <> "" AndAlso h.ThumbFile.StartsWith(ThumbCacheDir, StringComparison.OrdinalIgnoreCase) Then
                File.Delete(h.ThumbFile)
            End If
        Catch
        End Try
    End Sub

    ' Seed history from what's already sitting in the download folder (opt-in, from the empty state).
    Private Sub ImportDownloadFolder(sender As Object, e As EventArgs)
        Try
            If Not Directory.Exists(_downloadPath) Then Return
            Dim known = New HashSet(Of String)(_history.Select(Function(h) h.FilePath), StringComparer.OrdinalIgnoreCase)
            Dim added = 0
            For Each f In New DirectoryInfo(_downloadPath).GetFiles()
                If Not MediaExts.Contains(f.Extension.ToLower()) Then Continue For
                If known.Contains(f.FullName) Then Continue For
                Dim isAudio = f.Extension.ToLower() = ".mp3" OrElse f.Extension.ToLower() = ".m4a"
                _history.Add(New HistoryItem With {
                    .Title = Path.GetFileNameWithoutExtension(f.Name),
                    .FilePath = f.FullName,
                    .Bytes = f.Length,
                    .IsAudio = isAudio,
                    .FormatLabel = If(isAudio, "Audio", ""),
                    .DoneAt = f.LastWriteTime
                })
                added += 1
            Next
            SaveHistory()
            UpdateHistoryTabText()
            BuildHistoryList()
            SetStatus(If(added = 0, "No video or audio files found in the download folder",
                                    $"Added {added} file(s) from {_downloadPath}"))
        Catch ex As Exception
            SetStatus("Couldn't read the download folder: " & ex.Message)
        End Try
    End Sub

    ' ── Thumbnails ─────────────────────────────────────────────────
    ' Order: cached file → YouTube's own thumbnail → a frame grabbed by ffmpeg.
    ' Whatever is found is cached, so each item only does this once.

    Private Async Sub LoadHistoryThumbAsync(h As HistoryItem, pic As PictureBox)
        Try
            If h.ThumbFile = "" OrElse Not File.Exists(h.ThumbFile) Then
                Await _thumbGate.WaitAsync()
                Try
                    If pic.IsDisposed Then Return
                    Dim made = Await MakeHistoryThumbAsync(h)
                    If made = "" Then Return
                    h.ThumbFile = made
                Finally
                    _thumbGate.Release()
                End Try
                SaveHistory()
            End If
            Dim bytes = Await File.ReadAllBytesAsync(h.ThumbFile)
            Dim img As Image
            Using ms As New MemoryStream(bytes), src = Image.FromStream(ms)
                img = New Bitmap(src)
            End Using
            If pic.IsDisposed Then Return
            pic.Image = img
            pic.BackColor = Color.Black
        Catch
        End Try
    End Sub

    Private Async Function MakeHistoryThumbAsync(h As HistoryItem) As Task(Of String)
        Directory.CreateDirectory(ThumbCacheDir)
        Dim target = Path.Combine(ThumbCacheDir, Guid.NewGuid().ToString("N") & ".jpg")

        Dim vid = ExtractVideoId(h.Url)
        If vid <> "" Then
            Try
                Dim bytes = Await _http.GetByteArrayAsync($"https://img.youtube.com/vi/{vid}/mqdefault.jpg")
                Await File.WriteAllBytesAsync(target, bytes)
                Return target
            Catch
            End Try
        End If

        If Not h.IsAudio AndAlso File.Exists(h.FilePath) AndAlso File.Exists(FfmpegPath) Then
            Try
                Dim psi As New ProcessStartInfo(FfmpegPath,
                    $"-v quiet -y -ss 1 -i ""{h.FilePath}"" -frames:v 1 -vf scale=224:-2 ""{target}""") With {
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }
                Using p = Process.Start(psi)
                    Await p.WaitForExitAsync()
                End Using
                If File.Exists(target) Then Return target
            Catch
            End Try
        End If
        Return ""
    End Function

    ' ── Theme ──────────────────────────────────────────────────────

    Private Sub ApplyHistoryTheme()
        If pnlHistory Is Nothing Then Return
        Dim dark = _darkMode
        Dim listBack = If(dark, Color.FromArgb(28, 28, 28), Color.FromArgb(246, 246, 246))
        Dim barBack = If(dark, Color.FromArgb(24, 24, 24), Color.White)
        Dim text = If(dark, Color.FromArgb(200, 200, 200), Color.FromArgb(60, 60, 60))
        pnlHistory.BackColor = listBack
        flpHistory.BackColor = listBack
        pnlHistEmpty.BackColor = listBack
        lblHistEmpty.ForeColor = If(dark, Color.FromArgb(110, 110, 110), Color.FromArgb(150, 150, 150))
        pnlHistTop.BackColor = barBack
        For Each c As Control In pnlHistTop.Controls
            c.ForeColor = text
        Next
        lblHistCount.ForeColor = If(dark, Color.FromArgb(130, 130, 130), Color.FromArgb(120, 120, 120))
        txtHistSearch.BackColor = If(dark, Color.FromArgb(40, 40, 40), Color.White)
        txtHistSearch.ForeColor = If(dark, Color.White, Color.FromArgb(30, 30, 30))
        cboHistSort.BackColor = txtHistSearch.BackColor
        cboHistSort.ForeColor = txtHistSearch.ForeColor
        For Each b In {btnHistClear, btnHistImport}
            b.BackColor = If(dark, Color.FromArgb(50, 50, 50), Color.FromArgb(240, 240, 240))
            b.ForeColor = If(dark, Color.FromArgb(180, 180, 180), Color.FromArgb(80, 80, 80))
            b.FlatAppearance.BorderColor = If(dark, Color.FromArgb(70, 70, 70), Color.FromArgb(210, 210, 210))
            b.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(65, 65, 65), Color.FromArgb(220, 220, 220))
        Next
        btnTabHistory.BackColor = If(dark, Color.FromArgb(22, 22, 22), Color.White)
    End Sub

End Class
