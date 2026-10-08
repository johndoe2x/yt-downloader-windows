Imports System.Drawing.Drawing2D
Imports System.Text.RegularExpressions

' ═══════════════════════════════════════════════════════════════════
'  BULK DOWNLOAD
'  BulkDialog  — the popup: paste links, pick quality + pace
'  BulkStrip   — the animated progress strip shown above the queue
'  Form1 (BULK section) owns the scheduler that paces the downloads.
' ═══════════════════════════════════════════════════════════════════

Public Enum BulkPace
    Gentle = 0
    Normal = 1
    Fast = 2
End Enum

Public Class BulkDialog
    Inherits Form

    ' Results — public fields (not properties) to avoid WFO1000 on .NET 10
    Public Urls As New List(Of String)
    Public FormatIndex As Integer = 0
    Public Pace As BulkPace = BulkPace.Gentle

    Private ReadOnly _isSupported As Func(Of String, Boolean)
    Private ReadOnly _alreadyQueued As HashSet(Of String)
    Private ReadOnly _maxParallel As Integer
    Private ReadOnly _dark As Boolean

    Private txtLinks As TextBox
    Private lblFound As Label
    Private lblFoundDetail As Label
    Private cboQuality As ComboBox
    Private rbGentle As RadioButton
    Private rbNormal As RadioButton
    Private rbFast As RadioButton
    Private lblPaceNote As Label
    Private btnStart As Button
    ' The pace follows the recommendation until the user picks one themselves.
    Private _paceTouched As Boolean = False
    Private _settingPace As Boolean = False

    Public Sub New(isSupported As Func(Of String, Boolean), alreadyQueued As IEnumerable(Of String),
                   formatIndex As Integer, maxParallel As Integer, dark As Boolean)
        _isSupported = isSupported
        _alreadyQueued = New HashSet(Of String)(alreadyQueued, StringComparer.OrdinalIgnoreCase)
        _maxParallel = maxParallel
        _dark = dark
        InitForm()
        cboQuality.SelectedIndex = Math.Max(0, Math.Min(cboQuality.Items.Count - 1, formatIndex))
        RefreshFound()
    End Sub

    ' Pull every supported link out of free text — a list, a chat message, anything.
    Public Shared Function ExtractUrls(text As String) As List(Of String)
        Dim found As New List(Of String)
        For Each m As Match In Regex.Matches(If(text, ""), "https?://[^\s""'<>]+", RegexOptions.IgnoreCase)
            found.Add(m.Value.TrimEnd("."c, ","c, ";"c, ")"c, "]"c, "!"c, "?"c))
        Next
        Return found
    End Function

    Private Sub InitForm()
        Dim bg = If(_dark, Color.FromArgb(30, 30, 30), Color.White)
        Dim fg = If(_dark, Color.White, Color.FromArgb(20, 20, 20))
        Dim muted = If(_dark, Color.FromArgb(150, 150, 150), Color.FromArgb(110, 110, 110))
        Dim fieldBg = If(_dark, Color.FromArgb(40, 40, 40), Color.FromArgb(250, 250, 250))
        Dim btnBg = If(_dark, Color.FromArgb(55, 55, 55), Color.FromArgb(235, 235, 235))

        Me.Text = "Bulk download"
        Me.ClientSize = New Size(620, 590)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = bg
        Me.ForeColor = fg
        Me.Font = New Font("Segoe UI", 9)

        Dim lblTitle As New Label() With {
            .Text = "Bulk download",
            .Font = New Font("Segoe UI", 14, FontStyle.Bold),
            .Location = New Point(20, 14), .AutoSize = True
        }
        Dim lblHint As New Label() With {
            .Text = "Paste as many YouTube or Instagram links as you like — one per line, or a whole" & vbCrLf &
                    "message. The links are picked out automatically; everything else is ignored.",
            .ForeColor = muted, .Location = New Point(22, 46), .AutoSize = True
        }

        txtLinks = New TextBox() With {
            .Multiline = True, .ScrollBars = ScrollBars.Vertical, .WordWrap = False,
            .AcceptsReturn = True,
            .Font = New Font("Consolas", 9.5),
            .Location = New Point(20, 84), .Size = New Size(580, 190),
            .BorderStyle = BorderStyle.FixedSingle,
            .BackColor = fieldBg, .ForeColor = fg,
            .PlaceholderText = "https://www.youtube.com/watch?v=…" & vbCrLf & "https://www.instagram.com/reel/…"
        }
        AddHandler txtLinks.TextChanged, Sub(s, e) RefreshFound()

        Dim btnPaste As New Button() With {
            .Text = "📋  Paste from clipboard", .Location = New Point(20, 282), .Size = New Size(170, 28),
            .FlatStyle = FlatStyle.Flat, .BackColor = btnBg, .ForeColor = fg, .Cursor = Cursors.Hand
        }
        btnPaste.FlatAppearance.BorderSize = 0
        AddHandler btnPaste.Click, Sub(s, e)
            If Not Clipboard.ContainsText() Then Return
            Dim clip = Clipboard.GetText().Trim()
            If txtLinks.Text.Trim() = "" Then
                txtLinks.Text = clip
            Else
                txtLinks.AppendText(vbCrLf & clip)
            End If
        End Sub

        Dim btnClear As New Button() With {
            .Text = "Clear", .Location = New Point(196, 282), .Size = New Size(64, 28),
            .FlatStyle = FlatStyle.Flat, .BackColor = btnBg, .ForeColor = fg, .Cursor = Cursors.Hand
        }
        btnClear.FlatAppearance.BorderSize = 0
        AddHandler btnClear.Click, Sub(s, e) txtLinks.Clear()

        lblFound = New Label() With {
            .Font = New Font("Segoe UI", 10, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleRight,
            .Location = New Point(270, 282), .Size = New Size(330, 28)
        }
        lblFoundDetail = New Label() With {
            .ForeColor = muted, .TextAlign = ContentAlignment.TopRight,
            .Location = New Point(20, 312), .Size = New Size(580, 18)
        }

        ' ── Quality ──────────────────────────────────────────
        Dim sep1 As New Label() With {
            .BackColor = If(_dark, Color.FromArgb(60, 60, 60), Color.FromArgb(230, 230, 230)),
            .Location = New Point(20, 338), .Size = New Size(580, 1)
        }
        Dim lblQuality As New Label() With {
            .Text = "Quality for every link", .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Location = New Point(20, 350), .AutoSize = True
        }
        cboQuality = New ComboBox() With {
            .DropDownStyle = ComboBoxStyle.DropDownList, .FlatStyle = FlatStyle.Flat,
            .Location = New Point(200, 346), .Size = New Size(230, 26),
            .BackColor = fieldBg, .ForeColor = fg
        }
        cboQuality.Items.AddRange(New Object() {
            "★  Best Quality  (1080p / 4K)", "▶  1080p  Full HD", "▶  720p   HD", "▶  480p", "▶  360p",
            "♪  MP3  (Audio Only)", "♪  M4A  (Audio Only)", "▬  Video Only  (no audio)"
        })

        ' ── Pace ─────────────────────────────────────────────
        Dim lblPace As New Label() With {
            .Text = "Pace", .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Location = New Point(20, 386), .AutoSize = True
        }
        rbGentle = MakePaceRadio("Gentle  —  1 at a time, 10 s apart.  Safest for long lists and Instagram.", 406, fg)
        rbNormal = MakePaceRadio("Normal  —  2 at a time, 4 s apart.", 430, fg)
        rbFast = MakePaceRadio($"Fast  —  {_maxParallel} at a time (your Settings), no gap.  Can trigger ""not a bot"" checks.", 454, fg)
        rbGentle.Checked = True

        lblPaceNote = New Label() With {
            .ForeColor = muted, .Location = New Point(22, 482), .Size = New Size(578, 38)
        }

        ' ── Buttons ──────────────────────────────────────────
        Dim btnCancel As New Button() With {
            .Text = "Cancel", .DialogResult = DialogResult.Cancel,
            .Location = New Point(360, 540), .Size = New Size(90, 34),
            .FlatStyle = FlatStyle.Flat, .BackColor = btnBg, .ForeColor = fg, .Cursor = Cursors.Hand
        }
        btnCancel.FlatAppearance.BorderSize = 0

        btnStart = New Button() With {
            .Location = New Point(458, 540), .Size = New Size(142, 34),
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .FlatStyle = FlatStyle.Flat, .BackColor = Color.FromArgb(200, 0, 0), .ForeColor = Color.White,
            .Cursor = Cursors.Hand
        }
        btnStart.FlatAppearance.BorderSize = 0
        btnStart.FlatAppearance.MouseOverBackColor = Color.FromArgb(170, 0, 0)
        AddHandler btnStart.Click, Sub(s, e)
            If Urls.Count = 0 Then Return
            FormatIndex = cboQuality.SelectedIndex
            Pace = SelectedPace()
            Me.DialogResult = DialogResult.OK
            Me.Close()
        End Sub

        Me.CancelButton = btnCancel
        Me.Controls.AddRange(New Control() {
            lblTitle, lblHint, txtLinks, btnPaste, btnClear, lblFound, lblFoundDetail,
            sep1, lblQuality, cboQuality, lblPace, rbGentle, rbNormal, rbFast, lblPaceNote,
            btnCancel, btnStart
        })

        ' Open with the clipboard already pasted when it holds links — one click saved.
        AddHandler Me.Shown, Sub(s, e)
            Try
                If txtLinks.Text = "" AndAlso Clipboard.ContainsText() Then
                    Dim clip = Clipboard.GetText()
                    If ExtractUrls(clip).Any(Function(u) _isSupported(u)) Then txtLinks.Text = clip.Trim()
                End If
            Catch
            End Try
            txtLinks.Focus()
            txtLinks.SelectionStart = txtLinks.TextLength
        End Sub
    End Sub

    Private Function MakePaceRadio(text As String, top As Integer, fg As Color) As RadioButton
        Dim rb As New RadioButton() With {
            .Text = text, .Location = New Point(22, top), .AutoSize = True, .ForeColor = fg, .Cursor = Cursors.Hand
        }
        AddHandler rb.CheckedChanged, Sub(s, e)
            If Not rb.Checked Then Return
            If Not _settingPace Then _paceTouched = True
            RefreshPaceNote()
        End Sub
        Return rb
    End Function

    Private Function SelectedPace() As BulkPace
        If rbFast.Checked Then Return BulkPace.Fast
        If rbNormal.Checked Then Return BulkPace.Normal
        Return BulkPace.Gentle
    End Function

    Private Sub RefreshFound()
        Dim all = ExtractUrls(txtLinks.Text)
        Urls.Clear()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim yt = 0, ig = 0, dupes = 0, queued = 0, unsupported = 0
        For Each u In all
            If Not _isSupported(u) Then
                unsupported += 1
            ElseIf Not seen.Add(u) Then
                dupes += 1
            ElseIf _alreadyQueued.Contains(u) Then
                queued += 1
            Else
                Urls.Add(u)
                If Regex.IsMatch(u, "instagr(am\.com|\.am)", RegexOptions.IgnoreCase) Then ig += 1 Else yt += 1
            End If
        Next

        Dim n = Urls.Count
        lblFound.Text = If(n = 0, "No links yet", $"{n} link{If(n = 1, "", "s")} ready")
        lblFound.ForeColor = If(n = 0, Color.Gray, Color.FromArgb(30, 150, 60))

        Dim bits As New List(Of String)
        If yt > 0 Then bits.Add($"{yt} YouTube")
        If ig > 0 Then bits.Add($"{ig} Instagram")
        If dupes > 0 Then bits.Add($"{dupes} duplicate{If(dupes = 1, "", "s")} skipped")
        If queued > 0 Then bits.Add($"{queued} already in your list, skipped")
        If unsupported > 0 Then bits.Add($"{unsupported} not YouTube/Instagram, ignored")
        lblFoundDetail.Text = String.Join("   ·   ", bits)

        ' Recommend a pace until the user picks one themselves.
        If Not _paceTouched Then
            _settingPace = True
            If ig > 0 OrElse n >= 10 Then
                rbGentle.Checked = True
            Else
                rbNormal.Checked = True
            End If
            _settingPace = False
        End If

        btnStart.Text = If(n = 0, "⬇  Download", $"⬇  Download {n}")
        btnStart.Enabled = n > 0
        RefreshPaceNote()
    End Sub

    Private Sub RefreshPaceNote()
        If lblPaceNote Is Nothing Then Return
        Dim n = Urls.Count
        Dim p = SelectedPace()
        Dim gap = BulkBatch.GapFor(p)
        Dim atOnce = BulkBatch.AtOnceFor(p, _maxParallel)
        Dim auto = If(_paceTouched, "", "Picked for you based on the links — change it any time.  ")
        Dim est = ""
        If n > 1 AndAlso gap > 0 Then
            Dim secs = CInt(Math.Ceiling((n - atOnce) / CDbl(atOnce))) * gap
            If secs > 0 Then est = $"Spacing alone adds about {FormatSpan(secs)} before the last link starts.  "
        End If
        lblPaceNote.Text = auto & est &
            "If a site says ""too many requests"", the batch pauses, waits, then carries on slower by itself."
    End Sub

    Public Shared Function FormatSpan(secs As Integer) As String
        If secs < 60 Then Return $"{secs} s"
        Dim m = secs \ 60, s = secs Mod 60
        If m < 60 Then Return If(s = 0, $"{m} min", $"{m} min {s} s")
        Return $"{m \ 60} h {m Mod 60} min"
    End Function
End Class

' ═══════════════════════════════════════════════════════════════════
'  BATCH STATE — the scheduler in Form1 reads and writes this
' ═══════════════════════════════════════════════════════════════════

Public Class BulkBatch
    Public Total As Integer
    Public Done As Integer
    Public Failed As Integer
    Public Skipped As Integer
    Public Running As Integer
    Public Waiting As Integer
    Public MaxAtOnce As Integer
    Public GapSeconds As Integer
    Public Pace As BulkPace
    Public Paused As Boolean
    Public Stopped As Boolean
    Public Finished As Boolean
    Public SlowedDown As Boolean
    Public NextStartUtc As DateTime = DateTime.MinValue
    Public CooldownUntilUtc As DateTime = DateTime.MinValue
    Public CooldownSeconds As Integer = 60
    Public RateLimitHits As Integer
    ' Sum of the running rows' progress (0–100 each), refreshed by Form1 every tick
    Public RunningPercentSum As Double

    Public Shared Function GapFor(p As BulkPace) As Integer
        Select Case p
            Case BulkPace.Gentle : Return 10
            Case BulkPace.Normal : Return 4
            Case Else : Return 0
        End Select
    End Function

    Public Shared Function AtOnceFor(p As BulkPace, maxParallel As Integer) As Integer
        Select Case p
            Case BulkPace.Gentle : Return 1
            Case BulkPace.Normal : Return Math.Min(2, maxParallel)
            Case Else : Return maxParallel
        End Select
    End Function

    Public ReadOnly Property Fraction As Double
        Get
            If Total <= 0 Then Return 0
            Return Math.Min(1.0, (Done + Failed + Skipped + RunningPercentSum / 100.0) / Total)
        End Get
    End Property

    Public ReadOnly Property InCooldown As Boolean
        Get
            Return DateTime.UtcNow < CooldownUntilUtc
        End Get
    End Property
End Class

' ═══════════════════════════════════════════════════════════════════
'  BULK STRIP — custom-painted, animated batch progress above the queue
' ═══════════════════════════════════════════════════════════════════

Public Class BulkStrip
    Inherits Panel

    Public Batch As BulkBatch = Nothing
    Public Dark As Boolean = False
    Public ReadOnly BtnPause As Button
    Public ReadOnly BtnStop As Button
    Public ReadOnly BtnClose As Button

    Private ReadOnly _anim As New System.Windows.Forms.Timer() With {.Interval = 33}
    Private _phase As Single = 0        ' drives the spinner and the moving stripes
    Private _shownFraction As Double = 0 ' eased toward the real fraction so the bar glides

    Public Sub New()
        Me.DoubleBuffered = True
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                    ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Me.Height = 62
        Me.Dock = DockStyle.Top
        Me.Visible = False

        BtnPause = MakeButton("Pause", 86)
        BtnStop = MakeButton("Stop", 70)
        BtnClose = MakeButton("Close", 70)
        BtnClose.Visible = False
        Dim tt As New ToolTip()
        tt.SetToolTip(BtnPause, "Pause starting new links. Ones already downloading keep going.")
        tt.SetToolTip(BtnStop, "Don't start any more links. Ones already downloading finish.")
        Me.Controls.AddRange(New Control() {BtnPause, BtnStop, BtnClose})

        AddHandler _anim.Tick, Sub(s, e)
            _phase += 1
            If Batch IsNot Nothing Then _shownFraction += (Batch.Fraction - _shownFraction) * 0.12
            Invalidate()
        End Sub
        AddHandler Me.Resize, Sub(s, e) LayoutButtons()
    End Sub

    Private Function MakeButton(text As String, w As Integer) As Button
        Dim b As New Button() With {
            .Text = text, .Size = New Size(w, 28), .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 8.5), .Cursor = Cursors.Hand
        }
        b.FlatAppearance.BorderSize = 1
        Return b
    End Function

    Public Sub StartAnimation()
        _shownFraction = 0
        Me.Visible = True
        BtnPause.Visible = True
        BtnStop.Visible = True
        BtnClose.Visible = False
        ApplyColors()
        LayoutButtons()
        _anim.Start()
    End Sub

    ' Called once the batch is over: stop spending CPU on frames nobody needs.
    Public Sub FinishAnimation()
        _shownFraction = If(Batch IsNot Nothing, Batch.Fraction, 1)
        BtnPause.Visible = False
        BtnStop.Visible = False
        BtnClose.Visible = True
        LayoutButtons()
        _anim.Stop()
        Invalidate()
    End Sub

    Public Sub ApplyColors()
        Me.BackColor = If(Dark, Color.FromArgb(26, 26, 26), Color.FromArgb(252, 248, 248))
        For Each b In {BtnPause, BtnStop, BtnClose}
            b.BackColor = If(Dark, Color.FromArgb(50, 50, 50), Color.White)
            b.ForeColor = If(Dark, Color.White, Color.FromArgb(40, 40, 40))
            b.FlatAppearance.BorderColor = If(Dark, Color.FromArgb(80, 80, 80), Color.FromArgb(210, 210, 210))
            b.FlatAppearance.MouseOverBackColor = If(Dark, Color.FromArgb(70, 70, 70), Color.FromArgb(240, 240, 240))
        Next
        Invalidate()
    End Sub

    Private Sub LayoutButtons()
        Dim x = ClientSize.Width - 12
        For Each b In {BtnClose, BtnStop, BtnPause}
            If Not b.Visible Then Continue For
            x -= b.Width
            b.Location = New Point(x, 8)
            x -= 6
        Next
    End Sub

    Private Function Accent() As Color
        If Batch Is Nothing Then Return Color.FromArgb(200, 0, 0)
        If Batch.Finished Then Return If(Batch.Failed > 0, Color.FromArgb(200, 130, 0), Color.FromArgb(30, 160, 60))
        If Batch.InCooldown Then Return Color.FromArgb(215, 140, 0)
        If Batch.Paused Then Return Color.FromArgb(130, 130, 130)
        Return Color.FromArgb(210, 20, 20)
    End Function

    Public Function HeadlineText() As String
        Dim b = Batch
        If b Is Nothing Then Return ""
        Dim parts As New List(Of String) From {$"{b.Done} of {b.Total} saved"}
        If b.Running > 0 Then parts.Add($"{b.Running} downloading")
        If b.Waiting > 0 Then parts.Add($"{b.Waiting} waiting")
        If b.Failed > 0 Then parts.Add($"{b.Failed} failed")
        If b.Skipped > 0 Then parts.Add($"{b.Skipped} skipped")
        Return "Bulk download  ·  " & String.Join("  ·  ", parts)
    End Function

    Public Function DetailText() As String
        Dim b = Batch
        If b Is Nothing Then Return ""
        If b.Finished Then
            If b.Stopped AndAlso b.Skipped > 0 Then
                Return $"Stopped — {b.Skipped} link(s) not started. Press ↻ on a row to start one."
            End If
            If b.Failed > 0 Then Return $"Finished — {b.Failed} failed. Hover a red row to see why, or press ↻ on it."
            Return "All done."
        End If
        If b.InCooldown Then
            Dim left = CInt(Math.Ceiling((b.CooldownUntilUtc - DateTime.UtcNow).TotalSeconds))
            Return $"The site said ""too many requests"" — waiting {left} s, then continuing " &
                   $"{b.MaxAtOnce} at a time, {b.GapSeconds} s apart."
        End If
        If b.Paused Then Return "Paused — no new links start until you press Resume."
        Dim pace = $"{b.Pace} pace: {b.MaxAtOnce} at a time, {b.GapSeconds} s apart"
        If b.SlowedDown Then pace = $"Slowed down after a rate limit: {b.MaxAtOnce} at a time, {b.GapSeconds} s apart"
        If b.Waiting > 0 Then
            Dim wait = (b.NextStartUtc - DateTime.UtcNow).TotalSeconds
            If b.Running >= b.MaxAtOnce Then Return $"{pace}.  Next link starts when a slot frees up."
            If wait > 0.5 Then Return $"{pace}.  Next link starts in {CInt(Math.Ceiling(wait))} s."
            Return $"{pace}.  Starting next link…"
        End If
        Return $"{pace}.  Last links finishing…"
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If Batch Is Nothing Then Return
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim accent = Me.Accent()
        Dim fg = If(Dark, Color.White, Color.FromArgb(20, 20, 20))
        Dim muted = If(Dark, Color.FromArgb(160, 160, 160), Color.FromArgb(105, 105, 105))
        Dim buttonsLeft = If(BtnClose.Visible, BtnClose.Left, BtnPause.Left)

        ' ── Status icon: spinner / pause bars / hourglass ring / check ──
        Dim icon As New Rectangle(14, 12, 24, 24)
        Using pen As New Pen(accent, 3.2F) With {.StartCap = LineCap.Round, .EndCap = LineCap.Round}
            If Batch.Finished Then
                g.DrawEllipse(pen, icon)
                If Batch.Failed = 0 Then
                    g.DrawLines(pen, {New PointF(20, 24), New PointF(25, 29), New PointF(33, 19)})
                Else
                    g.DrawLine(pen, 26, 18, 26, 25)
                    g.DrawLine(pen, 26, 30, 26, 30.5F)
                End If
            ElseIf Batch.Paused Then
                g.DrawLine(pen, 21, 17, 21, 31)
                g.DrawLine(pen, 31, 17, 31, 31)
            ElseIf Batch.InCooldown Then
                ' Ring that empties as the cooldown runs out
                Using track As New Pen(Color.FromArgb(60, accent), 3.2F)
                    g.DrawEllipse(track, icon)
                End Using
                Dim total = Math.Max(1, Batch.CooldownSeconds)
                Dim left = Math.Max(0, (Batch.CooldownUntilUtc - DateTime.UtcNow).TotalSeconds)
                g.DrawArc(pen, icon, -90, CSng(360 * left / total))
            Else
                Using track As New Pen(Color.FromArgb(45, accent), 3.2F)
                    g.DrawEllipse(track, icon)
                End Using
                g.DrawArc(pen, icon, (_phase * 11) Mod 360, 100 + CSng(Math.Sin(_phase / 9) * 60))
            End If
        End Using

        ' ── Text ──
        Dim textW = Math.Max(50, buttonsLeft - 52 - 10)
        Using f As New Font("Segoe UI", 9.5, FontStyle.Bold), br As New SolidBrush(fg)
            TextRenderer.DrawText(g, HeadlineText(), f, New Rectangle(48, 6, textW, 20), fg,
                                  TextFormatFlags.EndEllipsis Or TextFormatFlags.Left)
        End Using
        Using f As New Font("Segoe UI", 8.5)
            TextRenderer.DrawText(g, DetailText(), f, New Rectangle(48, 25, textW, 18), muted,
                                  TextFormatFlags.EndEllipsis Or TextFormatFlags.Left)
        End Using

        ' ── Progress bar with moving stripes ──
        Dim bar As New Rectangle(48, 47, ClientSize.Width - 48 - 14, 8)
        If bar.Width < 20 Then Return
        Dim trackColor = If(Dark, Color.FromArgb(55, 55, 55), Color.FromArgb(232, 225, 225))
        Using path = RoundRect(bar, 4), br As New SolidBrush(trackColor)
            g.FillPath(br, path)
        End Using
        Dim fillW = CInt(bar.Width * Math.Max(0, Math.Min(1, _shownFraction)))
        If fillW >= 4 Then
            Dim fillRect As New Rectangle(bar.X, bar.Y, fillW, bar.Height)
            Using path = RoundRect(fillRect, 4)
                Using br As New SolidBrush(accent)
                    g.FillPath(br, path)
                End Using
                If Not Batch.Finished AndAlso Not Batch.Paused Then
                    Dim oldClip = g.Clip
                    g.SetClip(path)
                    Using stripe As New SolidBrush(Color.FromArgb(60, Color.White))
                        Dim offset = CInt(_phase * 0.8) Mod 16
                        For x = bar.X - 16 + offset To bar.X + fillW Step 16
                            g.FillPolygon(stripe, {New Point(x, bar.Bottom), New Point(x + 8, bar.Y),
                                                   New Point(x + 14, bar.Y), New Point(x + 6, bar.Bottom)})
                        Next
                    End Using
                    g.Clip = oldClip
                End If
            End Using
        End If

        ' Percentage at the right end of the text line
        Dim pctText = $"{CInt(Math.Floor(_shownFraction * 100))}%"
        Using f As New Font("Segoe UI", 9, FontStyle.Bold)
            Dim sz = TextRenderer.MeasureText(pctText, f)
            TextRenderer.DrawText(g, pctText, f, New Point(buttonsLeft - sz.Width - 8, 25), accent)
        End Using

        ' Bottom separator
        Using pen As New Pen(If(Dark, Color.FromArgb(45, 45, 45), Color.FromArgb(225, 215, 215)))
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1)
        End Using
    End Sub

    Private Shared Function RoundRect(r As Rectangle, radius As Integer) As GraphicsPath
        Dim p As New GraphicsPath()
        Dim d = Math.Min(radius * 2, Math.Min(r.Width, r.Height))
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        p.CloseFigure()
        Return p
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then _anim.Dispose()
        MyBase.Dispose(disposing)
    End Sub
End Class
