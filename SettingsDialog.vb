Public Class SettingsDialog
    Inherits Form

    Public DownloadPath As String = ""
    Public MaxParallel As Integer = 3
    Public AutoUpdateOnStart As Boolean = True
    Public NotifyOnComplete As Boolean = True
    Public ClipboardMonitor As Boolean = False
    Public DarkMode As Boolean = False
    Public CookiesBrowser As String = ""
    Public CookiesFile As String = ""

    Private txtPath As TextBox
    Private nudParallel As NumericUpDown
    Private chkAutoUpdate As CheckBox
    Private chkNotify As CheckBox
    Private chkClipboard As CheckBox
    Private chkDarkMode As CheckBox
    Private cboBrowser As ComboBox
    Private txtCookiesFile As TextBox

    ' yt-dlp browser keys matching the ComboBox index
    Private Shared ReadOnly BrowserKeys As String() = {"", "chrome", "edge", "firefox", "brave", "opera", "chromium"}

    Public Sub New(path As String, parallel As Integer, autoUpdate As Boolean,
                   notify As Boolean, clipMonitor As Boolean, darkMode As Boolean,
                   cookiesBrowser As String, cookiesFile As String)
        InitForm()
        txtPath.Text = path
        nudParallel.Value = Math.Max(1, Math.Min(10, parallel))
        chkAutoUpdate.Checked = autoUpdate
        chkNotify.Checked = notify
        chkClipboard.Checked = clipMonitor
        chkDarkMode.Checked = darkMode
        Dim idx = Array.IndexOf(BrowserKeys, cookiesBrowser.ToLower())
        cboBrowser.SelectedIndex = If(idx < 0, 0, idx)
        txtCookiesFile.Text = cookiesFile
    End Sub

    Private Sub InitForm()
        Me.Text = "Settings"
        Me.ClientSize = New Size(440, 590)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.StartPosition = FormStartPosition.CenterParent
        Me.BackColor = Color.White
        Me.Font = New Font("Segoe UI", 9)

        ' ── Download folder ──────────────────────────────────────
        Dim lblFolder As New Label() With {
            .Text = "Download Folder",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Location = New Point(20, 20),
            .AutoSize = True
        }

        txtPath = New TextBox() With {
            .Location = New Point(20, 42),
            .Size = New Size(310, 28),
            .Font = New Font("Segoe UI", 9),
            .ReadOnly = True,
            .BackColor = Color.FromArgb(245, 245, 245),
            .BorderStyle = BorderStyle.FixedSingle,
            .Cursor = Cursors.Hand
        }

        Dim btnBrowse As New Button() With {
            .Text = "Browse…",
            .Location = New Point(338, 41),
            .Size = New Size(84, 28),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(240, 240, 240),
            .Font = New Font("Segoe UI", 9),
            .Cursor = Cursors.Hand
        }
        btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200)
        AddHandler btnBrowse.Click, Sub(s, e)
            Using dlg As New FolderBrowserDialog()
                dlg.Description = "Choose download folder"
                dlg.SelectedPath = txtPath.Text
                If dlg.ShowDialog() = DialogResult.OK Then
                    txtPath.Text = dlg.SelectedPath
                End If
            End Using
        End Sub
        AddHandler txtPath.Click, Sub(s, e) btnBrowse.PerformClick()

        ' ── Parallel downloads ───────────────────────────────────
        Dim sep1 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 86),
            .Size = New Size(402, 1)
        }

        Dim lblParallel As New Label() With {
            .Text = "Max Parallel Downloads",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Location = New Point(20, 100),
            .AutoSize = True
        }

        Dim lblParallelHint As New Label() With {
            .Text = "How many videos download at the same time (1–5 recommended)",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.Gray,
            .Location = New Point(20, 120),
            .AutoSize = True
        }

        nudParallel = New NumericUpDown() With {
            .Location = New Point(20, 142),
            .Size = New Size(80, 28),
            .Font = New Font("Segoe UI", 10),
            .Minimum = 1,
            .Maximum = 10,
            .Value = 3,
            .BorderStyle = BorderStyle.FixedSingle
        }

        ' ── Auto-update ─────────────────────────────────────────
        Dim sep2 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 182),
            .Size = New Size(402, 1)
        }

        chkAutoUpdate = New CheckBox() With {
            .Text = "Check for yt-dlp and ffmpeg updates on startup",
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(20, 196),
            .AutoSize = True,
            .Checked = True
        }

        ' ── Notify on complete ───────────────────────────────────
        Dim sep3 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 232),
            .Size = New Size(402, 1)
        }
        chkNotify = New CheckBox() With {
            .Text = "Show notification when a download completes",
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(20, 246),
            .AutoSize = True,
            .Checked = True
        }

        ' ── Clipboard monitor ────────────────────────────────────
        Dim sep4 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 278),
            .Size = New Size(402, 1)
        }
        chkClipboard = New CheckBox() With {
            .Text = "Monitor clipboard for YouTube / Instagram URLs (auto-fill URL box)",
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(20, 292),
            .AutoSize = True,
            .Checked = False
        }

        ' ── Dark mode ────────────────────────────────────────────
        Dim sep5 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 324),
            .Size = New Size(402, 1)
        }
        chkDarkMode = New CheckBox() With {
            .Text = "Dark mode",
            .Font = New Font("Segoe UI", 9),
            .Location = New Point(20, 338),
            .AutoSize = True,
            .Checked = False
        }

        ' ── Browser cookies ──────────────────────────────────────
        Dim sep6 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 370),
            .Size = New Size(402, 1)
        }
        Dim lblBrowser As New Label() With {
            .Text = "Browser for YouTube / Instagram sign-in cookies",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Location = New Point(20, 384),
            .AutoSize = True
        }
        Dim lblBrowserHint As New Label() With {
            .Text = "Needed for age-restricted YouTube videos and private or login-walled Instagram posts",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.Gray,
            .Location = New Point(20, 404),
            .AutoSize = True
        }
        cboBrowser = New ComboBox() With {
            .Location = New Point(20, 424),
            .Size = New Size(200, 28),
            .Font = New Font("Segoe UI", 9),
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .FlatStyle = FlatStyle.Flat
        }
        cboBrowser.Items.AddRange(New Object() {
            "None (public videos only)",
            "Chrome",
            "Microsoft Edge",
            "Firefox",
            "Brave",
            "Opera",
            "Chromium"
        })
        cboBrowser.SelectedIndex = 0

        ' ── Cookies.txt file ─────────────────────────────────────
        Dim sep7 As New Label() With {
            .BackColor = Color.FromArgb(230, 230, 230),
            .Location = New Point(20, 460),
            .Size = New Size(402, 1)
        }
        Dim lblCookiesFile As New Label() With {
            .Text = "YouTube Cookies File (cookies.txt)",
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Location = New Point(20, 474),
            .AutoSize = True
        }
        Dim lblCookiesHint As New Label() With {
            .Text = "Export from Chrome using 'Get cookies.txt LOCALLY' extension",
            .Font = New Font("Segoe UI", 8),
            .ForeColor = Color.Gray,
            .Location = New Point(20, 494),
            .AutoSize = True
        }
        txtCookiesFile = New TextBox() With {
            .Location = New Point(20, 514),
            .Size = New Size(310, 28),
            .Font = New Font("Segoe UI", 9),
            .ReadOnly = True,
            .BackColor = Color.FromArgb(245, 245, 245),
            .BorderStyle = BorderStyle.FixedSingle,
            .Cursor = Cursors.Hand
        }
        Dim btnBrowseCookies As New Button() With {
            .Text = "Browse…",
            .Location = New Point(338, 513),
            .Size = New Size(84, 28),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(240, 240, 240),
            .Font = New Font("Segoe UI", 9),
            .Cursor = Cursors.Hand
        }
        btnBrowseCookies.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200)
        AddHandler btnBrowseCookies.Click, Sub(s, e)
            Using dlg As New OpenFileDialog()
                dlg.Title = "Select cookies.txt file"
                dlg.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
                If dlg.ShowDialog() = DialogResult.OK Then
                    txtCookiesFile.Text = dlg.FileName
                End If
            End Using
        End Sub
        AddHandler txtCookiesFile.Click, Sub(s, e) btnBrowseCookies.PerformClick()

        ' ── Buttons ──────────────────────────────────────────────
        Dim btnOK As New Button() With {
            .Text = "Save",
            .Location = New Point(248, 550),
            .Size = New Size(80, 30),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(200, 0, 0),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Cursor = Cursors.Hand
        }
        btnOK.FlatAppearance.BorderSize = 0
        AddHandler btnOK.Click, Sub(s, e)
            If txtPath.Text <> "" AndAlso Not System.IO.Directory.Exists(txtPath.Text) Then
                MessageBox.Show("The folder does not exist. Please choose a valid folder.",
                                "Invalid Folder", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            DownloadPath = txtPath.Text
            MaxParallel = CInt(nudParallel.Value)
            AutoUpdateOnStart = chkAutoUpdate.Checked
            NotifyOnComplete = chkNotify.Checked
            ClipboardMonitor = chkClipboard.Checked
            DarkMode = chkDarkMode.Checked
            CookiesBrowser = BrowserKeys(cboBrowser.SelectedIndex)
            CookiesFile = txtCookiesFile.Text.Trim()
            Me.DialogResult = DialogResult.OK
            Me.Close()
        End Sub

        Dim btnCancel As New Button() With {
            .Text = "Cancel",
            .Location = New Point(338, 550),
            .Size = New Size(80, 30),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(240, 240, 240),
            .Font = New Font("Segoe UI", 9),
            .Cursor = Cursors.Hand
        }
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200)
        AddHandler btnCancel.Click, Sub(s, e)
            Me.DialogResult = DialogResult.Cancel
            Me.Close()
        End Sub

        Me.Controls.AddRange(New Control() {
            lblFolder, txtPath, btnBrowse,
            sep1, lblParallel, lblParallelHint, nudParallel,
            sep2, chkAutoUpdate,
            sep3, chkNotify, sep4, chkClipboard, sep5, chkDarkMode,
            sep6, lblBrowser, lblBrowserHint, cboBrowser,
            sep7, lblCookiesFile, lblCookiesHint, txtCookiesFile, btnBrowseCookies,
            btnOK, btnCancel
        })

        Me.AcceptButton = btnOK
        Me.CancelButton = btnCancel
    End Sub

End Class
