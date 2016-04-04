using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Windows.Forms.ExtendedControl
{
    /// <summary>
    /// プレースホルダー対応テキストボックス
    /// </summary>
    public class PlaceholdTextBox : System.Windows.Forms.TextBox
    {
        private String inputText;
        private System.Drawing.Color inputColor;
        private String placeholderText;
        private System.Drawing.Color placeholderColor;

        [System.ComponentModel.Browsable(false)]
        public Boolean IsEmpty { get; protected set; }

        [System.ComponentModel.Category("Appearance"),
         System.ComponentModel.Description("コントロールに関連付けされたテキストに何も入力されていない時に表示するテキストを取得または設定します。") ]
        public String PlaceholderText
        {
            get { return this.placeholderText; }
            set
            {
                this.placeholderText = value;
                this.UpdateText(base.Focused);
            }
        }

        [System.ComponentModel.Category("Appearance"),
         System.ComponentModel.Description("PlaceholderTextの前景色です。")]
        public System.Drawing.Color PlaceholderColor
        {
            get { return this.placeholderColor; }
            set
            {
                this.placeholderColor = value;
                this.UpdateText(base.Focused);
            }
        }


        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            this.PlaceholderColor = System.Drawing.Color.LightGray;
            this.inputColor = System.Drawing.SystemColors.WindowText;
            this.UpdateText(base.Focused);
        }


        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            base.Text = this.inputText;
            this.UpdateText(true);
        }


        protected override void OnLeave(EventArgs e)
        {
            this.inputText = base.Text;
            this.UpdateText(false);
            base.OnLeave(e);
        }


        public new void Clear()
        {
            base.Clear();
            this.inputText = "";
        }


        private void UpdateText(Boolean isFocused)
        {
            if (!isFocused && String.IsNullOrEmpty(this.inputText))
            {
                this.IsEmpty = true;
                base.Text = this.placeholderText;
                base.ForeColor = this.placeholderColor;
            }
            else
            {
                this.IsEmpty = false;
                base.Text = this.inputText;
                base.ForeColor = this.inputColor;
            }
        }
    }
}
