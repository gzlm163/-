using NCalc;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WpfApp1209afvd
{
    public partial class Lab3Window : Window
    {
        private const double PlotMargin = 30;

        public Lab3Window()
        {
            InitializeComponent();
        }

        private bool TryParseFunction(string formula, out Func<double, double> function, out string error)
        {
            function = null;
            error = null;

            if (string.IsNullOrWhiteSpace(formula))
            {
                error = "Формула не введена";
                return false;
            }

            string preparedFormula = formula.Replace("^", "**");
            string[] mathFunctions = { "sin", "cos", "tan", "asin", "acos", "atan", "sqrt", "abs", "exp", "log", "ln" };
            foreach (string func in mathFunctions)
            {
                string properName = char.ToUpper(func[0]) + func.Substring(1);
                preparedFormula = Regex.Replace(preparedFormula, @"\b" + func + @"\b", properName, RegexOptions.IgnoreCase);
            }

            try
            {
                var testExpression = new NCalc.Expression(preparedFormula);
                testExpression.Parameters["x"] = 1.0;
                if (testExpression.Evaluate() == null)
                {
                    error = "Ошибка вычисления формулы";
                    return false;
                }

                function = (double argumentX) =>
                {
                    try
                    {
                        var expression = new NCalc.Expression(preparedFormula);
                        expression.Parameters["x"] = argumentX;
                        var result = expression.Evaluate();
                        return result == null ? double.NaN : Convert.ToDouble(result);
                    }
                    catch { return double.NaN; }
                };

                return true;
            }
            catch (Exception ex)
            {
                error = "Синтаксическая ошибка: " + ex.Message;
                return false;
            }
        }

        private bool TryParseDouble(string inputText, string fieldName, out double parsedValue, out string error)
        {
            parsedValue = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(inputText))
            {
                error = "Поле \"" + fieldName + "\" пустое";
                return false;
            }

            string cleanedText = inputText.Trim().Replace('.', ',');

            if (!double.TryParse(cleanedText, out parsedValue))
            {
                error = "Поле \"" + fieldName + "\" не является числом";
                return false;
            }
            return true;
        }

        private void FindIsolationInterval(Func<double, double> function, double start, double end, bool findMax, out double left, out double right, out string error)
        {
            left = start;
            right = end;
            error = null;

            double h = (end - start) / 100.0;
            double x = start;
            double fx = function(x);

            while (x + h <= end)
            {
                double xNext = x + h;
                double fxNext = function(xNext);

                bool found = findMax ? (fxNext < fx) : (fxNext > fx);

                if (found)
                {
                    left = x - h;
                    right = xNext;
                    return;
                }

                x = xNext;
                fx = fxNext;
            }

            error = "На отрезке нет экстремума (функция монотонна)";
        }

        private double GoldenSectionSearch(Func<double, double> function, double left, double right, double accuracy, bool findMax)
        {
            double alpha = (3.0 - Math.Sqrt(5.0)) / 2.0;

            double x1 = left + alpha * (right - left);
            double x2 = right - alpha * (right - left);

            Func<double, double> f = findMax ? (x => -function(x)) : function;

            double f1 = f(x1);
            double f2 = f(x2);

            while (Math.Abs(right - left) > accuracy)
            {
                if (f1 < f2)
                {
                    right = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = left + alpha * (right - left);
                    f1 = f(x1);
                }
                else
                {
                    left = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = right - alpha * (right - left);
                    f2 = f(x2);
                }
            }

            return (left + right) / 2;
        }

        private double FindRoot(Func<double, double> function, double left, double right, double accuracy)
        {
            return GoldenSectionSearch(x => Math.Abs(function(x)), left, right, accuracy, false);
        }

        private void DrawPlot(Func<double, double> function, double left, double right, double? specialPointX, string pointType)
        {
            PlotCanvas.Children.Clear();

            double canvasWidth = PlotCanvas.ActualWidth;
            double canvasHeight = PlotCanvas.ActualHeight;
            if (canvasWidth <= 2 * PlotMargin || canvasHeight <= 2 * PlotMargin) return;

            int pointCount = 500;
            double minValueY = double.MaxValue;
            double maxValueY = double.MinValue;

            for (int i = 0; i <= pointCount; ++i)
            {
                double x = left + (right - left) * i / pointCount;
                double y = function(x);
                if (!double.IsNaN(y) && !double.IsInfinity(y) && Math.Abs(y) < 1e6)
                {
                    if (y < minValueY) minValueY = y;
                    if (y > maxValueY) maxValueY = y;
                }
            }

            if (minValueY > maxValueY) { minValueY = -1; maxValueY = 1; }
            double rangeY = maxValueY - minValueY;
            if (rangeY < 1e-9) rangeY = 1;
            minValueY -= rangeY * 0.1;
            maxValueY += rangeY * 0.1;

            double plotWidth = canvasWidth - 2 * PlotMargin;
            double plotHeight = canvasHeight - 2 * PlotMargin;

            Func<double, double> mapToCanvasX = mathX => PlotMargin + (mathX - left) / (right - left) * plotWidth;
            Func<double, double> mapToCanvasY = mathY => canvasHeight - PlotMargin - (mathY - minValueY) / (maxValueY - minValueY) * plotHeight;

            double axisY = mapToCanvasY(0);
            if (axisY < PlotMargin) axisY = PlotMargin;
            if (axisY > canvasHeight - PlotMargin) axisY = canvasHeight - PlotMargin;

            Line xAxis = new Line { X1 = PlotMargin, Y1 = axisY, X2 = canvasWidth - PlotMargin, Y2 = axisY, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(xAxis);

            double axisX = mapToCanvasX(0);
            if (axisX < PlotMargin) axisX = PlotMargin;
            if (axisX > canvasWidth - PlotMargin) axisX = canvasWidth - PlotMargin;

            Line yAxis = new Line { X1 = axisX, Y1 = PlotMargin, X2 = axisX, Y2 = canvasHeight - PlotMargin, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(yAxis);

            AddAxisLabel(left.ToString("F2"), mapToCanvasX(left), canvasHeight - PlotMargin + 5);
            AddAxisLabel(right.ToString("F2"), mapToCanvasX(right) - 30, canvasHeight - PlotMargin + 5);

            Polyline line = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0x8E, 0x24, 0xAA)), StrokeThickness = 2 };

            for (int i = 0; i <= pointCount; ++i)
            {
                double x = left + (right - left) * i / pointCount;
                double y = function(x);

                if (double.IsNaN(y) || double.IsInfinity(y) || Math.Abs(y) > 1e6)
                {
                    if (line.Points.Count > 0)
                    {
                        PlotCanvas.Children.Add(line);
                        line = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0x8E, 0x24, 0xAA)), StrokeThickness = 2 };
                    }
                    continue;
                }
                line.Points.Add(new Point(mapToCanvasX(x), mapToCanvasY(y)));
            }
            if (line.Points.Count > 0) PlotCanvas.Children.Add(line);

            if (specialPointX.HasValue)
            {
                double px = mapToCanvasX(specialPointX.Value);
                double py = mapToCanvasY(function(specialPointX.Value));

                Ellipse marker = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0x14, 0x8C)), Stroke = Brushes.White, StrokeThickness = 1 };
                Canvas.SetLeft(marker, px - 6);
                Canvas.SetTop(marker, py - 6);
                PlotCanvas.Children.Add(marker);

                TextBlock label = new TextBlock { Text = pointType, Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 12 };
                Canvas.SetLeft(label, px + 10);
                Canvas.SetTop(label, py - 10);
                PlotCanvas.Children.Add(label);
            }
        }

        private void AddAxisLabel(string text, double xPosition, double yPosition)
        {
            TextBlock axisLabel = new TextBlock { Text = text, Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 11 };
            Canvas.SetLeft(axisLabel, xPosition);
            Canvas.SetTop(axisLabel, yPosition);
            PlotCanvas.Children.Add(axisLabel);
        }

        private void ButtonPlot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double a, out double b, out double accuracy)) return;
            DrawPlot(function, a, b, null, "");
        }

        private void ButtonFindExtremum_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double a, out double b, out double accuracy)) return;

            bool isMax = RadioMax.IsChecked == true;

            FindIsolationInterval(function, a, b, isMax, out double left, out double right, out string isolationError);

            if (isolationError != null)
            {
                TextBoxResult.Text = isolationError;
                return;
            }

            double extremumX = GoldenSectionSearch(function, left, right, accuracy, isMax);
            double extremumY = function(extremumX);

            string type = isMax ? "Максимум" : "Минимум";
            TextBoxResult.Text = $"{type}:\r\nx = {extremumX:F6}\r\ny = {extremumY:F6}";
            DrawPlot(function, a, b, extremumX, type);
        }

        private void ButtonFindRoot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out Func<double, double> function, out double a, out double b, out double accuracy)) return;

            double rootX = FindRoot(function, a, b, accuracy);
            double rootY = function(rootX);

            if (Math.Abs(rootY) > accuracy)
            {
                TextBoxResult.Text = "Корень не найден (функция не пересекает ноль)";
                DrawPlot(function, a, b, null, "");
                return;
            }

            TextBoxResult.Text = $"Корень:\r\nx = {rootX:F6}\r\ny = {rootY:F6}";
            DrawPlot(function, a, b, rootX, "Корень");
        }

        private bool ValidateInputs(out Func<double, double> function, out double a, out double b, out double accuracy)
        {
            function = null; a = 0; b = 0; accuracy = 0;

            if (!TryParseFunction(TextBoxFunction.Text, out function, out string fError)) { MessageBox.Show(fError); return false; }
            if (!TryParseDouble(TextBoxA.Text, "a", out a, out string aError)) { MessageBox.Show(aError); return false; }
            if (!TryParseDouble(TextBoxB.Text, "b", out b, out string bError)) { MessageBox.Show(bError); return false; }

            if (!int.TryParse(TextBoxE.Text, out int decimalPlaces)) { MessageBox.Show("Количество знаков целое число"); return false; }
            if (decimalPlaces <= 0) { MessageBox.Show("Количество знаков > 0"); return false; }

            accuracy = 1.0 / Math.Pow(10, decimalPlaces);

            if (a >= b) { MessageBox.Show("a должно быть меньше b"); return false; }
            if (accuracy >= b - a) { MessageBox.Show("Слишком высокая точность"); return false; }

            return true;
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            TextBoxFunction.Text = string.Empty;
            TextBoxA.Text = string.Empty;
            TextBoxB.Text = string.Empty;
            TextBoxE.Text = string.Empty;
            TextBoxResult.Text = string.Empty;
            PlotCanvas.Children.Clear();
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}