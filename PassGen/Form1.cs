using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Guncho;
using System.Web;
using Guncho.Api.Security;

namespace PassGen
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (textBox1.Text == "")
            {
                textBox2.Text = "";
            }
            else
            {
                string salt = OldTimeyPasswordHasher.GenerateSalt();
                string hash = OldTimeyPasswordHasher.HashPassword(salt, textBox1.Text);
                textBox2.Text = string.Format("pwdSalt=\"{0}\" pwdHash=\"{1}\"",
                    HttpUtility.HtmlAttributeEncode(salt),
                    HttpUtility.HtmlAttributeEncode(hash));
            }
        }
    }
}