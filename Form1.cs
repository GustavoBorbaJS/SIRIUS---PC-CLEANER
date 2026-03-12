private void ShowLicenseDialog()
{
    using (var form = new Form())
    {
        form.Text = "Gerenciar Licença";
        form.Size = new Size(450, 400);
        form.StartPosition = FormStartPosition.CenterParent;
        form.BackColor = AppStyles.BackgroundColor;

        string licenseMsg;
        bool isTrial;
        int daysLeft;
        DateTime expiryDate;
        AppMonitor.ValidateLicense(out licenseMsg, out isTrial, out daysLeft, out expiryDate);

        var lblStatus = new Label
        {
            Text = AppMonitor.GetLicenseStatus(),
            Location = new Point(20, 20),
            Size = new Size(400, 30),
            Font = AppStyles.BoldFont,
            ForeColor = isTrial && daysLeft <= 1 ? Color.Red : AppStyles.PrimaryColor
        };

        var lblExpiry = new Label
        {
            Text = isTrial ? $"Expira em: {expiryDate:dd/MM/yyyy} (restam {daysLeft} dias)" : "Licença perpétua",
            Location = new Point(20, 50),
            Size = new Size(400, 20),
            Font = AppStyles.NormalFont
        };

        // Seção de renovação do trial
        var lblRenew = new Label
        {
            Text = "RENOVAR TRIAL (7 DIAS)",
            Location = new Point(20, 90),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppStyles.SecondaryColor
        };

        var lblRenewInfo = new Label
        {
            Text = "Senha: An@lystadesystem",
            Location = new Point(20, 110),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Italic),
            ForeColor = Color.Gray
        };

        var txtRenew = new TextBox
        {
            Location = new Point(20, 135),
            Size = new Size(280, 25),
            Font = AppStyles.NormalFont,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };

        var btnRenew = new Button
        {
            Text = "Renovar Trial",
            Location = new Point(310, 134),
            Size = new Size(100, 28)
        };
        AppStyles.ApplyModernButton(btnRenew, AppStyles.SecondaryColor);
        btnRenew.Click += (s, e) =>
        {
            if (AppMonitor.RenewTrialWithPassword(txtRenew.Text))
            {
                MessageBox.Show("✅ Trial renovado por mais 7 dias!", "Sucesso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();
                this.Text = "Sirus Cleaner [MODO TRIAL]";
                form.Close();
            }
            else
            {
                MessageBox.Show("❌ Senha incorreta!", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // Seção de ativação permanente
        var lblPermanent = new Label
        {
            Text = "ATIVAR LICENÇA PERMANENTE",
            Location = new Point(20, 180),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppStyles.PrimaryColor
        };

        var lblPermanentInfo = new Label
        {
            Text = "Senha Master: Ayanokoji2026@",
            Location = new Point(20, 200),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Italic),
            ForeColor = Color.Gray
        };

        var txtPermanent = new TextBox
        {
            Location = new Point(20, 225),
            Size = new Size(280, 25),
            Font = AppStyles.NormalFont,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };

        var btnPermanent = new Button
        {
            Text = "Ativar Permanente",
            Location = new Point(310, 224),
            Size = new Size(100, 28)
        };
        AppStyles.ApplyModernButton(btnPermanent, AppStyles.PrimaryColor);
        btnPermanent.Click += (s, e) =>
        {
            if (AppMonitor.ActivatePermanentWithPassword(txtPermanent.Text))
            {
                MessageBox.Show("💎 Licença permanente ativada com sucesso!", "Sucesso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();
                this.Text = "Sirus Cleaner [PROFISSIONAL]";
                form.Close();
            }
            else
            {
                MessageBox.Show("❌ Senha master incorreta!", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // Opção de chave de licença (para vendas futuras)
        var lblKey = new Label
        {
            Text = "OU USE CHAVE DE LICENÇA:",
            Location = new Point(20, 270),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Color.Gray
        };

        var txtKey = new TextBox
        {
            Location = new Point(20, 295),
            Size = new Size(280, 25),
            Font = AppStyles.NormalFont
        };

        var btnKey = new Button
        {
            Text = "Ativar com Chave",
            Location = new Point(310, 294),
            Size = new Size(100, 28)
        };
        AppStyles.ApplyModernButton(btnKey, AppStyles.DarkColor);
        btnKey.Click += (s, e) =>
        {
            if (AppMonitor.ActivatePermanentWithKey(txtKey.Text.Trim()))
            {
                MessageBox.Show("💎 Licença ativada com sucesso!", "Sucesso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();
                this.Text = "Sirus Cleaner [PROFISSIONAL]";
                form.Close();
            }
            else
            {
                MessageBox.Show("❌ Chave inválida!", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var btnClose = new Button
        {
            Text = "Fechar",
            Location = new Point(330, 340),
            Size = new Size(100, 30),
            DialogResult = DialogResult.Cancel
        };
        AppStyles.ApplyModernButton(btnClose, AppStyles.AccentColor);

        form.Controls.AddRange(new Control[] { 
            lblStatus, lblExpiry,
            lblRenew, lblRenewInfo, txtRenew, btnRenew,
            lblPermanent, lblPermanentInfo, txtPermanent, btnPermanent,
            lblKey, txtKey, btnKey,
            btnClose 
        });

        form.ShowDialog();
    }
}