using Avalonia.Controls;
using Avalonia.Media;

namespace NetClassManage.Views
{
    public partial class NotificationCard : UserControl
    {
        private string _time = "";
        private string _title = "";
        private string _message = "";
        private int _priority = 0;

        public string Time
        {
            get => _time;
            set
            {
                _time = value;
                if (TimeText != null)
                    TimeText.Text = value;
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                if (TitleText != null)
                    TitleText.Text = value;
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                if (MessageText != null)
                    MessageText.Text = value;
            }
        }

        public int Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                UpdatePriorityBadge();
            }
        }

        public NotificationCard()
        {
            InitializeComponent();
            UpdatePriorityBadge();
        }

        public NotificationCard(string time, string title, string message, int priority) : this()
        {
            Time = time;
            Title = title;
            Message = message;
            Priority = priority;
        }

        private void UpdatePriorityBadge()
        {
            if (PriorityBadge == null || PriorityText == null) return;

            string text;
            Color color;

            switch (_priority)
            {
                case 1:
                    text = "重要";
                    color = Color.Parse("#FF8C00");
                    break;
                case 2:
                    text = "紧急";
                    color = Color.Parse("#E81123");
                    break;
                default:
                    text = "一般";
                    color = Color.Parse("#0078D4");
                    break;
            }

            PriorityText.Text = text;
            PriorityBadge.Background = new SolidColorBrush(color);
        }
    }
}