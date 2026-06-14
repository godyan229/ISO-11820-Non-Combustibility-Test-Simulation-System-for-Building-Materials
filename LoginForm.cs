using ISO11820.Global;

namespace ISO11820.Forms;

public class LoginForm : Form
{
    private RadioButton _rbAdmin;
    private RadioButton _rbExperimenter;
    private TextBox _txtPassword;
    private Button _btnLogin;
    private Label _lblStatus;

    public LoginForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "ISO 11820 建筑材料不燃性试验系统 - 登录";
        this.Size = new Size(420, 300);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(240, 240, 240);

        var titleLabel = new Label
        {
            Text = "ISO 11820 不燃性试验系统",
            Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
            Location = new Point(80, 20),
            Size = new Size(260, 30),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var roleLabel = new Label
        {
            Text = "选择角色：",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(60, 70),
            Size = new Size(100, 25)
        };

        _rbAdmin = new RadioButton
        {
            Text = "管理员",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(160, 70),
            Size = new Size(80, 25),
            Checked = true
        };

        _rbExperimenter = new RadioButton
        {
            Text = "试验员",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(250, 70),
            Size = new Size(80, 25)
        };

        var pwdLabel = new Label
        {
            Text = "访问口令：",
            Font = new Font("Microsoft YaHei", 10),
            Location = new Point(60, 120),
            Size = new Size(100, 25)
        };

        _txtPassword = new TextBox
        {
            Location = new Point(160, 118),
            Size = new Size(170, 25),
            PasswordChar = '*',
            Font = new Font("Microsoft YaHei", 10)
        };

        _btnLogin = new Button
        {
            Text = "登 录",
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            Location = new Point(160, 170),
            Size = new Size(170, 35),
            BackColor = Color.FromArgb(0, 122, 204),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnLogin.Click += BtnLogin_Click;

        _lblStatus = new Label
        {
            Text = "",
            Font = new Font("Microsoft YaHei", 9),
            Location = new Point(60, 220),
            Size = new Size(300, 25),
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter
        };

        this.Controls.AddRange(new Control[] { titleLabel, roleLabel, _rbAdmin, _rbExperimenter,
            pwdLabel, _txtPassword, _btnLogin, _lblStatus });

        _txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, e); };
    }

    private void BtnLogin_Click(object? sender, EventArgs e)
    {
        var role = _rbAdmin.Checked ? "管理员" : "试验员";
        var pwd = _txtPassword.Text;

        var user = AppGlobal.Instance.Db.ValidateLogin(role, pwd);
        if (user != null)
        {
            AppGlobal.Instance.CurrentUser = user;
            _lblStatus.Text = "";
            var mainForm = new MainForm();
            mainForm.FormClosed += (s, args) => this.Show();
            this.Hide();
            mainForm.Show();
        }
        else
        {
            _lblStatus.Text = "密码错误，请重新输入";
            _txtPassword.SelectAll();
            _txtPassword.Focus();
        }
    }
}
