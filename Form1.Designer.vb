<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        ' ── Controls ───────────────────────────────────────────────
        pnlHeader     = New Panel()
        lblAppIcon    = New Label()
        lblAppTitle   = New Label()
        lblParallel   = New Label()
        btnSettings   = New Button()
        pnlInput      = New Panel()
        tlpInput      = New TableLayoutPanel()
        txtUrl        = New TextBox()
        btnPaste      = New Button()
        btnBulk       = New Button()
        cboFormat     = New ComboBox()
        btnAdd        = New Button()
        pnlTabs       = New Panel()
        btnTabAll     = New Button()
        btnTabVideo   = New Button()
        btnTabAudio   = New Button()
        lblTabLine    = New Label()
        btnClearCompleted = New Button()
        pnlQueueOuter = New Panel()
        flpItems      = New FlowLayoutPanel()
        pnlEmpty      = New Panel()
        lblEmptyIcon  = New Label()
        lblEmptyText  = New Label()
        pnlStatus     = New Panel()
        lblStatusBar  = New Label()
        lblToolStatus = New Label()

        pnlHeader.SuspendLayout()
        pnlInput.SuspendLayout()
        tlpInput.SuspendLayout()
        pnlTabs.SuspendLayout()
        pnlQueueOuter.SuspendLayout()
        pnlEmpty.SuspendLayout()
        pnlStatus.SuspendLayout()
        Me.SuspendLayout()

        ' ════════════════════════════════════════════════════════
        ' HEADER
        ' ════════════════════════════════════════════════════════
        pnlHeader.BackColor = Color.FromArgb(18, 18, 18)
        pnlHeader.Dock = DockStyle.Top
        pnlHeader.Height = 52
        pnlHeader.Controls.AddRange(New Control() {lblAppIcon, lblAppTitle, lblParallel, btnSettings})

        lblAppIcon.Text = "▶"
        lblAppIcon.Font = New Font("Segoe UI", 18, FontStyle.Bold)
        lblAppIcon.ForeColor = Color.FromArgb(255, 40, 40)
        lblAppIcon.AutoSize = True
        lblAppIcon.Location = New Point(14, 10)

        lblAppTitle.Text = "YT Downloader"
        lblAppTitle.Font = New Font("Segoe UI", 15, FontStyle.Bold)
        lblAppTitle.ForeColor = Color.White
        lblAppTitle.AutoSize = True
        lblAppTitle.Location = New Point(50, 13)

        lblParallel.Text = ""
        lblParallel.Font = New Font("Segoe UI", 8)
        lblParallel.ForeColor = Color.FromArgb(150, 150, 150)
        lblParallel.AutoSize = True
        lblParallel.Location = New Point(232, 19)

        ' Settings button — positioned manually via RepositionHeaderControls()
        btnSettings.Text = "⚙  Settings"
        btnSettings.Font = New Font("Segoe UI", 9, FontStyle.Regular)
        btnSettings.ForeColor = Color.White
        btnSettings.FlatStyle = FlatStyle.Flat
        btnSettings.BackColor = Color.FromArgb(55, 55, 55)
        btnSettings.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90)
        btnSettings.FlatAppearance.BorderSize = 1
        btnSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 80, 80)
        btnSettings.Size = New Size(106, 30)
        btnSettings.Location = New Point(10, 11)   ' placeholder; real pos set on Shown
        btnSettings.Cursor = Cursors.Hand

        ' ════════════════════════════════════════════════════════
        ' INPUT PANEL  — uses TableLayoutPanel so nothing overlaps
        ' ════════════════════════════════════════════════════════
        pnlInput.BackColor = Color.FromArgb(240, 240, 240)
        pnlInput.Dock = DockStyle.Top
        pnlInput.Height = 58
        pnlInput.Padding = New Padding(10, 10, 10, 10)
        pnlInput.Controls.Add(tlpInput)

        ' TableLayoutPanel fills the panel
        tlpInput.Dock = DockStyle.Fill
        tlpInput.BackColor = Color.Transparent
        tlpInput.Padding = New Padding(0)
        tlpInput.Margin = New Padding(0)
        tlpInput.ColumnCount = 5
        tlpInput.RowCount = 1
        ' Column 0 = URL (Fill), Col 1 = Paste, Col 2 = Bulk, Col 3 = Format, Col 4 = Download (all Auto)
        tlpInput.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        tlpInput.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tlpInput.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tlpInput.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tlpInput.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tlpInput.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        tlpInput.Controls.Add(txtUrl, 0, 0)
        tlpInput.Controls.Add(btnPaste, 1, 0)
        tlpInput.Controls.Add(btnBulk, 2, 0)
        tlpInput.Controls.Add(cboFormat, 3, 0)
        tlpInput.Controls.Add(btnAdd, 4, 0)

        ' URL textbox
        txtUrl.Dock = DockStyle.Fill
        txtUrl.Font = New Font("Segoe UI", 10)
        txtUrl.PlaceholderText = "Paste YouTube or Instagram URL here…"
        txtUrl.BorderStyle = BorderStyle.FixedSingle
        txtUrl.BackColor = Color.White
        txtUrl.Margin = New Padding(0, 0, 6, 0)

        ' Paste button
        btnPaste.Text = "📋 Paste"
        btnPaste.Size = New Size(76, 30)
        btnPaste.Font = New Font("Segoe UI", 9)
        btnPaste.FlatStyle = FlatStyle.Flat
        btnPaste.BackColor = Color.FromArgb(220, 220, 220)
        btnPaste.ForeColor = Color.FromArgb(30, 30, 30)
        btnPaste.FlatAppearance.BorderSize = 0
        btnPaste.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200)
        btnPaste.Cursor = Cursors.Hand
        btnPaste.Margin = New Padding(0, 0, 6, 0)
        btnPaste.Anchor = AnchorStyles.None

        ' Bulk button — opens the paste-many-links popup
        btnBulk.Text = "☰  Bulk"
        btnBulk.Size = New Size(76, 30)
        btnBulk.Font = New Font("Segoe UI", 9)
        btnBulk.FlatStyle = FlatStyle.Flat
        btnBulk.BackColor = Color.FromArgb(220, 220, 220)
        btnBulk.ForeColor = Color.FromArgb(30, 30, 30)
        btnBulk.FlatAppearance.BorderSize = 0
        btnBulk.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200)
        btnBulk.Cursor = Cursors.Hand
        btnBulk.Margin = New Padding(0, 0, 6, 0)
        btnBulk.Anchor = AnchorStyles.None

        ' Format dropdown
        cboFormat.Size = New Size(155, 30)
        cboFormat.Font = New Font("Segoe UI", 9)
        cboFormat.DropDownStyle = ComboBoxStyle.DropDownList
        cboFormat.FlatStyle = FlatStyle.Flat
        cboFormat.Items.AddRange(New Object() {
            "★  Best Quality  (1080p / 4K)",
            "▶  1080p  Full HD",
            "▶  720p   HD",
            "▶  480p",
            "▶  360p",
            "♪  MP3  (Audio Only)",
            "♪  M4A  (Audio Only)",
            "▬  Video Only  (no audio)"
        })
        cboFormat.SelectedIndex = 0
        cboFormat.MinimumSize = New Size(195, 0)
        cboFormat.Margin = New Padding(0, 0, 6, 0)
        cboFormat.Anchor = AnchorStyles.None

        ' Download button
        btnAdd.Text = "＋  Download"
        btnAdd.Size = New Size(128, 30)
        btnAdd.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        btnAdd.FlatStyle = FlatStyle.Flat
        btnAdd.BackColor = Color.FromArgb(200, 0, 0)
        btnAdd.ForeColor = Color.White
        btnAdd.FlatAppearance.BorderSize = 0
        btnAdd.FlatAppearance.MouseOverBackColor = Color.FromArgb(170, 0, 0)
        btnAdd.Cursor = Cursors.Hand
        btnAdd.Margin = New Padding(0)
        btnAdd.Anchor = AnchorStyles.None

        ' ════════════════════════════════════════════════════════
        ' TABS
        ' ════════════════════════════════════════════════════════
        pnlTabs.BackColor = Color.White
        pnlTabs.Dock = DockStyle.Top
        pnlTabs.Height = 36
        pnlTabs.Controls.AddRange(New Control() {btnTabAll, btnTabVideo, btnTabAudio, lblTabLine, btnClearCompleted})

        btnTabAll.Text = "All"
        btnTabAll.Location = New Point(14, 0)
        btnTabAll.Size = New Size(64, 34)
        btnTabAll.FlatStyle = FlatStyle.Flat
        btnTabAll.FlatAppearance.BorderSize = 0
        btnTabAll.Font = New Font("Segoe UI", 9, FontStyle.Bold)
        btnTabAll.ForeColor = Color.FromArgb(200, 0, 0)
        btnTabAll.BackColor = Color.White
        btnTabAll.Cursor = Cursors.Hand
        btnTabAll.Tag = "All"

        btnTabVideo.Text = "Video"
        btnTabVideo.Location = New Point(80, 0)
        btnTabVideo.Size = New Size(64, 34)
        btnTabVideo.FlatStyle = FlatStyle.Flat
        btnTabVideo.FlatAppearance.BorderSize = 0
        btnTabVideo.Font = New Font("Segoe UI", 9)
        btnTabVideo.ForeColor = Color.FromArgb(80, 80, 80)
        btnTabVideo.BackColor = Color.White
        btnTabVideo.Cursor = Cursors.Hand
        btnTabVideo.Tag = "Video"

        btnTabAudio.Text = "Audio"
        btnTabAudio.Location = New Point(146, 0)
        btnTabAudio.Size = New Size(64, 34)
        btnTabAudio.FlatStyle = FlatStyle.Flat
        btnTabAudio.FlatAppearance.BorderSize = 0
        btnTabAudio.Font = New Font("Segoe UI", 9)
        btnTabAudio.ForeColor = Color.FromArgb(80, 80, 80)
        btnTabAudio.BackColor = Color.White
        btnTabAudio.Cursor = Cursors.Hand
        btnTabAudio.Tag = "Audio"

        lblTabLine.BackColor = Color.FromArgb(218, 218, 218)
        lblTabLine.Dock = DockStyle.Bottom
        lblTabLine.Height = 1

        ' Clear Completed button — right-aligned via RepositionTabControls()
        btnClearCompleted.Text = "Clear Completed"
        btnClearCompleted.Size = New Size(120, 28)
        btnClearCompleted.Font = New Font("Segoe UI", 8.5)
        btnClearCompleted.FlatStyle = FlatStyle.Flat
        btnClearCompleted.BackColor = Color.FromArgb(240, 240, 240)
        btnClearCompleted.ForeColor = Color.FromArgb(100, 100, 100)
        btnClearCompleted.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210)
        btnClearCompleted.FlatAppearance.BorderSize = 1
        btnClearCompleted.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 220)
        btnClearCompleted.Cursor = Cursors.Hand
        btnClearCompleted.Location = New Point(10, 4)   ' placeholder; real pos set by RepositionTabControls()

        ' ════════════════════════════════════════════════════════
        ' QUEUE AREA
        ' ════════════════════════════════════════════════════════
        pnlQueueOuter.BackColor = Color.FromArgb(246, 246, 246)
        pnlQueueOuter.Dock = DockStyle.Fill
        pnlQueueOuter.Controls.Add(flpItems)
        pnlQueueOuter.Controls.Add(pnlEmpty)

        flpItems.Dock = DockStyle.Fill
        flpItems.FlowDirection = FlowDirection.TopDown
        flpItems.WrapContents = False
        flpItems.AutoScroll = True
        flpItems.BackColor = Color.FromArgb(246, 246, 246)
        flpItems.Padding = New Padding(0, 4, 0, 4)
        flpItems.Visible = False

        pnlEmpty.Dock = DockStyle.Fill
        pnlEmpty.BackColor = Color.FromArgb(246, 246, 246)
        pnlEmpty.Controls.Add(lblEmptyText)
        pnlEmpty.Controls.Add(lblEmptyIcon)

        lblEmptyIcon.Text = "▶"
        lblEmptyIcon.Font = New Font("Segoe UI", 56, FontStyle.Bold)
        lblEmptyIcon.ForeColor = Color.FromArgb(215, 215, 215)
        lblEmptyIcon.AutoSize = True

        lblEmptyText.Text = "Paste a YouTube or Instagram URL above and click  ＋ Download"
        lblEmptyText.Font = New Font("Segoe UI", 11)
        lblEmptyText.ForeColor = Color.FromArgb(170, 170, 170)
        lblEmptyText.AutoSize = True

        ' ════════════════════════════════════════════════════════
        ' STATUS BAR
        ' ════════════════════════════════════════════════════════
        pnlStatus.BackColor = Color.FromArgb(232, 232, 232)
        pnlStatus.Dock = DockStyle.Bottom
        pnlStatus.Height = 24
        pnlStatus.Controls.AddRange(New Control() {lblStatusBar, lblToolStatus})

        lblStatusBar.Text = "Ready"
        lblStatusBar.Font = New Font("Segoe UI", 8)
        lblStatusBar.ForeColor = Color.FromArgb(80, 80, 80)
        lblStatusBar.AutoSize = True
        lblStatusBar.Location = New Point(10, 4)

        lblToolStatus.Text = ""
        lblToolStatus.Font = New Font("Segoe UI", 8)
        lblToolStatus.ForeColor = Color.FromArgb(60, 140, 60)
        lblToolStatus.AutoSize = True
        lblToolStatus.Anchor = AnchorStyles.Right Or AnchorStyles.Top
        lblToolStatus.Location = New Point(560, 4)

        ' ════════════════════════════════════════════════════════
        ' FORM
        ' ════════════════════════════════════════════════════════
        Me.Text = "YT Downloader"
        Me.ClientSize = New Size(860, 580)
        Me.MinimumSize = New Size(700, 460)
        Me.BackColor = Color.White
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Font = New Font("Segoe UI", 9)
        Me.Controls.Add(pnlQueueOuter)
        Me.Controls.Add(pnlTabs)
        Me.Controls.Add(pnlInput)
        Me.Controls.Add(pnlHeader)
        Me.Controls.Add(pnlStatus)

        pnlHeader.ResumeLayout(False)
        pnlInput.ResumeLayout(False)
        tlpInput.ResumeLayout(False)
        pnlTabs.ResumeLayout(False)
        pnlQueueOuter.ResumeLayout(False)
        pnlEmpty.ResumeLayout(False)
        pnlStatus.ResumeLayout(False)
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents pnlHeader As Panel
    Friend WithEvents lblAppIcon As Label
    Friend WithEvents lblAppTitle As Label
    Friend WithEvents lblParallel As Label
    Friend WithEvents btnSettings As Button
    Friend WithEvents pnlInput As Panel
    Friend WithEvents tlpInput As TableLayoutPanel
    Friend WithEvents txtUrl As TextBox
    Friend WithEvents btnPaste As Button
    Friend WithEvents btnBulk As Button
    Friend WithEvents cboFormat As ComboBox
    Friend WithEvents btnAdd As Button
    Friend WithEvents pnlTabs As Panel
    Friend WithEvents btnTabAll As Button
    Friend WithEvents btnTabVideo As Button
    Friend WithEvents btnTabAudio As Button
    Friend WithEvents lblTabLine As Label
    Friend WithEvents btnClearCompleted As Button
    Friend WithEvents pnlQueueOuter As Panel
    Friend WithEvents flpItems As FlowLayoutPanel
    Friend WithEvents pnlEmpty As Panel
    Friend WithEvents lblEmptyIcon As Label
    Friend WithEvents lblEmptyText As Label
    Friend WithEvents pnlStatus As Panel
    Friend WithEvents lblStatusBar As Label
    Friend WithEvents lblToolStatus As Label

End Class
