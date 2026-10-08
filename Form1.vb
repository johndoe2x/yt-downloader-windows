Imports System.IO
Imports System.IO.Compression
Imports System.Net.Http
Imports System.Diagnostics
Imports System.Text.RegularExpressions
Imports System.Threading

Public Class Form1

    ' ── Paths ──────────────────────────────────────────────────────
    Private ReadOnly YtDlpPath As String = Path.Combine(Application.StartupPath, "yt-dlp.exe")
    Private ReadOnly FfmpegPath As String = Path.Combine(Application.StartupPath, "ffmpeg.exe")
    Private ReadOnly DenoPath As String = Path.Combine(Application.StartupPath, "deno.exe")
    Private ReadOnly YtDlpUrl As String = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
    Private ReadOnly FfmpegZipUrl As String = "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-lgpl.zip"
    Private ReadOnly DenoZipUrl As String = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip"
    Private ReadOnly SettingsFile As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YTDownloaderVB", "settings.ini")

    ' ── Settings ───────────────────────────────────────────────────
    Private _downloadPath As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
    Private _maxParallel As Integer = 3
    Private _autoUpdate As Boolean = True

    ' ── Feature 7: Dark mode + Feature 4: Notify + Feature 5: Clipboard ──
    Private _darkMode As Boolean = False
    Private _notifyOnComplete As Boolean = True
    Private _clipboardMonitor As Boolean = False
    Private _cookiesBrowser As String = ""
    Private _cookiesFile As String = ""

    ' ── Feature 5: Clipboard monitor fields ────────────────────────
    Private _clipboardTimer As System.Windows.Forms.Timer = Nothing
    Private _lastClipboardText As String = ""

    ' ── State ──────────────────────────────────────────────────────
    Private _items As New List(Of DownloadEntry)
    Private _activeTab As String = "All"
    Private _ffmpegReady As Boolean = False
    Private _denoReady As Boolean = False
    Private _semaphore As SemaphoreSlim

    ' ── Shared HTTP client ─────────────────────────────────────────
    Private Shared ReadOnly _http As New HttpClient()

    ' ═══════════════════════════════════════════════════════════════
    '  DATA MODEL
    ' ═══════════════════════════════════════════════════════════════

    Private Class DownloadEntry
        Public Id As Guid = Guid.NewGuid()
        Public Url As String = ""
        Public Title As String = "Fetching info…"
        Public Channel As String = ""
        Public Duration As String = ""
        Public FileSize As String = ""
        Public FormatIndex As Integer = 2
        Public FormatLabel As String = "720p"
        Public IsAudio As Boolean = False
        Public FilePath As String = ""
        Public Process As Process = Nothing
        Public IsRunning As Boolean = False
        Public IsDone As Boolean = False
        Public AlreadyExisted As Boolean = False
        ' Feature 1: Speed + ETA
        Public SpeedEta As String = ""
        ' Feature 3: Retry
        Public BtnRetry As Button = Nothing
        Public HasFailed As Boolean = False
        Public HasCookieError As Boolean = False
        ' Actual resolution yt-dlp ended up writing (from --print after_move)
        Public Resolution As String = ""
        Public StartedUtc As DateTime = DateTime.UtcNow
        ' Last ERROR line yt-dlp printed. Without this the row only ever said
        ' "Error (exit 1)", which says nothing about what went wrong.
        Public LastError As String = ""
        ' Instagram has no fixed thumbnail URL pattern — yt-dlp hands us one.
        Public ThumbUrl As String = ""
        ' False until the title/duration fetch has run — bulk rows wait in the
        ' queue without it, and Retry on a never-started row must fetch it first.
        Public InfoFetched As Boolean = False
        Public ReadOnly Property IsInstagram As Boolean
            Get
                Return IsInstagramUrl(Url)
            End Get
        End Property
        ' UI controls
        Public Panel As Panel = Nothing
        Public PicThumb As PictureBox = Nothing
        Public LblTitle As Label = Nothing
        Public LblInfo As Label = Nothing
        Public PbProgress As ProgressBar = Nothing
        Public LblStatus As Label = Nothing
        Public BtnFolder As Button = Nothing
        Public BtnRemove As Button = Nothing
        Public BtnCopyUrl As Button = Nothing
        Public BtnCopyPath As Button = Nothing
        Public BtnDrag As Button = Nothing
        Public BtnPlay As Button = Nothing
    End Class

    ' ═══════════════════════════════════════════════════════════════
    '  STARTUP
    ' ═══════════════════════════════════════════════════════════════

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("YTDownloaderVB/3.0")
        LoadSettings()
        SetupBulkStrip()
        InitHistory()
        StartUiPump()
        RebuildSemaphore()
        UpdateParallelLabel()

        Await EnsureYtDlpAsync()
        Await EnsureFfmpegAsync()
        Await EnsureDenoAsync()

        If _autoUpdate Then
            Await CheckForUpdatesAsync()
        End If

        ' Feature 5: start clipboard monitor
        ConfigureClipboardTimer()
        ' Feature 7: apply theme on load
        ApplyTheme()
    End Sub

    Private Sub Form1_Shown(sender As Object, e As EventArgs) Handles MyBase.Shown
        RepositionHeaderControls()
        PositionEmptyState()
        ' Feature 2: position clear completed button
        RepositionTabControls()
        CheckCookiesFile()
    End Sub

    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles MyBase.Resize
        RepositionHeaderControls()
        PositionEmptyState()
        RefreshItemWidths()
        ' Feature 2: reposition clear completed button
        RepositionTabControls()
    End Sub

    Private Sub RepositionHeaderControls()
        ' Right-align settings button inside header — manual because Anchor
        ' can't compute correct offset during SuspendLayout/ResumeLayout(False)
        btnSettings.Left = pnlHeader.ClientSize.Width - btnSettings.Width - 12
        btnSettings.Top = (pnlHeader.Height - btnSettings.Height) \ 2
    End Sub

    ' Feature 2: reposition clear completed button in tabs bar
    Private Sub RepositionTabControls()
        btnClearCompleted.Left = pnlTabs.ClientSize.Width - btnClearCompleted.Width - 10
        btnClearCompleted.Top = (pnlTabs.Height - btnClearCompleted.Height) \ 2
    End Sub

    Private Sub PositionEmptyState()
        If pnlEmpty Is Nothing Then Return
        Dim W = pnlQueueOuter.ClientSize.Width
        Dim H = pnlQueueOuter.ClientSize.Height
        If W < 10 OrElse H < 10 Then Return   ' not yet laid out
        Dim cx = W \ 2
        Dim cy = H \ 2
        lblEmptyIcon.Left = Math.Max(10, cx - lblEmptyIcon.Width \ 2)
        lblEmptyIcon.Top = Math.Max(10, cy - 80)
        lblEmptyText.Left = Math.Max(10, cx - lblEmptyText.Width \ 2)
        lblEmptyText.Top = lblEmptyIcon.Top + lblEmptyIcon.Height + 8
    End Sub

    Private Sub RebuildSemaphore()
        _semaphore = New SemaphoreSlim(_maxParallel, 20)
    End Sub

    Private Sub UpdateParallelLabel()
        lblParallel.Text = $"  ↕ {_maxParallel} parallel"
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  SETTINGS
    ' ═══════════════════════════════════════════════════════════════

    Private Sub LoadSettings()
        Try
            If Not File.Exists(SettingsFile) Then Return
            For Each line In File.ReadAllLines(SettingsFile)
                Dim parts = line.Split({"="c}, 2)
                If parts.Length < 2 Then Continue For
                Select Case parts(0).Trim().ToLower()
                    Case "path"
                        If Directory.Exists(parts(1).Trim()) Then _downloadPath = parts(1).Trim()
                    Case "parallel"
                        Dim v As Integer
                        If Integer.TryParse(parts(1).Trim(), v) Then _maxParallel = Math.Max(1, Math.Min(10, v))
                    Case "autoupdate"
                        _autoUpdate = parts(1).Trim().ToLower() <> "false"
                    ' Feature 4: notify on complete
                    Case "notify" : _notifyOnComplete = parts(1).Trim().ToLower() <> "false"
                    ' Feature 5: clipboard monitor
                    Case "clipboardmonitor" : _clipboardMonitor = parts(1).Trim().ToLower() = "true"
                    ' Feature 7: dark mode
                    Case "darkmode" : _darkMode = parts(1).Trim().ToLower() = "true"
                    Case "cookiesbrowser" : _cookiesBrowser = parts(1).Trim().ToLower()
                    Case "cookiesfile" : _cookiesFile = parts(1).Trim()
                End Select
            Next
        Catch
        End Try
    End Sub

    Private Sub SaveSettings()
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile))
            File.WriteAllLines(SettingsFile, {
                "path=" & _downloadPath,
                "parallel=" & _maxParallel,
                "autoupdate=" & _autoUpdate.ToString(),
                "notify=" & _notifyOnComplete.ToString(),
                "clipboardmonitor=" & _clipboardMonitor.ToString(),
                "darkmode=" & _darkMode.ToString(),
                "cookiesbrowser=" & _cookiesBrowser,
                "cookiesfile=" & _cookiesFile
            })
        Catch
        End Try
    End Sub

    Private Sub btnSettings_Click(sender As Object, e As EventArgs) Handles btnSettings.Click
        Using dlg As New SettingsDialog(_downloadPath, _maxParallel, _autoUpdate,
                                        _notifyOnComplete, _clipboardMonitor, _darkMode,
                                        _cookiesBrowser, _cookiesFile)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                _downloadPath = dlg.DownloadPath
                _maxParallel = dlg.MaxParallel
                _autoUpdate = dlg.AutoUpdateOnStart
                _notifyOnComplete = dlg.NotifyOnComplete
                Dim clipChanged = (_clipboardMonitor <> dlg.ClipboardMonitor)
                _clipboardMonitor = dlg.ClipboardMonitor
                Dim themeChanged = (_darkMode <> dlg.DarkMode)
                _darkMode = dlg.DarkMode
                _cookiesBrowser = dlg.CookiesBrowser
                _cookiesFile = dlg.CookiesFile
                SaveSettings()
                RebuildSemaphore()
                UpdateParallelLabel()
                SetStatus($"Settings saved  —  saving to: {_downloadPath}")
                CheckCookiesFile()
                If clipChanged Then ConfigureClipboardTimer()
                If themeChanged Then ApplyTheme()
            End If
        End Using
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  TOOL INSTALL
    ' ═══════════════════════════════════════════════════════════════

    Private Async Function EnsureYtDlpAsync() As Task
        If File.Exists(YtDlpPath) Then
            SetToolStatus("yt-dlp ✓")
            Return
        End If
        SetToolStatus("Downloading yt-dlp…")
        btnAdd.Enabled = False
        Try
            Dim data = Await _http.GetByteArrayAsync(YtDlpUrl)
            File.WriteAllBytes(YtDlpPath, data)
            SetToolStatus("yt-dlp ✓")
        Catch ex As Exception
            SetToolStatus("yt-dlp FAILED")
            MessageBox.Show("Could not download yt-dlp: " & ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnAdd.Enabled = True
        End Try
    End Function

    Private Async Function EnsureFfmpegAsync() As Task
        If File.Exists(FfmpegPath) Then
            _ffmpegReady = True
            SetToolStatus("✓ Ready — max quality: 4K / 1080p")
            Return
        End If
        SetToolStatus("Downloading ffmpeg for 1080p/4K support…")
        Try
            Dim zipData = Await _http.GetByteArrayAsync(FfmpegZipUrl)
            Await Task.Run(Sub()
                Using ms As New MemoryStream(zipData)
                Using zip As New ZipArchive(ms, ZipArchiveMode.Read)
                    For Each entry In zip.Entries
                        If entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) Then
                            entry.ExtractToFile(FfmpegPath, overwrite:=True)
                            Exit For
                        End If
                    Next
                End Using
                End Using
            End Sub)
            If File.Exists(FfmpegPath) Then
                _ffmpegReady = True
                SetToolStatus("✓ Ready — max quality: 4K / 1080p")
            End If
        Catch
            SetToolStatus("⚠ ffmpeg unavailable — max quality: 720p")
        End Try
    End Function

    Private Async Function EnsureDenoAsync() As Task
        If File.Exists(DenoPath) Then
            _denoReady = True
            Return
        End If
        SetToolStatus("Downloading Deno (JS runtime for YouTube)…")
        Try
            Dim zipData = Await _http.GetByteArrayAsync(DenoZipUrl)
            Await Task.Run(Sub()
                Using ms As New MemoryStream(zipData)
                Using zip As New ZipArchive(ms, ZipArchiveMode.Read)
                    For Each entry In zip.Entries
                        If entry.Name.Equals("deno.exe", StringComparison.OrdinalIgnoreCase) Then
                            entry.ExtractToFile(DenoPath, overwrite:=True)
                            Exit For
                        End If
                    Next
                End Using
                End Using
            End Sub)
            If File.Exists(DenoPath) Then
                _denoReady = True
                SetToolStatus("✓ Ready")
            End If
        Catch
            SetToolStatus("⚠ Deno unavailable — some videos may need sign-in")
        End Try
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  AUTO-UPDATE
    ' ═══════════════════════════════════════════════════════════════

    Private Async Function CheckForUpdatesAsync() As Task
        Try
            ' ── yt-dlp ─────────────────────────────────────────────
            If File.Exists(YtDlpPath) Then
                Dim currentVer = Await GetYtDlpCurrentVersionAsync()
                Dim latestVer = Await GetYtDlpLatestVersionAsync()
                If latestVer <> "" AndAlso currentVer <> "" AndAlso currentVer <> latestVer Then
                    SetStatus($"yt-dlp update available ({currentVer} → {latestVer}). Updating…")
                    Try
                        Dim data = Await _http.GetByteArrayAsync(YtDlpUrl)
                        File.WriteAllBytes(YtDlpPath, data)
                        SetStatus($"yt-dlp updated to {latestVer}")
                        SetToolStatus($"yt-dlp {latestVer} ✓  ffmpeg ✓")
                    Catch
                        SetStatus("yt-dlp update failed — keeping current version")
                    End Try
                Else
                    SetStatus("All tools up to date")
                End If
            End If

            ' ── ffmpeg age check (re-download if > 30 days) ─────────
            If File.Exists(FfmpegPath) Then
                Dim age = DateTime.Now - File.GetLastWriteTime(FfmpegPath)
                If age.TotalDays > 30 Then
                    SetStatus("Refreshing ffmpeg (30-day update)…")
                    Try
                        Dim zipData = Await _http.GetByteArrayAsync(FfmpegZipUrl)
                        Await Task.Run(Sub()
                            Using ms As New MemoryStream(zipData)
                            Using zip As New ZipArchive(ms, ZipArchiveMode.Read)
                                For Each entry In zip.Entries
                                    If entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) Then
                                        entry.ExtractToFile(FfmpegPath, overwrite:=True)
                                        Exit For
                                    End If
                                Next
                            End Using
                            End Using
                        End Sub)
                        SetStatus("ffmpeg refreshed")
                    Catch
                        SetStatus("ffmpeg refresh failed — keeping current")
                    End Try
                End If
            End If
        Catch
            ' Update check is non-critical
        End Try
    End Function

    Private Async Function GetYtDlpCurrentVersionAsync() As Task(Of String)
        Return Await Task.Run(Function()
            Try
                Dim psi As New ProcessStartInfo(YtDlpPath, "--version") With {
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .CreateNoWindow = True
                }
                Using p As New Process() With {.StartInfo = psi}
                    p.Start()
                    Dim v = p.StandardOutput.ReadLine()?.Trim()
                    p.WaitForExit()
                    Return If(v, "")
                End Using
            Catch
                Return ""
            End Try
        End Function)
    End Function

    Private Async Function GetYtDlpLatestVersionAsync() As Task(Of String)
        Try
            Dim json = Await _http.GetStringAsync(
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest")
            Dim m = Regex.Match(json, """tag_name""\s*:\s*""([^""]+)""")
            Return If(m.Success, m.Groups(1).Value, "")
        Catch
            Return ""
        End Try
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  TABS
    ' ═══════════════════════════════════════════════════════════════

    Private Sub btnTabAll_Click(s As Object, e As EventArgs) Handles btnTabAll.Click
        SelectTab("All")
    End Sub
    Private Sub btnTabVideo_Click(s As Object, e As EventArgs) Handles btnTabVideo.Click
        SelectTab("Video")
    End Sub
    Private Sub btnTabAudio_Click(s As Object, e As EventArgs) Handles btnTabAudio.Click
        SelectTab("Audio")
    End Sub

    Private Sub SelectTab(tab As String)
        _activeTab = tab
        For Each btn As Button In TabButtons()
            Dim active = btn.Tag?.ToString() = tab
            btn.ForeColor = If(active,
                If(_darkMode, Color.FromArgb(255, 80, 80), Color.FromArgb(200, 0, 0)),
                If(_darkMode, Color.FromArgb(160, 160, 160), Color.FromArgb(80, 80, 80)))
            btn.Font = New Font("Segoe UI", 9, If(active, FontStyle.Bold, FontStyle.Regular))
        Next
        ShowHistoryView(tab = "History")
        If tab <> "History" Then RebuildList()
    End Sub

    Private Function TabButtons() As Button()
        If btnTabHistory Is Nothing Then Return {btnTabAll, btnTabVideo, btnTabAudio}
        Return {btnTabAll, btnTabVideo, btnTabAudio, btnTabHistory}
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  ADD TO QUEUE
    ' ═══════════════════════════════════════════════════════════════

    Private Sub btnAdd_Click(s As Object, e As EventArgs) Handles btnAdd.Click
        Dim raw = txtUrl.Text.Trim()

        ' Batch: multiple lines each containing a URL
        Dim lines = raw.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
        If lines.Length > 1 Then
            Dim ytUrls = lines.Select(Function(l) l.Trim()).Where(Function(l) IsSupportedUrl(l)).ToList()
            If ytUrls.Count > 0 Then
                If Not File.Exists(YtDlpPath) Then
                    MessageBox.Show("yt-dlp is not ready yet. Please wait.", "Not Ready",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                txtUrl.Text = ""
                ReleaseUrlFocus()
                For Each u In ytUrls
                    EnqueueUrl(u)
                Next
                Return
            End If
        End If

        ' Single URL — original logic unchanged
        If Not ValidateUrl(raw) Then Return
        If Not File.Exists(YtDlpPath) Then
            MessageBox.Show("yt-dlp is not ready yet. Please wait.", "Not Ready",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        txtUrl.Text = ""
        ReleaseUrlFocus()
        EnqueueUrl(raw)
    End Sub

    ' Feature 6: EnqueueUrl extracted method
    Private Sub EnqueueUrl(url As String)
        Dim entry = CreateEntry(url, cboFormat.SelectedIndex)
        Task.Run(Async Function()
            Await _semaphore.WaitAsync()
            Try
                Await FetchInfoAndDownload(entry)
            Finally
                _semaphore.Release()
            End Try
        End Function)
    End Sub

    ' Adds the row to the list without starting it. Bulk rows start later, on the pacer's schedule.
    Private Function CreateEntry(url As String, formatIdx As Integer, Optional title As String = Nothing) As DownloadEntry
        Dim entry As New DownloadEntry With {
            .Url = url.Trim(),
            .FormatIndex = formatIdx,
            .FormatLabel = GetFormatLabel(formatIdx),
            .IsAudio = (formatIdx = 5 OrElse formatIdx = 6)
        }
        If title IsNot Nothing Then entry.Title = title
        _items.Add(entry)
        AddItemPanel(entry)
        UpdateEmptyState()
        Return entry
    End Function

    Private Function GetFormatLabel(idx As Integer) As String
        Select Case idx
            Case 0 : Return "Best"
            Case 1 : Return "1080p"
            Case 2 : Return "720p"
            Case 3 : Return "480p"
            Case 4 : Return "360p"
            Case 5 : Return "MP3"
            Case 6 : Return "M4A"
            Case 7 : Return "Video"
            Case Else : Return "MP4"
        End Select
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  FETCH INFO + DOWNLOAD
    ' ═══════════════════════════════════════════════════════════════

    Private Async Function FetchInfoAndDownload(entry As DownloadEntry) As Task
        UpdateItemStatus(entry, "Fetching info…", Color.FromArgb(80, 140, 200))
        Try
            ' Instagram titles are always "Video by <user>", so show the caption's
            ' opening words too, and grab the thumbnail URL in the same call.
            Dim infoArgs = If(entry.IsInstagram,
                $"--replace-in-metadata description ""\s+"" "" "" --print ""%(uploader,channel)s · %(description).70s"" --print duration_string --print thumbnail --playlist-items 1 ""{entry.Url}""",
                $"--no-playlist --get-title --get-duration ""{entry.Url}""")
            Dim raw = Await RunYtDlpCaptureAsync(infoArgs)
            Dim lines = raw.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            If lines.Length >= 1 Then
                entry.Title = lines(0).Trim()
                If entry.IsInstagram Then entry.Title = "Instagram · " & entry.Title.Replace(" · NA", "")
                If entry.Title.Length > 90 Then entry.Title = entry.Title.Substring(0, 87) & "…"
            End If
            If lines.Length >= 2 AndAlso lines(1).Trim() <> "NA" Then entry.Duration = NormalizeDuration(lines(1))
            If entry.IsInstagram AndAlso lines.Length >= 3 AndAlso lines(2).StartsWith("http") Then
                entry.ThumbUrl = lines(2).Trim()
            End If
            UpdateItemLabel(entry)
        Catch
        End Try
        entry.InfoFetched = True

        ' Load thumbnail async (fire and forget)
#Disable Warning BC42358
        Task.Run(Sub() LoadThumbnailAsync(entry))
#Enable Warning BC42358

        Await RunDownload(entry)
    End Function

    Private Async Function RunDownload(entry As DownloadEntry) As Task
        If Not File.Exists(YtDlpPath) Then Return

        ' Every Instagram title is "Video by <user>" — two reels from one account would
        ' collide and the second would be skipped as "already downloaded". The post id
        ' keeps them apart.
        Dim outputTemplate = Path.Combine(_downloadPath,
            If(entry.IsInstagram, "%(title)s [%(id)s].%(ext)s", "%(title)s.%(ext)s"))
        ' --no-playlist would cut an Instagram carousel down to its first clip.
        Dim playlistArg = If(entry.IsInstagram, "", "--no-playlist")
        Dim fmtArgs = BuildFormatArgs(entry.FormatIndex)
        Dim ffArg = If(_ffmpegReady, $"--ffmpeg-location ""{FfmpegPath}""", "")
        Dim denoArg = If(_denoReady, $"--js-runtimes deno:""{DenoPath}""", "")
        ' Do NOT force player clients here. YouTube's SABR-only rollout strips every
        ' adaptive format from the android + web clients, so forcing them leaves only
        ' itag 18 (640x360) and caps every "Best Quality" download at 360p.
        ' yt-dlp's own default client list returns the full ladder up to 4K.
        Dim clientArg = ""
        ' Kept only as a last-resort retry for videos the default clients can't extract.
        Dim fallbackClientArg = "--extractor-args ""youtube:player_client=android,web"""
        ' Report the resolution actually written, so a silent low-quality fallback is visible.
        Dim printArg = "--print ""after_move:YTRES %(resolution)s"" --print ""after_move:YTFILE %(filepath)s"""
        ' yt-dlp writes the updated cookie jar back over whatever --cookies points at.
        ' Pointed at the user's own export, every failed run degrades it a little more
        ' until the login cookies are gone. Always hand it a throwaway copy.
        Dim cookieArg As String
        Dim cookieTemp As String = ""
        If _cookiesFile <> "" AndAlso File.Exists(_cookiesFile) Then
            cookieTemp = CopyCookiesToTemp(_cookiesFile)
            cookieArg = If(cookieTemp <> "", $"--cookies ""{cookieTemp}""", "")
        ElseIf _cookiesBrowser <> "" Then
            cookieArg = $"--cookies-from-browser {_cookiesBrowser}"
        Else
            cookieArg = ""
        End If
        Dim baseArgs = $"{fmtArgs} {ffArg} {denoArg} {clientArg} {playlistArg} --newline --progress {printArg} -o ""{outputTemplate}"" ""{entry.Url}"""
        Dim args = $"{fmtArgs} {ffArg} {cookieArg} {denoArg} {clientArg} {playlistArg} --newline --progress {printArg} -o ""{outputTemplate}"" ""{entry.Url}"""

        entry.IsRunning = True
        entry.StartedUtc = DateTime.UtcNow
        entry.LastError = ""
        UpdateItemStatus(entry, "Downloading…", Color.FromArgb(80, 140, 200))

        Dim code = Await RunProcess(entry, args.Trim())

        ' Cookie database locked — try other browsers before giving up
        If code <> 0 AndAlso code <> -1 AndAlso entry.HasCookieError Then
            ' Firefox first: Chrome 127+ App-Bound Encryption makes every Chrome-family
            ' cookie DB unreadable on this machine ("Could not copy Chrome cookie database").
            Dim fallbackBrowsers = New String() {"firefox", "edge", "chrome", "brave", "chromium"}
            For Each browser In fallbackBrowsers
                If browser = _cookiesBrowser Then Continue For
                entry.HasCookieError = False
                entry.AlreadyExisted = False
                SetProgressUI(entry, 0)
                UpdateItemStatus(entry, $"Trying {browser} cookies…", Color.FromArgb(180, 130, 0))
                Dim fbArgs = $"{fmtArgs} {ffArg} --cookies-from-browser {browser} {denoArg} {clientArg} {playlistArg} --newline --progress {printArg} -o ""{outputTemplate}"" ""{entry.Url}"""
                code = Await RunProcess(entry, fbArgs.Trim())
                If code = 0 OrElse Not entry.HasCookieError Then Exit For
            Next
            ' All browsers locked — retry without cookies
            If code <> 0 AndAlso entry.HasCookieError Then
                entry.HasCookieError = False
                entry.AlreadyExisted = False
                SetProgressUI(entry, 0)
                UpdateItemStatus(entry, "Retrying without cookies…", Color.FromArgb(180, 130, 0))
                code = Await RunProcess(entry, baseArgs.Trim())
            End If
        End If

        ' Default clients couldn't extract it (bot wall / age gate) — one last try
        ' with the android client. This can only return 360p, so say so plainly.
        ' Instagram login wall and no cookies configured — Firefox is the one browser
        ' whose cookies are readable on this machine, so try it once before failing.
        If code <> 0 AndAlso code <> -1 AndAlso entry.IsInstagram AndAlso cookieArg = "" AndAlso
           IsInstagramLoginError(entry.LastError) Then
            entry.AlreadyExisted = False
            entry.LastError = ""
            SetProgressUI(entry, 0)
            UpdateItemStatus(entry, "Instagram wants a login — retrying with Firefox cookies…", Color.FromArgb(180, 130, 0))
            Dim igArgs = $"{fmtArgs} {ffArg} --cookies-from-browser firefox {playlistArg} --newline --progress {printArg} -o ""{outputTemplate}"" ""{entry.Url}"""
            code = Await RunProcess(entry, igArgs.Trim())
        End If

        If code <> 0 AndAlso code <> -1 AndAlso Not entry.IsInstagram Then
            entry.AlreadyExisted = False
            entry.Resolution = ""
            SetProgressUI(entry, 0)
            UpdateItemStatus(entry, "Retrying with android client (360p max)…", Color.FromArgb(180, 130, 0))
            Dim androidArgs = $"{fmtArgs} {ffArg} {cookieArg} {denoArg} {fallbackClientArg} {playlistArg} --newline --progress {printArg} -o ""{outputTemplate}"" ""{entry.Url}"""
            code = Await RunProcess(entry, androidArgs.Trim())
        End If

        entry.IsRunning = False
        If cookieTemp <> "" Then
            Try
                File.Delete(cookieTemp)
            Catch
            End Try
        End If

        If code = 0 Then
            entry.IsDone = True
            If entry.FilePath = "" OrElse Not File.Exists(entry.FilePath) Then
                entry.FilePath = FindDownloadedFile(entry.Title)
            End If
            SetFinalFileSize(entry)
            Dim doneLabel = If(entry.AlreadyExisted, "Already exists ✓", "✔  Complete")
            UpdateItemStatus(entry, doneLabel, Color.FromArgb(30, 160, 60))
            SetProgressUI(entry, 100)
            ShowFolderBtn(entry, True)
            ShowRetryBtn(entry, False)
            ShowCopyPathBtn(entry, entry.FilePath <> "")
            AddToHistory(entry)
            SetStatus(If(entry.AlreadyExisted, $"File already exists: {entry.FilePath}", $"Saved to: {_downloadPath}"))
            ShowToast(entry.Title)
        ElseIf code = -1 Then
            UpdateItemStatus(entry, "Cancelled", Color.FromArgb(150, 150, 150))
            SetProgressUI(entry, 0)
        Else
            Dim msg = If(entry.LastError <> "", FriendlyError(entry.LastError), $"Error  (exit {code})")
            UpdateItemStatus(entry, msg, Color.FromArgb(200, 50, 50))
            SetItemTooltip(entry, If(entry.LastError <> "", entry.LastError, $"yt-dlp exited with code {code}"))
            SetStatus(msg)
            entry.HasFailed = True
            ShowFolderBtn(entry, False)
            ShowRetryBtn(entry, True)
        End If
    End Function

    Private Async Function RunProcess(entry As DownloadEntry, args As String) As Task(Of Integer)
        Return Await Task.Run(Function()
            Dim psi As New ProcessStartInfo(YtDlpPath, args) With {
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .StandardOutputEncoding = New System.Text.UTF8Encoding(False),
                .StandardErrorEncoding = New System.Text.UTF8Encoding(False),
                .CreateNoWindow = True
            }
            entry.Process = New Process() With {.StartInfo = psi}
            AddHandler entry.Process.OutputDataReceived, Sub(s, ev) OnItemOutput(entry, ev.Data)
            AddHandler entry.Process.ErrorDataReceived, Sub(s, ev) OnItemError(entry, ev.Data)
            entry.Process.Start()
            entry.Process.BeginOutputReadLine()
            entry.Process.BeginErrorReadLine()
            entry.Process.WaitForExit()
            Dim code = entry.Process.ExitCode
            entry.Process.Dispose()
            entry.Process = Nothing
            Return code
        End Function)
    End Function

    Private Function BuildFormatArgs(idx As Integer) As String
        If _ffmpegReady Then
            Select Case idx
                ' ── Best: no height cap → gets 4K / 1080p / whatever is highest ──
                Case 0 : Return "-f ""bestvideo[ext=mp4]+bestaudio[ext=m4a]/bestvideo+bestaudio/best"" --merge-output-format mp4"
                ' ── Specific resolutions ──────────────────────────────────────────
                Case 1 : Return "-f ""bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]"" --merge-output-format mp4"
                Case 2 : Return "-f ""bestvideo[ext=mp4][height<=720]+bestaudio[ext=m4a]/best[height<=720]"" --merge-output-format mp4"
                Case 3 : Return "-f ""bestvideo[ext=mp4][height<=480]+bestaudio[ext=m4a]/best[height<=480]"" --merge-output-format mp4"
                Case 4 : Return "-f ""bestvideo[ext=mp4][height<=360]+bestaudio[ext=m4a]/best[height<=360]"" --merge-output-format mp4"
                ' ── Audio only ────────────────────────────────────────────────────
                Case 5 : Return "-x --audio-format mp3 --audio-quality 0"
                Case 6 : Return "-x --audio-format m4a --audio-quality 0"
                ' ── Video only (no audio stream) ──────────────────────────────────
                Case 7 : Return "-f ""bestvideo[ext=mp4]/bestvideo"" --merge-output-format mp4"
                Case Else : Return "-f ""bestvideo[ext=mp4]+bestaudio[ext=m4a]/best"" --merge-output-format mp4"
            End Select
        Else
            ' No ffmpeg — limited to pre-muxed progressive streams (max 720p)
            Select Case idx
                Case 0, 1, 2 : Return "-f ""best[ext=mp4][height<=720]/best[ext=mp4]/best"""
                Case 3       : Return "-f ""best[ext=mp4][height<=480]/best[ext=mp4]/best"""
                Case 4       : Return "-f ""best[ext=mp4][height<=360]/best[ext=mp4]/best"""
                Case 5, 6    : Return "-f ""bestaudio[ext=m4a]/bestaudio"""
                Case 7       : Return "-f ""bestvideo[ext=mp4]/bestvideo"""
                Case Else    : Return "-f ""best[ext=mp4]/best"""
            End Select
        End If
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  THUMBNAIL
    ' ═══════════════════════════════════════════════════════════════

    Private Async Sub LoadThumbnailAsync(entry As DownloadEntry)
        Dim candidates As New List(Of String)
        If entry.ThumbUrl <> "" Then
            candidates.Add(entry.ThumbUrl)
        Else
            Dim videoId = ExtractVideoId(entry.Url)
            If videoId = "" Then Return
            ' Try hqdefault first, then mqdefault
            For Each quality In {"hqdefault", "mqdefault", "default"}
                candidates.Add($"https://img.youtube.com/vi/{videoId}/{quality}.jpg")
            Next
        End If
        For Each url In candidates
            Try
                Dim bytes = Await _http.GetByteArrayAsync(url)
                ' Copy into a Bitmap: the UI call no longer waits, so the stream is gone by the time it runs.
                Dim img As Image
                Using ms As New MemoryStream(bytes), src = Image.FromStream(ms)
                    img = New Bitmap(src)
                End Using
                If entry.PicThumb Is Nothing OrElse entry.PicThumb.IsDisposed Then Return
                UI(Sub()
                    If entry.PicThumb.IsDisposed Then Return
                    entry.PicThumb.Image = img
                    entry.PicThumb.BackColor = Color.Black
                End Sub)
                Return
            Catch
            End Try
        Next
    End Sub

    Private Function ExtractVideoId(url As String) As String
        Dim m = Regex.Match(url, "(?:[?&]v=|youtu\.be/|/shorts/)([A-Za-z0-9_-]{11})")
        Return If(m.Success, m.Groups(1).Value, "")
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  PROCESS OUTPUT
    ' ═══════════════════════════════════════════════════════════════

    Private Sub OnItemOutput(entry As DownloadEntry, data As String)
        If data Is Nothing OrElse data.Trim() = "" Then Return
        Dim line = data.Trim()

        Dim mPct = Regex.Match(line, "\[download\]\s+([\d\.]+)%\s+of\s+~?\s*([\d\.]+\s*\w+)")
        If mPct.Success Then
            Dim pct As Double
            If Double.TryParse(mPct.Groups(1).Value, Globalization.NumberStyles.Float,
                               Globalization.CultureInfo.InvariantCulture, pct) Then
                SetProgressUI(entry, CInt(Math.Min(pct, 100)))
                Dim sz = mPct.Groups(2).Value.Trim()
                If sz <> "" Then entry.FileSize = sz
                UpdateItemLabel(entry)

                ' Feature 1: extract speed + ETA from same line
                Dim mSpeed = Regex.Match(line,
                    "at\s+([\d\.]+\s*\w+/s)\s+ETA\s+([\d:]+)",
                    RegexOptions.IgnoreCase)
                If mSpeed.Success Then
                    entry.SpeedEta = $"  ·  {mSpeed.Groups(1).Value.Trim()}  ETA {mSpeed.Groups(2).Value.Trim()}"
                Else
                    entry.SpeedEta = ""
                End If

                UpdateItemStatus(entry, $"Downloading  {pct:0.0}%{entry.SpeedEta}", Color.FromArgb(80, 140, 200))
            End If
            Return
        End If

        If line.StartsWith("YTRES ") Then
            Dim res = line.Substring(6).Trim()
            If res <> "" AndAlso Not res.StartsWith("NA") AndAlso res <> "audio only" Then
                entry.Resolution = res
                UpdateItemLabel(entry)
            End If
            Return
        End If

        If line.StartsWith("YTFILE ") Then
            Dim fp = line.Substring(7).Trim()
            If fp <> "" AndAlso Not fp.StartsWith("NA") Then entry.FilePath = fp
            Return
        End If

        If line.StartsWith("[download] Destination:") Then
            entry.FilePath = line.Replace("[download] Destination:", "").Trim()
        End If

        If line.Contains("has already been downloaded") Then
            entry.AlreadyExisted = True
            ' Parse the file path from "[download] C:\...\file.mp4 has already been downloaded"
            Dim ap = Regex.Match(line, "^\[download\]\s+(.+?)\s+has already been downloaded$")
            If ap.Success Then entry.FilePath = ap.Groups(1).Value.Trim()
        End If

        Dim mMerge = Regex.Match(line, "\[Merger\].*into ""(.+?)""")
        If mMerge.Success Then
            entry.FilePath = mMerge.Groups(1).Value.Trim()
            UpdateItemStatus(entry, "Merging streams…", Color.FromArgb(180, 130, 0))
            Return
        End If

        If line.StartsWith("[ExtractAudio]") Then
            UpdateItemStatus(entry, "Converting audio…", Color.FromArgb(180, 130, 0))
        End If
    End Sub

    Private Sub OnItemError(entry As DownloadEntry, data As String)
        If data Is Nothing OrElse data.Trim() = "" Then Return
        If data.Contains("Could not copy") OrElse data.ToLower().Contains("cookie database") Then
            entry.HasCookieError = True
        End If
        If data.Contains("ERROR") Then
            entry.LastError = data.Trim()
            UpdateItemStatus(entry, FriendlyError(entry.LastError), Color.FromArgb(200, 50, 50))
        End If
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  yt-dlp CAPTURE
    ' ═══════════════════════════════════════════════════════════════

    Private Async Function RunYtDlpCaptureAsync(args As String) As Task(Of String)
        Return Await Task.Run(Function()
            Try
                Dim psi As New ProcessStartInfo(YtDlpPath, args) With {
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .StandardOutputEncoding = New System.Text.UTF8Encoding(False),
                    .StandardErrorEncoding = New System.Text.UTF8Encoding(False),
                    .CreateNoWindow = True
                }
                Using p As New Process() With {.StartInfo = psi}
                    p.Start()
                    Dim out = p.StandardOutput.ReadToEnd()
                    p.WaitForExit()
                    Return out
                End Using
            Catch
                Return ""
            End Try
        End Function)
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  FILE ACTIONS
    ' ═══════════════════════════════════════════════════════════════

    Private Sub OpenFileLocation(entry As DownloadEntry)
        If entry.FilePath <> "" AndAlso File.Exists(entry.FilePath) Then
            Process.Start("explorer.exe", $"/select,""{entry.FilePath}""")
        ElseIf Directory.Exists(_downloadPath) Then
            Process.Start("explorer.exe", $"""{_downloadPath}""")
        End If
    End Sub

    Private Sub OpenFile(entry As DownloadEntry)
        If entry.FilePath <> "" AndAlso File.Exists(entry.FilePath) Then
            Process.Start(New ProcessStartInfo(entry.FilePath) With {.UseShellExecute = True})
        Else
            MessageBox.Show("File not found. It may have been moved or renamed.",
                            "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Function FindDownloadedFile(title As String) As String
        Try
            Dim clean = New String(title.Where(Function(c) Not Path.GetInvalidFileNameChars().Contains(c)).ToArray())
            clean = clean.Substring(0, Math.Min(clean.Length, 40)).Trim()
            For Each ext In {"mp4", "mkv", "webm", "mp3", "m4a"}
                Dim files = Directory.GetFiles(_downloadPath, $"*{clean}*.{ext}",
                                               SearchOption.TopDirectoryOnly)
                If files.Length > 0 Then Return files(0)
            Next
        Catch
        End Try
        Return ""
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  ITEM PANEL BUILDER  (fixed layout — no overlapping)
    '
    '  Panel h=90
    '  PicThumb   x=10   y=11  w=108 h=68
    '  lblBadge   inside thumb, bottom-right
    '  lblTitle   x=128  y=10  w=PW-357 h=22   (right 229px reserved for btns)
    '  btnPlay    x=PW-220 y=8 w=32 h=32       (visible when file exists)
    '  btnDrag    x=PW-184 y=8 w=32 h=32       (visible when file exists)
    '  btnCopyUrl x=PW-148 y=8 w=32 h=32       (always visible)
    '  btnCopyPath x=PW-112 y=8 w=32 h=32      (visible when file exists)
    '  btnFolder  x=PW-76 y=8  w=32 h=32       (visible when complete)
    '  btnRetry   x=PW-76 y=8  w=32 h=32       (same slot as btnFolder)
    '  btnX       x=PW-40 y=8  w=28 h=28
    '  lblInfo    x=128  y=34  w=PW-357 h=18
    '  pb         x=128  y=55  w=PW-145 h=8    (well below button bottom=40)
    '  lblStatus  x=128  y=66  w=PW-210 h=18
    ' ═══════════════════════════════════════════════════════════════

    Private Sub AddItemPanel(entry As DownloadEntry)
        Dim PW = Math.Max(flpItems.ClientSize.Width - 6, 600)

        Dim pnl As New Panel() With {
            .Width = PW,
            .Height = 90,
            .BackColor = Color.White,
            .Margin = New Padding(3, 3, 3, 0)
        }
        AddHandler pnl.Paint, AddressOf ItemPanelPaint

        ' ── Thumbnail (PictureBox) ──────────────────────────────
        Dim pic As New PictureBox() With {
            .Size = New Size(108, 68),
            .Location = New Point(10, 11),
            .BackColor = Color.FromArgb(28, 28, 28),
            .SizeMode = PictureBoxSizeMode.Zoom
        }
        ' Play icon overlay (hidden once thumbnail loads)
        Dim lblPlay As New Label() With {
            .Text = "▶",
            .Font = New Font("Segoe UI", 20, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.Transparent,
            .AutoSize = True
        }
        pic.Controls.Add(lblPlay)
        AddHandler pic.Paint, Sub(s, ev)
            lblPlay.Left = (pic.Width - lblPlay.Width) \ 2
            lblPlay.Top = (pic.Height - lblPlay.Height) \ 2
            lblPlay.Visible = (pic.Image Is Nothing)
        End Sub

        ' Format badge bottom-right of thumb
        Dim lblBadge As New Label() With {
            .Text = entry.FormatLabel,
            .Font = New Font("Segoe UI", 7, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.FromArgb(180, 0, 0),
            .AutoSize = False,
            .Size = New Size(46, 16),
            .TextAlign = ContentAlignment.MiddleCenter,
            .Location = New Point(62, 52)
        }
        pic.Controls.Add(lblBadge)
        pic.Controls.SetChildIndex(lblBadge, 0)

        ' ── Title ──────────────────────────────────────────────
        Dim lblTitle As New Label() With {
            .Text = entry.Title,
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .ForeColor = Color.FromArgb(18, 18, 18),
            .AutoEllipsis = True,
            .Location = New Point(128, 10),
            .Size = New Size(PW - 357, 22)
        }

        ' ── Info ───────────────────────────────────────────────
        Dim lblInfo As New Label() With {
            .Text = BuildInfoText(entry),
            .Font = New Font("Segoe UI", 8.5),
            .ForeColor = Color.FromArgb(100, 100, 100),
            .AutoEllipsis = True,
            .Location = New Point(128, 34),
            .Size = New Size(PW - 357, 18)
        }

        ' ── Progress bar  (y=55, well below buttons at y=8..40) ─
        Dim pb As New ProgressBar() With {
            .Location = New Point(128, 55),
            .Size = New Size(PW - 145, 8),
            .Style = ProgressBarStyle.Continuous,
            .Value = 0
        }

        ' ── Status ─────────────────────────────────────────────
        Dim lblStatus As New Label() With {
            .Text = "Queued",
            .Font = New Font("Segoe UI", 7.5),
            .ForeColor = Color.FromArgb(140, 140, 140),
            .AutoSize = True,
            .Location = New Point(128, 66)
        }

        ' ── Folder button  (y=8, hidden until complete) ────────
        Dim btnFolder As New Button() With {
            .Text = ChrW(&HE838).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(32, 32),
            .Cursor = Cursors.Hand,
            .Visible = False,
            .Location = New Point(PW - 76, 8)
        }
        btnFolder.FlatAppearance.BorderSize = 0
        btnFolder.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 245, 225)
        Dim ttFolder As New ToolTip()
        ttFolder.SetToolTip(btnFolder, "Show in folder")
        WireIconHover(btnFolder)
        AddHandler btnFolder.Click, Sub(s, ev) OpenFileLocation(entry)

        ' Feature 3: Retry button (same slot as btnFolder, mutually exclusive)
        Dim btnRetry As New Button() With {
            .Text = ChrW(&HE72C).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(32, 32),
            .Cursor = Cursors.Hand,
            .Visible = False,
            .Location = New Point(PW - 76, 8)
        }
        btnRetry.FlatAppearance.BorderSize = 0
        btnRetry.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 240, 200)
        Dim ttRetry As New ToolTip()
        ttRetry.SetToolTip(btnRetry, "Retry")
        WireIconHover(btnRetry)
        AddHandler btnRetry.Click, Sub(s, ev) RetryItem(entry)
        entry.BtnRetry = btnRetry

        ' ── Copy URL button  (y=8, always visible) ─────────────
        Dim btnCopyUrl As New Button() With {
            .Text = ChrW(&HE71B).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(32, 32),
            .Cursor = Cursors.Hand,
            .Location = New Point(PW - 148, 8)
        }
        btnCopyUrl.FlatAppearance.BorderSize = 0
        btnCopyUrl.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 235, 255)
        Dim ttCopyUrl As New ToolTip()
        ttCopyUrl.SetToolTip(btnCopyUrl, "Copy URL")
        WireIconHover(btnCopyUrl)
        AddHandler btnCopyUrl.Click, Sub(s, ev)
            If entry.Url <> "" Then
                Clipboard.SetText(entry.Url)
                FlashCopyButton(btnCopyUrl, ChrW(&HE71B).ToString())
            End If
        End Sub

        ' ── Copy Path button  (y=8, shown when file exists) ────
        Dim btnCopyPath As New Button() With {
            .Text = ChrW(&HE8C8).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(32, 32),
            .Cursor = Cursors.Hand,
            .Visible = False,
            .Location = New Point(PW - 112, 8)
        }
        btnCopyPath.FlatAppearance.BorderSize = 0
        btnCopyPath.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 235, 255)
        Dim ttCopyPath As New ToolTip()
        ttCopyPath.SetToolTip(btnCopyPath, "Copy file path")
        WireIconHover(btnCopyPath)
        AddHandler btnCopyPath.Click, Sub(s, ev)
            If entry.FilePath <> "" Then
                Clipboard.SetText(entry.FilePath)
                FlashCopyButton(btnCopyPath, ChrW(&HE8C8).ToString())
            End If
        End Sub
        entry.BtnCopyUrl = btnCopyUrl
        entry.BtnCopyPath = btnCopyPath

        ' ── Drag handle  (y=8, shown when file exists) ─────────
        ' Grab it and drop the video straight into Telegram, WhatsApp, a folder…
        Dim btnDrag As New Button() With {
            .Text = ChrW(&HE759).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(32, 32),
            .Cursor = Cursors.SizeAll,
            .Visible = False,
            .Location = New Point(PW - 184, 8)
        }
        btnDrag.FlatAppearance.BorderSize = 0
        btnDrag.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 235, 255)
        Dim ttDrag As New ToolTip()
        ttDrag.SetToolTip(btnDrag, "Drag the video into Telegram, WhatsApp, a folder…")
        WireIconHover(btnDrag)
        AddHandler btnDrag.Click, Sub(s, ev) SetStatus("Hold the ✥ button and drag it into Telegram, WhatsApp or a folder")
        entry.BtnDrag = btnDrag

        ' ── Play button  (y=8, shown when file exists) ─────────
        Dim btnPlay As New Button() With {
            .Text = ChrW(&HE768).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 11),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(32, 32),
            .Cursor = Cursors.Hand,
            .Visible = False,
            .Location = New Point(PW - 220, 8)
        }
        btnPlay.FlatAppearance.BorderSize = 0
        btnPlay.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 245, 225)
        Dim ttPlay As New ToolTip()
        ttPlay.SetToolTip(btnPlay, "Play")
        WireIconHover(btnPlay)
        AddHandler btnPlay.Click, Sub(s, ev) OpenFile(entry)
        entry.BtnPlay = btnPlay

        ' ── Remove button  (y=8, always visible) ───────────────
        Dim btnX As New Button() With {
            .Text = ChrW(&HE711).ToString(),
            .Font = New Font("Segoe MDL2 Assets", 10),
            .ForeColor = Color.FromArgb(70, 70, 70),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .Size = New Size(28, 28),
            .Cursor = Cursors.Hand,
            .Location = New Point(PW - 40, 10)
        }
        btnX.FlatAppearance.BorderSize = 0
        btnX.FlatAppearance.MouseOverBackColor = Color.FromArgb(250, 220, 220)
        AddHandler btnX.MouseEnter, Sub(s, ev) CType(s, Button).ForeColor = Color.FromArgb(255, 90, 90)
        AddHandler btnX.MouseLeave, Sub(s, ev) CType(s, Button).ForeColor = IconRestColor()
        AddHandler btnX.Click, Sub(s, ev) RemoveOrCancelItem(entry)
        Dim ttX As New ToolTip()
        ttX.SetToolTip(btnX, "Remove")

        ' ── Drag-out: the handle, the thumbnail and the title all carry the file ──
        For Each ctl As Control In {btnDrag, pic, lblPlay, lblBadge, lblTitle}
            WireFileDrag(ctl, Function() entry.FilePath)
        Next

        ' ── Context menu ───────────────────────────────────────
        Dim ctx = BuildContextMenu(entry)
        For Each ctl As Control In {pnl, pic, lblTitle, lblInfo, lblStatus}
            ctl.ContextMenuStrip = ctx
        Next

        ' ── Store refs ─────────────────────────────────────────
        entry.Panel = pnl
        entry.PicThumb = pic
        entry.LblTitle = lblTitle
        entry.LblInfo = lblInfo
        entry.PbProgress = pb
        entry.LblStatus = lblStatus
        entry.BtnFolder = btnFolder
        entry.BtnRemove = btnX

        ' ── Resize anchor ──────────────────────────────────────
        AddHandler pnl.Resize, Sub(s, ev)
            Dim W = pnl.Width
            lblTitle.Width = W - 357
            lblInfo.Width = W - 357
            pb.Width = W - 145
            lblStatus.Width = W - 210
            btnPlay.Left = W - 220
            btnDrag.Left = W - 184
            btnCopyUrl.Left = W - 148
            btnCopyPath.Left = W - 112
            btnFolder.Left = W - 76
            btnRetry.Left = W - 76
            btnX.Left = W - 40
        End Sub

        pnl.Controls.AddRange(New Control() {pic, lblTitle, lblInfo, pb, lblStatus, btnPlay, btnDrag, btnCopyUrl, btnCopyPath, btnFolder, btnRetry, btnX})

        ' Feature 7: apply theme to the new panel
        ApplyThemeToItemPanel(entry)

        If ShouldShow(entry) Then flpItems.Controls.Add(pnl)
    End Sub

    Private Function BuildContextMenu(entry As DownloadEntry) As ContextMenuStrip
        Dim ctx As New ContextMenuStrip()

        Dim iOpen As New ToolStripMenuItem("📂  Open File Location")
        AddHandler iOpen.Click, Sub(s, ev) OpenFileLocation(entry)

        Dim iPlay As New ToolStripMenuItem("▶  Open / Play")
        AddHandler iPlay.Click, Sub(s, ev) OpenFile(entry)

        Dim iCopyPath As New ToolStripMenuItem("📋  Copy File Path")
        AddHandler iCopyPath.Click, Sub(s, ev)
            If entry.FilePath <> "" Then Clipboard.SetText(entry.FilePath)
        End Sub

        Dim iCopyUrl As New ToolStripMenuItem("🔗  Copy YouTube URL")
        AddHandler iCopyUrl.Click, Sub(s, ev) Clipboard.SetText(entry.Url)

        Dim iRemove As New ToolStripMenuItem("✕  Remove") With {
            .ForeColor = Color.FromArgb(180, 0, 0)
        }
        AddHandler iRemove.Click, Sub(s, ev) RemoveOrCancelItem(entry)

        ctx.Items.AddRange(New ToolStripItem() {
            iOpen, iPlay, iCopyPath, iCopyUrl,
            New ToolStripSeparator(), iRemove
        })

        AddHandler ctx.Opening, Sub(s, ev)
            Dim hasFile = entry.FilePath <> "" AndAlso File.Exists(entry.FilePath)
            iPlay.Enabled = hasFile
            iCopyPath.Enabled = entry.FilePath <> ""
        End Sub

        Return ctx
    End Function

    Private Sub ItemPanelPaint(sender As Object, e As PaintEventArgs)
        Dim pnl = CType(sender, Panel)
        ' Feature 7: use dark mode separator color
        Dim sepColor = If(_darkMode, Color.FromArgb(45, 45, 45), Color.FromArgb(235, 235, 235))
        Using pen As New Pen(sepColor, 1)
            e.Graphics.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1)
        End Using
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  LIST MANAGEMENT
    ' ═══════════════════════════════════════════════════════════════

    Private Sub RebuildList()
        flpItems.Controls.Clear()
        For Each entry In _items
            If ShouldShow(entry) AndAlso entry.Panel IsNot Nothing Then
                flpItems.Controls.Add(entry.Panel)
            End If
        Next
        UpdateEmptyState()
    End Sub

    Private Function ShouldShow(entry As DownloadEntry) As Boolean
        Return _activeTab = "All" OrElse
               (_activeTab = "Video" AndAlso Not entry.IsAudio) OrElse
               (_activeTab = "Audio" AndAlso entry.IsAudio)
    End Function

    Private Sub UpdateEmptyState()
        If _activeTab = "History" Then
            pnlEmpty.Visible = False
            flpItems.Visible = False
            Return
        End If
        Dim any = flpItems.Controls.Count > 0
        pnlEmpty.Visible = Not any
        flpItems.Visible = any
    End Sub

    Private Sub RefreshItemWidths()
        Dim W = Math.Max(flpItems.ClientSize.Width - 6, 600)
        For Each pnl As Panel In flpItems.Controls
            pnl.Width = W
        Next
    End Sub

    Private Sub RemoveOrCancelItem(entry As DownloadEntry)
        If entry.IsRunning AndAlso entry.Process IsNot Nothing Then
            Try : entry.Process.Kill() : Catch : End Try
        End If
        _items.Remove(entry)
        If entry.Panel IsNot Nothing Then
            flpItems.Controls.Remove(entry.Panel)
            entry.Panel.Dispose()
        End If
        UpdateEmptyState()
    End Sub

    ' Feature 2: Clear All Completed button handler
    Private Sub btnClearCompleted_Click(s As Object, e As EventArgs) Handles btnClearCompleted.Click
        Dim toRemove = _items.Where(Function(entry) entry.IsDone).ToList()
        For Each entry In toRemove
            _items.Remove(entry)
            If entry.Panel IsNot Nothing Then
                flpItems.Controls.Remove(entry.Panel)
                entry.Panel.Dispose()
            End If
        Next
        UpdateEmptyState()
        SetStatus($"Cleared {toRemove.Count} completed item(s)")
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  THREAD-SAFE UI HELPERS
    ' ═══════════════════════════════════════════════════════════════

    ' Worker threads hand UI work to the form and carry on — never a blocking Invoke.
    ' On 2026-09-29 a dump of a stuck bulk run showed two WinForms "run queued callbacks"
    ' messages that were posted and then vanished: a download thread sat in Invoke forever
    ' and the pacer's await never resumed. _uiPump (below) drains anything stranded.
    Private Sub UI(action As Action)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
        If Not Me.InvokeRequired Then
            action()
            Return
        End If
        Try
            Me.BeginInvoke(action)
        Catch
        End Try
    End Sub

    ' A same-thread Invoke runs Form1's whole pending-callback queue on the spot, so a
    ' lost wake-up message costs at most one tick instead of hanging a row forever.
    ' WM_TIMER is generated by Windows, not posted, so this tick can't be lost the same way.
    Private _uiPump As System.Windows.Forms.Timer

    Private Sub StartUiPump()
        _uiPump = New System.Windows.Forms.Timer() With {.Interval = 200}
        AddHandler _uiPump.Tick, Sub(s, e)
            Try
                Me.Invoke(Sub()
                          End Sub)
            Catch
            End Try
        End Sub
        _uiPump.Start()
    End Sub

    Private Sub ShowFolderBtn(entry As DownloadEntry, v As Boolean)
        If entry.BtnFolder Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() ShowFolderBtn(entry, v))
            Return
        End If
        entry.BtnFolder.Visible = v
    End Sub

    Private Sub ShowCopyPathBtn(entry As DownloadEntry, v As Boolean)
        If entry.BtnCopyPath Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() ShowCopyPathBtn(entry, v))
            Return
        End If
        entry.BtnCopyPath.Visible = v
        ' Drag-out needs the same thing copy-path does: a real file on disk
        If entry.BtnDrag IsNot Nothing Then entry.BtnDrag.Visible = v
        If entry.BtnPlay IsNot Nothing Then entry.BtnPlay.Visible = v
    End Sub

    ' Press on the control and move a few pixels → Windows file drag (CF_HDROP),
    ' the same thing Explorer hands out, so Telegram / WhatsApp / Discord / folders accept it.
    ' Copy only — dropping on a folder never moves the original away.
    Private Sub WireFileDrag(ctl As Control, getPath As Func(Of String))
        Dim downAt As Point = Point.Empty
        Dim armed As Boolean = False
        AddHandler ctl.MouseDown, Sub(s, ev)
            armed = (ev.Button = MouseButtons.Left)
            downAt = ev.Location
        End Sub
        AddHandler ctl.MouseUp, Sub(s, ev) armed = False
        AddHandler ctl.MouseMove, Sub(s, ev)
            If Not armed OrElse ev.Button <> MouseButtons.Left Then Return
            Dim ds = SystemInformation.DragSize
            If Math.Abs(ev.X - downAt.X) < ds.Width AndAlso Math.Abs(ev.Y - downAt.Y) < ds.Height Then Return
            armed = False
            Dim fp = getPath()
            If fp = "" OrElse Not File.Exists(fp) Then
                If fp <> "" Then SetStatus("Can't drag — the file was moved or deleted")
                Return
            End If
            Dim files As New System.Collections.Specialized.StringCollection()
            files.Add(fp)
            Dim data As New DataObject()
            data.SetFileDropList(files)
            SetStatus($"Dragging {Path.GetFileName(fp)} — drop it in Telegram, WhatsApp or a folder")
            Dim result = ctl.DoDragDrop(data, DragDropEffects.Copy)
            SetStatus(If(result = DragDropEffects.None, "Drag cancelled", $"Dropped {Path.GetFileName(fp)}"))
        End Sub
    End Sub

    ' Flash a copy button to ✓ for ~900ms after a successful copy
    Private Sub FlashCopyButton(btn As Button, originalText As String)
        btn.Text = "✓"
        btn.ForeColor = Color.FromArgb(30, 160, 60)
        Dim t As New System.Windows.Forms.Timer() With {.Interval = 900}
        AddHandler t.Tick, Sub(s, ev)
            t.Stop()
            t.Dispose()
            If btn IsNot Nothing AndAlso Not btn.IsDisposed Then
                btn.Text = originalText
                btn.ForeColor = IconRestColor()
            End If
        End Sub
        t.Start()
    End Sub

    ' Feature 3: ShowRetryBtn helper (mirrors ShowFolderBtn)
    Private Sub ShowRetryBtn(entry As DownloadEntry, v As Boolean)
        If entry.BtnRetry Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() ShowRetryBtn(entry, v))
            Return
        End If
        entry.BtnRetry.Visible = v
    End Sub

    ' Feature 3: RetryItem method
    Private Sub RetryItem(entry As DownloadEntry)
        entry.IsDone = False
        entry.HasFailed = False
        entry.IsRunning = False
        entry.SpeedEta = ""
        entry.FilePath = ""
        ShowRetryBtn(entry, False)
        ShowCopyPathBtn(entry, False)
        SetProgressUI(entry, 0)
        UpdateItemStatus(entry, "Retrying…", Color.FromArgb(80, 140, 200))
        Task.Run(Async Function()
            Await _semaphore.WaitAsync()
            Try
                If entry.InfoFetched Then
                    Await RunDownload(entry)
                Else
                    Await FetchInfoAndDownload(entry)
                End If
            Finally
                _semaphore.Release()
            End Try
        End Function)
    End Sub

    Private Sub UpdateItemStatus(entry As DownloadEntry, text As String, color As Color)
        If entry.LblStatus Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() UpdateItemStatus(entry, text, color))
            Return
        End If
        entry.LblStatus.Text = text
        entry.LblStatus.ForeColor = color
    End Sub

    Private Sub UpdateItemLabel(entry As DownloadEntry)
        If entry.LblInfo Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() UpdateItemLabel(entry))
            Return
        End If
        entry.LblTitle.Text = entry.Title
        entry.LblInfo.Text = BuildInfoText(entry)
    End Sub

    Private Sub SetItemTooltip(entry As DownloadEntry, text As String)
        If entry.LblStatus Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() SetItemTooltip(entry, text))
            Return
        End If
        Try
            Dim tt As New ToolTip()
            tt.SetToolTip(entry.LblStatus, text)
        Catch
        End Try
    End Sub

    Private Sub SetProgressUI(entry As DownloadEntry, value As Integer)
        If entry.PbProgress Is Nothing Then Return
        If Me.InvokeRequired Then
            UI(Sub() SetProgressUI(entry, value))
            Return
        End If
        entry.PbProgress.Value = Math.Max(0, Math.Min(100, value))
    End Sub

    ' Size of the finished file on disk. The progress-line size is only the last
    ' stream leg (audio, ~10MB) on a merged download, so it lies about the output.
    Private Sub SetFinalFileSize(entry As DownloadEntry)
        Try
            If entry.FilePath = "" OrElse Not File.Exists(entry.FilePath) Then
                entry.FilePath = NewestFileSince(entry.StartedUtc)
            End If
            If entry.FilePath = "" OrElse Not File.Exists(entry.FilePath) Then Return
            Dim bytes = New FileInfo(entry.FilePath).Length
            entry.FileSize = FormatBytes(bytes)
            UpdateItemLabel(entry)
        Catch
        End Try
    End Sub

    Private Function NewestFileSince(sinceUtc As DateTime) As String
        Try
            Dim best = ""
            Dim bestAt = sinceUtc
            For Each f In Directory.GetFiles(_downloadPath, "*", SearchOption.TopDirectoryOnly)
                Dim w = File.GetLastWriteTimeUtc(f)
                If w >= bestAt Then
                    bestAt = w
                    best = f
                End If
            Next
            Return best
        Catch
            Return ""
        End Try
    End Function

    ' yt-dlp prints a clip under a minute as bare seconds ("19", sometimes "19.4").
    ' Show it as m:ss like every longer duration. Anything with a colon is left alone.
    Friend Shared Function NormalizeDuration(raw As String) As String
        Dim t = If(raw, "").Trim()
        If t = "" OrElse t.Contains(":") Then Return t
        Dim secs As Double
        If Not Double.TryParse(t, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, secs) Then Return t
        Dim whole = CInt(Math.Floor(secs))
        Return $"{whole \ 60}:{whole Mod 60:00}"
    End Function

    Private Function FormatBytes(bytes As Long) As String
        If bytes >= 1073741824L Then Return (bytes / 1073741824.0).ToString("0.00") & " GB"
        If bytes >= 1048576L Then Return (bytes / 1048576.0).ToString("0.0") & " MB"
        If bytes >= 1024L Then Return (bytes / 1024.0).ToString("0") & " KB"
        Return bytes & " B"
    End Function

    ' Row icons are monochrome MDL2 glyphs so ForeColor actually applies.
    ' The old colour-emoji ignored ForeColor and rendered near-black on the dark row.
    ' yt-dlp's raw ERROR line is long and ends in two wiki URLs. Say what it means
    ' and what to do about it; the full text stays on the row's tooltip.
    ' A cookies.txt exported while signed OUT looks fine (it has NID, PREF, SOCS,
    ' VISITOR_INFO1_LIVE...) but carries no login, so it cannot clear a bot check.
    ' Say so in words instead of letting every download fail mysteriously.
    Private Shared ReadOnly LoginCookieNames As String() =
        {"SID", "HSID", "SSID", "APISID", "SAPISID", "LOGIN_INFO", "__Secure-1PSID", "__Secure-3PSID"}

    Private Function CopyCookiesToTemp(src As String) As String
        Try
            Dim dst = Path.Combine(Path.GetTempPath(), $"ytdl_cookies_{Guid.NewGuid():N}.txt")
            File.Copy(src, dst, True)
            Return dst
        Catch
            Return ""
        End Try
    End Function

    Private Function CookiesFileHasLogin(path As String) As Boolean
        Try
            For Each line In File.ReadAllLines(path)
                If line.StartsWith("#") OrElse line.Trim() = "" Then Continue For
                Dim cols = line.Split(ChrW(9))
                If cols.Length < 7 Then Continue For
                If LoginCookieNames.Contains(cols(5).Trim()) Then Return True
            Next
        Catch
        End Try
        Return False
    End Function

    Private Sub CheckCookiesFile()
        If _cookiesFile = "" Then Return
        If Not File.Exists(_cookiesFile) Then
            SetStatus("⚠ Cookies file not found: " & _cookiesFile)
            Return
        End If
        If Not CookiesFileHasLogin(_cookiesFile) Then
            SetStatus("⚠ Cookies file has no login cookies (no SID / SAPISID / LOGIN_INFO) — " &
                      "re-export it while signed in to YouTube, or it won't get past bot checks")
        End If
    End Sub

    Private Shared Function IsInstagramLoginError(raw As String) As Boolean
        Dim r = If(raw, "").ToLowerInvariant()
        Return r.Contains("[instagram]") AndAlso
            (r.Contains("login") OrElse r.Contains("cookies") OrElse r.Contains("rate-limit") OrElse
             r.Contains("not available") OrElse r.Contains("private"))
    End Function

    Private Function FriendlyError(raw As String) As String
        Dim r = If(raw, "")
        If IsInstagramLoginError(r) Then
            Return "Instagram needs a login for this post - sign in to Instagram in Firefox (or pick a browser / cookies.txt in Settings)"
        End If
        If r.Contains("[Instagram]") AndAlso r.Contains("There is no video") Then
            Return "This Instagram post is a photo, not a video"
        End If
        If r.Contains("not a bot") OrElse r.Contains("Sign in to confirm") Then
            Return "YouTube blocked this IP (bot check) - wait ~30 min, or add cookies from a signed-in browser"
        End If
        If r.Contains("Requested format is not available") Then
            Return "That quality isn't offered for this video - try a lower option"
        End If
        If r.Contains("Private video") Then Return "Private video - needs an account with access"
        If r.Contains("members-only") OrElse r.Contains("This video is available to this channel") Then
            Return "Members-only video - needs channel membership cookies"
        End If
        If r.Contains("Video unavailable") Then Return "Video unavailable (removed, or blocked in your country)"
        If r.Contains("age") AndAlso r.Contains("confirm") Then Return "Age-restricted - needs signed-in cookies"
        If r.Contains("Unable to download webpage") OrElse r.Contains("getaddrinfo") Then
            Return "Network error - couldn't reach the site"
        End If
        ' Unknown error: show yt-dlp's own words, minus the noise
        Dim t = r.Replace("ERROR:", "").Trim()
        Dim cut = t.IndexOf(". See  https")
        If cut > 0 Then t = t.Substring(0, cut)
        If t.Length > 110 Then t = t.Substring(0, 107) & "..."
        Return t
    End Function

    Private Function IconRestColor() As Color
        Return If(_darkMode, Color.FromArgb(190, 190, 190), Color.FromArgb(70, 70, 70))
    End Function

    Private Function IconHoverColor() As Color
        Return If(_darkMode, Color.White, Color.Black)
    End Function

    Private Sub WireIconHover(b As Button)
        AddHandler b.MouseEnter, Sub(s, ev) CType(s, Button).ForeColor = IconHoverColor()
        AddHandler b.MouseLeave, Sub(s, ev) CType(s, Button).ForeColor = IconRestColor()
    End Sub

    Private Function BuildInfoText(entry As DownloadEntry) As String
        Dim parts As New List(Of String)
        If entry.Duration <> "" Then parts.Add(entry.Duration)
        If entry.FileSize <> "" Then parts.Add(entry.FileSize)
        If entry.Resolution <> "" Then
            parts.Add($"{entry.FormatLabel} → {entry.Resolution}")
        Else
            parts.Add(entry.FormatLabel)
        End If
        Return String.Join("   ·   ", parts)
    End Function

    Private Sub SetStatus(text As String)
        If Me.InvokeRequired Then
            UI(Sub() SetStatus(text))
            Return
        End If
        lblStatusBar.Text = text
    End Sub

    Private Sub SetToolStatus(text As String)
        If Me.InvokeRequired Then
            UI(Sub() SetToolStatus(text))
            Return
        End If
        lblToolStatus.Text = text
        lblToolStatus.ForeColor = If(text.Contains("FAILED"),
            Color.FromArgb(200, 50, 50), Color.FromArgb(60, 150, 60))
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  INPUT EVENTS
    ' ═══════════════════════════════════════════════════════════════

    ' Feature 6: IsYouTubeUrl helper (also used by clipboard monitor)
    Private Function IsYouTubeUrl(s As String) As Boolean
        Return Regex.IsMatch(s.Trim(), "^https?://(www\.|m\.)?(youtube\.com/watch|youtu\.be/|youtube\.com/shorts/)",
                             RegexOptions.IgnoreCase)
    End Function

    ' Reels, posts (incl. carousels), IGTV and stories. yt-dlp's Instagram extractor
    ' does the actual work — public posts need no login.
    Private Shared Function IsInstagramUrl(s As String) As Boolean
        Return Regex.IsMatch(s.Trim(), "^https?://(www\.|m\.)?(instagram\.com|instagr\.am)/(reels?|p|tv|stories)/",
                             RegexOptions.IgnoreCase)
    End Function

    Private Function IsSupportedUrl(s As String) As Boolean
        Return IsYouTubeUrl(s) OrElse IsInstagramUrl(s)
    End Function

    Private Function ValidateUrl(url As String) As Boolean
        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show("Please paste a YouTube or Instagram URL first.", "No URL",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If
        If Not (url.StartsWith("http://") OrElse url.StartsWith("https://")) Then
            MessageBox.Show("URL must start with https://", "Invalid URL",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If
        Return True
    End Function

    ' Feature 6: Rewritten btnPaste_Click for batch paste
    Private Sub btnPaste_Click(s As Object, e As EventArgs) Handles btnPaste.Click
        If Not Clipboard.ContainsText() Then Return
        Dim raw = Clipboard.GetText()
        Dim lines = raw.Split({vbCrLf, vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)
        Dim urls = lines.Select(Function(l) l.Trim()).Where(Function(l) IsSupportedUrl(l)).ToList()
        If urls.Count > 1 Then
            If Not File.Exists(YtDlpPath) Then
                MessageBox.Show("yt-dlp is not ready yet.", "Not Ready",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            For Each u In urls
                EnqueueUrl(u)
            Next
            SetStatus($"Queued {urls.Count} URLs from clipboard")
        Else
            txtUrl.Text = raw.Trim()
            txtUrl.SelectAll()
            txtUrl.Focus()
        End If
    End Sub

    Private Sub txtUrl_KeyDown(s As Object, e As KeyEventArgs) Handles txtUrl.KeyDown
        If e.KeyCode = Keys.Enter Then
            btnAdd.PerformClick()
            e.SuppressKeyPress = True
        End If
    End Sub

    ' After a link is queued the box is empty — leaving the cursor in it just blinks at
    ' the user for the whole download and hides the grey "Paste … URL here" hint.
    Private Sub ReleaseUrlFocus()
        Me.ActiveControl = Nothing
    End Sub

    ' Ctrl+V anywhere in the window pastes into the URL box (the box no longer keeps focus).
    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = (Keys.Control Or Keys.V) AndAlso TypeOf Me.ActiveControl IsNot TextBoxBase Then
            btnPaste.PerformClick()
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub txtUrl_Enter(s As Object, e As EventArgs) Handles txtUrl.Enter
        If txtUrl.Text <> "" Then txtUrl.SelectAll()
    End Sub

    Private Sub Form1_FormClosing(s As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        ' Feature 5: stop clipboard timer before closing
        If _clipboardTimer IsNot Nothing Then _clipboardTimer.Stop()
        If _bulk IsNot Nothing Then _bulk.Stopped = True
        For Each item In _items
            If item.IsRunning AndAlso item.Process IsNot Nothing Then
                Try : item.Process.Kill() : Catch : End Try
            End If
        Next
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  BULK DOWNLOAD — paced queue with automatic rate-limit backoff
    '  The popup (BulkDialog) collects links; the strip (BulkStrip) shows
    '  the batch. This loop decides WHEN each link starts: at most
    '  MaxAtOnce running, GapSeconds between starts, and on a
    '  "too many requests" answer it pauses, then carries on slower.
    ' ═══════════════════════════════════════════════════════════════

    Private _bulkStrip As BulkStrip
    Private _bulk As BulkBatch = Nothing
    Private ReadOnly _bulkQueue As New LinkedList(Of DownloadEntry)
    Private ReadOnly _bulkRunning As New HashSet(Of DownloadEntry)
    Private ReadOnly _bulkRetries As New Dictionary(Of Guid, Integer)
    Private ReadOnly _bulkFinished As New System.Collections.Concurrent.ConcurrentQueue(Of DownloadEntry)
    Private ReadOnly _bulkTimer As New System.Windows.Forms.Timer() With {.Interval = 250}
    Private ReadOnly _rng As New Random()

    Private Sub SetupBulkStrip()
        AddHandler _bulkTimer.Tick, Sub(s, e) BulkTick()
        _bulkStrip = New BulkStrip()
        Me.Controls.Add(_bulkStrip)
        ' Dock order follows child index: this lands the strip under the URL row, above the tabs.
        Me.Controls.SetChildIndex(_bulkStrip, Me.Controls.GetChildIndex(pnlTabs) + 1)
        AddHandler _bulkStrip.BtnPause.Click, Sub(s, e)
            If _bulk Is Nothing Then Return
            _bulk.Paused = Not _bulk.Paused
            _bulkStrip.BtnPause.Text = If(_bulk.Paused, "Resume", "Pause")
            ' Resuming shouldn't make the user sit through a gap they already waited out.
            If Not _bulk.Paused Then _bulk.NextStartUtc = DateTime.UtcNow
            _bulkStrip.Invalidate()
        End Sub
        AddHandler _bulkStrip.BtnStop.Click, Sub(s, e)
            If _bulk Is Nothing Then Return
            _bulk.Stopped = True
            _bulkStrip.BtnStop.Enabled = False
            _bulkStrip.BtnPause.Enabled = False
        End Sub
        AddHandler _bulkStrip.BtnClose.Click, Sub(s, e) _bulkStrip.Visible = False
    End Sub

    Private Sub btnBulk_Click(s As Object, e As EventArgs) Handles btnBulk.Click
        If Not File.Exists(YtDlpPath) Then
            MessageBox.Show("yt-dlp is not ready yet. Please wait.", "Not Ready",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ' Links already downloading or done are skipped; failed ones may be pasted again.
        Dim inList = _items.Where(Function(x) Not x.HasFailed).Select(Function(x) x.Url).ToList()
        Using dlg As New BulkDialog(AddressOf IsSupportedUrl, inList, cboFormat.SelectedIndex, _maxParallel, _darkMode)
            If dlg.ShowDialog(Me) <> DialogResult.OK OrElse dlg.Urls.Count = 0 Then Return
            StartBulk(dlg.Urls.ToList(), dlg.FormatIndex, dlg.Pace)
        End Using
    End Sub

    Private Sub StartBulk(urls As List(Of String), formatIdx As Integer, pace As BulkPace)
        ' A second paste while a batch is running joins that batch instead of racing it.
        If _bulk Is Nothing OrElse _bulk.Finished Then
            _bulk = New BulkBatch()
            _bulkRetries.Clear()
        End If
        If Not _bulk.SlowedDown Then
            _bulk.Pace = pace
            _bulk.MaxAtOnce = BulkBatch.AtOnceFor(pace, _maxParallel)
            _bulk.GapSeconds = BulkBatch.GapFor(pace)
        End If
        For Each u In urls
            _bulk.Total += 1
            Dim entry = CreateEntry(u, formatIdx, u)
            UpdateItemStatus(entry, $"Waiting in bulk queue  (#{_bulk.Total})", Color.FromArgb(140, 140, 140))
            _bulkQueue.AddLast(entry)
        Next
        SetStatus($"Bulk: {urls.Count} link(s) added — {_bulk.MaxAtOnce} at a time, {_bulk.GapSeconds} s apart")

        _bulkStrip.Batch = _bulk
        _bulkStrip.Dark = _darkMode
        If Not _bulkTimer.Enabled Then
            _bulkStrip.BtnPause.Text = "Pause"
            _bulkStrip.BtnPause.Enabled = True
            _bulkStrip.BtnStop.Enabled = True
            _bulkStrip.StartAnimation()
            _bulkTimer.Start()
            BulkTick()
        End If
    End Sub

    ' The pacer is a timer tick, not an awaited loop. An await resumes through a posted
    ' message, and a lost one froze the first version of this batch for good.
    Private Sub BulkTick()
        Dim b = _bulk
        If b Is Nothing Then
            _bulkTimer.Stop()
            Return
        End If

        ' Rows whose worker finished since the last tick
        Dim finished As DownloadEntry = Nothing
        While _bulkFinished.TryDequeue(finished)
            _bulkRunning.Remove(finished)
            OnBulkItemFinished(finished, b)
        End While

        If b.Stopped AndAlso _bulkQueue.Count > 0 Then
            For Each entry In _bulkQueue
                If Not _items.Contains(entry) Then Continue For
                UpdateItemStatus(entry, "Not started — bulk stopped.  Press ↻ to start it.", Color.FromArgb(150, 150, 150))
                ShowRetryBtn(entry, True)
            Next
            b.Skipped += _bulkQueue.Count
            _bulkQueue.Clear()
        End If

        ' Start as many as the pace allows right now (Fast pace has no gap, so several at once).
        While _bulkQueue.Count > 0 AndAlso Not b.Paused AndAlso Not b.InCooldown AndAlso
              _bulkRunning.Count < b.MaxAtOnce AndAlso DateTime.UtcNow >= b.NextStartUtc
            Dim entry = _bulkQueue.First.Value
            _bulkQueue.RemoveFirst()
            If Not _items.Contains(entry) Then
                b.Skipped += 1   ' removed by the user before it started
                Continue While
            End If
            ' A little jitter so the requests don't land on a perfect metronome.
            Dim gap = b.GapSeconds + If(b.GapSeconds > 0, _rng.NextDouble() * b.GapSeconds * 0.3, 0)
            b.NextStartUtc = DateTime.UtcNow.AddSeconds(gap)
            StartBulkItem(entry, b)
        End While

        RefreshBulkCounts(b)
        If _bulkQueue.Count = 0 AndAlso _bulkRunning.Count = 0 Then
            _bulkTimer.Stop()
            b.Finished = True
            _bulkStrip.FinishAnimation()
            SetStatus($"Bulk finished — {b.Done} saved" &
                      If(b.Failed > 0, $", {b.Failed} failed", "") &
                      If(b.Skipped > 0, $", {b.Skipped} skipped", "") & $"  ·  {_downloadPath}")
        End If
    End Sub

    ' The worker runs entirely on the thread pool and just drops the row into
    ' _bulkFinished when it's done; the next BulkTick picks it up.
    Private Sub StartBulkItem(entry As DownloadEntry, b As BulkBatch)
        _bulkRunning.Add(entry)
        UpdateItemStatus(entry, "Starting…", Color.FromArgb(80, 140, 200))
        Task.Run(Async Function()
            Try
                ' Capture it: Settings can rebuild the semaphore while this one waits.
                Dim sem = _semaphore
                Await sem.WaitAsync()
                Try
                    If entry.InfoFetched Then
                        Await RunDownload(entry)
                    Else
                        Await FetchInfoAndDownload(entry)
                    End If
                Finally
                    sem.Release()
                End Try
            Catch
            Finally
                _bulkFinished.Enqueue(entry)
            End Try
        End Function)
    End Sub

    Private Sub OnBulkItemFinished(entry As DownloadEntry, b As BulkBatch)
        Dim tries = 0
        _bulkRetries.TryGetValue(entry.Id, tries)
        If entry.IsDone Then
            b.Done += 1
        ElseIf entry.HasFailed AndAlso IsRateLimitError(entry.LastError) AndAlso tries < 2 AndAlso
               Not b.Stopped AndAlso _items.Contains(entry) Then
            _bulkRetries(entry.Id) = tries + 1
            HitRateLimit(b)
            entry.HasFailed = False
            entry.FilePath = ""
            entry.SpeedEta = ""
            ShowRetryBtn(entry, False)
            SetProgressUI(entry, 0)
            UpdateItemStatus(entry, $"Rate-limited — will try again after the {b.CooldownSeconds} s cooldown",
                             Color.FromArgb(180, 130, 0))
            _bulkQueue.AddFirst(entry)
        ElseIf entry.HasFailed Then
            b.Failed += 1
        Else
            b.Skipped += 1   ' cancelled or removed while running
        End If
    End Sub

    ' The site pushed back: wait, then carry on one at a time with a wider gap.
    ' Several rows failing together count as one hit, not a stacked cooldown.
    Private Sub HitRateLimit(b As BulkBatch)
        If b.InCooldown Then Return
        b.RateLimitHits += 1
        b.CooldownSeconds = If(b.RateLimitHits = 1, 60, Math.Min(600, b.CooldownSeconds * 2))
        b.CooldownUntilUtc = DateTime.UtcNow.AddSeconds(b.CooldownSeconds)
        b.MaxAtOnce = 1
        b.GapSeconds = Math.Min(60, Math.Max(15, b.GapSeconds * 2))
        b.SlowedDown = True
        b.NextStartUtc = b.CooldownUntilUtc
        SetStatus($"Rate limit hit — bulk paused for {b.CooldownSeconds} s, then 1 at a time, {b.GapSeconds} s apart")
    End Sub

    Private Sub RefreshBulkCounts(b As BulkBatch)
        b.Waiting = _bulkQueue.Count
        b.Running = _bulkRunning.Count
        Dim sum = 0.0
        For Each entry In _bulkRunning
            If entry.PbProgress IsNot Nothing AndAlso Not entry.PbProgress.IsDisposed Then sum += entry.PbProgress.Value
        Next
        b.RunningPercentSum = sum
    End Sub

    Private Shared Function IsRateLimitError(raw As String) As Boolean
        Dim r = If(raw, "").ToLowerInvariant()
        Return r.Contains("http error 429") OrElse r.Contains("too many requests") OrElse
               r.Contains("rate-limit") OrElse r.Contains("rate limit") OrElse
               r.Contains("not a bot") OrElse r.Contains("sign in to confirm")
    End Function

    ' ═══════════════════════════════════════════════════════════════
    '  FEATURE 4 — TOAST NOTIFICATION
    ' ═══════════════════════════════════════════════════════════════

    Private Sub ShowToast(title As String)
        If Not _notifyOnComplete Then Return
        If Me.InvokeRequired Then
            UI(Sub() ShowToast(title))
            Return
        End If
        Dim t As New ToastForm(title)
        t.Show(Me)
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  FEATURE 5 — CLIPBOARD MONITOR
    ' ═══════════════════════════════════════════════════════════════

    Private Sub ConfigureClipboardTimer()
        If _clipboardTimer IsNot Nothing Then
            _clipboardTimer.Stop()
            _clipboardTimer.Dispose()
            _clipboardTimer = Nothing
        End If
        If Not _clipboardMonitor Then Return
        _lastClipboardText = If(Clipboard.ContainsText(), Clipboard.GetText().Trim(), "")
        _clipboardTimer = New System.Windows.Forms.Timer() With {.Interval = 1500}
        AddHandler _clipboardTimer.Tick, AddressOf ClipboardTimer_Tick
        _clipboardTimer.Start()
    End Sub

    ' WinForms Timer fires on UI thread — no Invoke needed
    Private Sub ClipboardTimer_Tick(sender As Object, e As EventArgs)
        Try
            If Not Clipboard.ContainsText() Then Return
            Dim txt = Clipboard.GetText().Trim()
            If txt = _lastClipboardText Then Return
            _lastClipboardText = txt
            If IsSupportedUrl(txt) AndAlso txtUrl.Text.Trim() = "" Then
                txtUrl.Text = txt
                txtUrl.SelectAll()
            End If
        Catch
        End Try
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  FEATURE 7 — DARK MODE
    ' ═══════════════════════════════════════════════════════════════

    Private Sub ApplyTheme()
        Dim dark = _darkMode
        Me.BackColor = If(dark, Color.FromArgb(18, 18, 18), Color.White)
        pnlInput.BackColor = If(dark, Color.FromArgb(32, 32, 32), Color.FromArgb(240, 240, 240))
        txtUrl.BackColor = If(dark, Color.FromArgb(40, 40, 40), Color.White)
        txtUrl.ForeColor = If(dark, Color.White, Color.FromArgb(30, 30, 30))
        btnPaste.BackColor = If(dark, Color.FromArgb(55, 55, 55), Color.FromArgb(220, 220, 220))
        btnPaste.ForeColor = If(dark, Color.White, Color.FromArgb(30, 30, 30))
        btnPaste.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(75, 75, 75), Color.FromArgb(200, 200, 200))
        btnBulk.BackColor = btnPaste.BackColor
        btnBulk.ForeColor = btnPaste.ForeColor
        btnBulk.FlatAppearance.MouseOverBackColor = btnPaste.FlatAppearance.MouseOverBackColor
        If _bulkStrip IsNot Nothing Then
            _bulkStrip.Dark = dark
            _bulkStrip.ApplyColors()
        End If
        pnlTabs.BackColor = If(dark, Color.FromArgb(22, 22, 22), Color.White)
        lblTabLine.BackColor = If(dark, Color.FromArgb(45, 45, 45), Color.FromArgb(218, 218, 218))
        For Each btn As Button In TabButtons()
            btn.BackColor = If(dark, Color.FromArgb(22, 22, 22), Color.White)
            Dim isActive = btn.Tag?.ToString() = _activeTab
            btn.ForeColor = If(isActive,
                If(dark, Color.FromArgb(255, 80, 80), Color.FromArgb(200, 0, 0)),
                If(dark, Color.FromArgb(160, 160, 160), Color.FromArgb(80, 80, 80)))
            btn.Font = New Font("Segoe UI", 9, If(isActive, FontStyle.Bold, FontStyle.Regular))
        Next
        btnClearCompleted.BackColor = If(dark, Color.FromArgb(50, 50, 50), Color.FromArgb(240, 240, 240))
        btnClearCompleted.ForeColor = If(dark, Color.FromArgb(160, 160, 160), Color.FromArgb(100, 100, 100))
        btnClearCompleted.FlatAppearance.BorderColor = If(dark, Color.FromArgb(70, 70, 70), Color.FromArgb(210, 210, 210))
        pnlQueueOuter.BackColor = If(dark, Color.FromArgb(28, 28, 28), Color.FromArgb(246, 246, 246))
        flpItems.BackColor = If(dark, Color.FromArgb(28, 28, 28), Color.FromArgb(246, 246, 246))
        pnlEmpty.BackColor = If(dark, Color.FromArgb(28, 28, 28), Color.FromArgb(246, 246, 246))
        lblEmptyIcon.ForeColor = If(dark, Color.FromArgb(60, 60, 60), Color.FromArgb(215, 215, 215))
        lblEmptyText.ForeColor = If(dark, Color.FromArgb(80, 80, 80), Color.FromArgb(170, 170, 170))
        pnlStatus.BackColor = If(dark, Color.FromArgb(24, 24, 24), Color.FromArgb(232, 232, 232))
        lblStatusBar.ForeColor = If(dark, Color.FromArgb(130, 130, 130), Color.FromArgb(80, 80, 80))
        ApplyHistoryTheme()
        If _activeTab = "History" Then BuildHistoryList()
        For Each entry In _items.ToList()
            ApplyThemeToItemPanel(entry)
        Next
        flpItems.Invalidate(True)
    End Sub

    Private Sub ApplyThemeToItemPanel(entry As DownloadEntry)
        If entry.Panel Is Nothing OrElse entry.Panel.IsDisposed Then Return
        Dim dark = _darkMode
        entry.Panel.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
        If entry.LblTitle IsNot Nothing Then
            entry.LblTitle.ForeColor = If(dark, Color.White, Color.FromArgb(18, 18, 18))
        End If
        If entry.LblInfo IsNot Nothing Then
            entry.LblInfo.ForeColor = If(dark, Color.FromArgb(160, 160, 160), Color.FromArgb(100, 100, 100))
        End If
        If entry.BtnFolder IsNot Nothing Then
            entry.BtnFolder.ForeColor = IconRestColor()
            entry.BtnFolder.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
            entry.BtnFolder.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(40, 70, 40), Color.FromArgb(225, 245, 225))
        End If
        If entry.BtnRetry IsNot Nothing Then
            entry.BtnRetry.ForeColor = IconRestColor()
            entry.BtnRetry.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
        End If
        If entry.BtnRemove IsNot Nothing Then
            entry.BtnRemove.ForeColor = IconRestColor()
            entry.BtnRemove.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
            entry.BtnRemove.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(70, 30, 30), Color.FromArgb(250, 220, 220))
        End If
        If entry.BtnCopyUrl IsNot Nothing Then
            entry.BtnCopyUrl.ForeColor = IconRestColor()
            entry.BtnCopyUrl.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
            entry.BtnCopyUrl.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(35, 55, 80), Color.FromArgb(220, 235, 255))
        End If
        If entry.BtnCopyPath IsNot Nothing Then
            entry.BtnCopyPath.ForeColor = IconRestColor()
            entry.BtnCopyPath.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
            entry.BtnCopyPath.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(35, 55, 80), Color.FromArgb(220, 235, 255))
        End If
        If entry.BtnPlay IsNot Nothing Then
            entry.BtnPlay.ForeColor = IconRestColor()
            entry.BtnPlay.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
            entry.BtnPlay.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(40, 70, 40), Color.FromArgb(225, 245, 225))
        End If
        If entry.BtnDrag IsNot Nothing Then
            entry.BtnDrag.ForeColor = IconRestColor()
            entry.BtnDrag.BackColor = If(dark, Color.FromArgb(30, 30, 30), Color.White)
            entry.BtnDrag.FlatAppearance.MouseOverBackColor = If(dark, Color.FromArgb(35, 55, 80), Color.FromArgb(220, 235, 255))
        End If
    End Sub

    ' ═══════════════════════════════════════════════════════════════
    '  FEATURE 4 — TOAST FORM (nested class)
    ' ═══════════════════════════════════════════════════════════════

    Private Class ToastForm
        Inherits Form
        Public Sub New(title As String)
            Me.FormBorderStyle = FormBorderStyle.None
            Me.Size = New Size(320, 64)
            Me.StartPosition = FormStartPosition.Manual
            Me.TopMost = True
            Me.ShowInTaskbar = False
            Me.BackColor = Color.FromArgb(50, 50, 50)
            Dim wa = Screen.PrimaryScreen.WorkingArea
            Me.Location = New Point(wa.Right - Me.Width - 16, wa.Bottom - Me.Height - 16)
            Dim lbl As New Label() With {
                .Text = "✔  Download complete: " & If(title.Length > 38, title.Substring(0, 35) & "…", title),
                .Font = New Font("Segoe UI", 9),
                .ForeColor = Color.White,
                .AutoSize = False,
                .Size = New Size(300, 44),
                .Location = New Point(10, 10),
                .TextAlign = ContentAlignment.MiddleLeft
            }
            Me.Controls.Add(lbl)
            AddHandler Me.Click, Sub(s, ev) Me.Close()
            AddHandler lbl.Click, Sub(s, ev) Me.Close()
            Dim t As New System.Windows.Forms.Timer() With {.Interval = 4000}
            AddHandler t.Tick, Sub(s, ev)
                t.Stop()
                t.Dispose()
                If Not Me.IsDisposed Then Me.Close()
            End Sub
            t.Start()
        End Sub
    End Class

End Class
