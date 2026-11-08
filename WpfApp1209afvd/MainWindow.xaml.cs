using System.Windows;

namespace WpfApp1209afvd
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Lab1Button_Click(object sender, RoutedEventArgs e)
        {
            Lab1Window lab1 = new Lab1Window();
            lab1.Show();
        }

        private void Lab2Button_Click(object sender, RoutedEventArgs e)
        {
            Lab2Window lab2 = new Lab2Window();
            lab2.Show();
        }

        private void Lab3Button_Click(object sender, RoutedEventArgs e)
        {
            Lab3Window lab3 = new Lab3Window();
            lab3.Show();
        }

        private void Lab4Button_Click(object sender, RoutedEventArgs e)
        {
            Lab4Window lab4 = new Lab4Window();
            lab4.Show();
        }
    }
}