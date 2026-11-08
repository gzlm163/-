using NCalc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApp1209afvd
{
    public partial class Lab4Window : Window
    {
        private const double PlotMargin = 30;
        private DispatcherTimer animationTimer;
        private List<double> stepsPoints = new List<double>();

        public Lab4Window()
        {
            InitializeComponent();
            animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(400);
            animationTimer.Tick += AnimationTimer_Tick;
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

        private double FirstDerivative(Func<double, double> function, double x)
        {
            double h = 1e-5;
            return (function(x + h) - function(x - h)) / (2 * h);
        }

        private double SecondDerivative(Func<double, double> function, double x)
        {
            double h = 1e-4;
            return (function(x + h) - 2 * function(x) + function(x - h)) / (h * h);
        }

        private bool TryNewtonMethod(Func<double, double> function, double a, double b, double startX, double accuracy, out double minX, out string error)
        {
            minX = startX;
            error = null;
            stepsPoints.Clear();

            double currentX = startX;

            for (int iter = 0; iter < 1000; iter++)
            {
                double f1 = FirstDerivative(function, currentX);
                double f2 = SecondDerivative(function, currentX);

                if (Math.Abs(f2) < 1e-10)
                {
                    error = "На отрезке нет минимума";
                    return false;
                }

                double nextX = currentX - (f1 / f2);

                if (double.IsNaN(nextX) || double.IsInfinity(nextX) || nextX < a || nextX > b)
                {
                    error = "На отрезке нет минимума";
                    return false;
                }

                stepsPoints.Add(nextX);

                if (Math.Abs(nextX - currentX) < accuracy)
                {
                    minX = nextX;
                    return true;
                }

                currentX = nextX;
            }

            error = "На отрезке нет минимума";
            return false;
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            animationTimer.Stop();
            if (!ValidateInputs(out var function, out var a, out var b, out var accuracy)) return;

            if (TryNewtonMethod(function, a, b, (a + b) / 2, accuracy, out double minX, out string error))
            {
                TextBoxResult.Text = $"Минимум найден:\r\nx = {minX:F6}\r\ny = {function(minX):F6}";
                DrawPlot(function, a, b);
            }
            else
            {
                TextBoxResult.Text = error;
            }
        }

        private void DrawPlot(Func<double, double> function, double left, double right)
        {
            PlotCanvas.Children.Clear();

            double canvasWidth = PlotCanvas.ActualWidth;
            double canvasHeight = PlotCanvas.ActualHeight;
            if (canvasWidth <= 2 * PlotMargin || canvasHeight <= 2 * PlotMargin) return;

            int pointCount = 500;
            double minValueY = double.MaxValue;
            double maxValueY = double.MinValue;

            for (int pointIndex = 0; pointIndex <= pointCount; ++pointIndex)
            {
                double currentX = left + (right - left) * pointIndex / pointCount;
                double currentY = function(currentX);

                if (!double.IsNaN(currentY) && !double.IsInfinity(currentY) && Math.Abs(currentY) < 1e6)
                {
                    if (currentY < minValueY) minValueY = currentY;
                    if (currentY > maxValueY) maxValueY = currentY;
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

            double axisYPixel = mapToCanvasY(0);
            if (axisYPixel < PlotMargin) axisYPixel = PlotMargin;
            if (axisYPixel > canvasHeight - PlotMargin) axisYPixel = canvasHeight - PlotMargin;

            double axisXPixel = mapToCanvasX(0);
            if (axisXPixel < PlotMargin) axisXPixel = PlotMargin;
            if (axisXPixel > canvasWidth - PlotMargin) axisXPixel = canvasWidth - PlotMargin;

            Line xAxisLine = new Line { X1 = PlotMargin, Y1 = axisYPixel, X2 = canvasWidth - PlotMargin - 10, Y2 = axisYPixel, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(xAxisLine);
            Polygon xArrow = new Polygon { Fill = Brushes.Black, Points = new PointCollection { new Point(canvasWidth - PlotMargin, axisYPixel), new Point(canvasWidth - PlotMargin - 10, axisYPixel - 5), new Point(canvasWidth - PlotMargin - 10, axisYPixel + 5) } };
            PlotCanvas.Children.Add(xArrow);

            Line yAxisLine = new Line { X1 = axisXPixel, Y1 = canvasHeight - PlotMargin, X2 = axisXPixel, Y2 = PlotMargin + 10, Stroke = Brushes.Black, StrokeThickness = 2 };
            PlotCanvas.Children.Add(yAxisLine);
            Polygon yArrow = new Polygon { Fill = Brushes.Black, Points = new PointCollection { new Point(axisXPixel, PlotMargin), new Point(axisXPixel - 5, PlotMargin + 10), new Point(axisXPixel + 5, PlotMargin + 10) } };
            PlotCanvas.Children.Add(yArrow);

            AddAxisLabel("X", canvasWidth - PlotMargin + 5, axisYPixel - 10);
            AddAxisLabel("Y", axisXPixel + 5, PlotMargin);

            Func<double, double> getNiceStep = (range) =>
            {
                double rawStep = range / 8.0;
                double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
                double normalizedStep = rawStep / magnitude;

                if (normalizedStep < 1.5) return magnitude;
                if (normalizedStep < 3.0) return 2 * magnitude;
                if (normalizedStep < 7.0) return 5 * magnitude;
                return 10 * magnitude;
            };

            double stepX = getNiceStep(right - left);
            double startX = Math.Ceiling(left / stepX) * stepX;
            for (double tickX = startX; tickX <= right; tickX += stepX)
            {
                double px = mapToCanvasX(tickX);
                Line tickLine = new Line { X1 = px, Y1 = axisYPixel - 3, X2 = px, Y2 = axisYPixel + 3, Stroke = Brushes.Black, StrokeThickness = 1 };
                PlotCanvas.Children.Add(tickLine);
                AddAxisLabel(tickX.ToString("G"), px - 10, axisYPixel + 5);
            }

            double stepY = getNiceStep(maxValueY - minValueY);
            double startY = Math.Ceiling(minValueY / stepY) * stepY;
            for (double tickY = startY; tickY <= maxValueY; tickY += stepY)
            {
                double py = mapToCanvasY(tickY);
                Line tickLine = new Line { X1 = axisXPixel - 3, Y1 = py, X2 = axisXPixel + 3, Y2 = py, Stroke = Brushes.Black, StrokeThickness = 1 };
                PlotCanvas.Children.Add(tickLine);

                if (Math.Abs(tickY) > stepY / 10)
                {
                    var label = new TextBlock { Text = tickY.ToString("G"), FontSize = 11, FontWeight = FontWeights.Bold };
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double textWidth = label.DesiredSize.Width;
                    double labelX = axisXPixel < 40 ? axisXPixel + 8 : axisXPixel - textWidth - 5;
                    AddAxisLabel(tickY.ToString("G"), labelX, py - 8);
                }
            }

            Polyline functionLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x5E, 0x80)), StrokeThickness = 2 };

            for (int pointIndex = 0; pointIndex <= pointCount; ++pointIndex)
            {
                double currentX = left + (right - left) * pointIndex / pointCount;
                double currentY = function(currentX);

                if (double.IsNaN(currentY) || double.IsInfinity(currentY) || Math.Abs(currentY) > 1e6)
                {
                    if (functionLine.Points.Count > 0)
                    {
                        PlotCanvas.Children.Add(functionLine);
                        functionLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x5E, 0x80)), StrokeThickness = 2 };
                    }
                    continue;
                }
                functionLine.Points.Add(new Point(mapToCanvasX(currentX), mapToCanvasY(currentY)));
            }
            if (functionLine.Points.Count > 0) PlotCanvas.Children.Add(functionLine);

            foreach (double stepXPoint in stepsPoints)
            {
                double yVal = function(stepXPoint);
                if (double.IsNaN(yVal) || double.IsInfinity(yVal) || Math.Abs(yVal) > 1e6) continue;

                Ellipse marker = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0xC7, 0x15, 0x85)) };
                Canvas.SetLeft(marker, mapToCanvasX(stepXPoint) - 4);
                Canvas.SetTop(marker, mapToCanvasY(yVal) - 4);
                PlotCanvas.Children.Add(marker);
            }
        }

        private void AddAxisLabel(string text, double xPosition, double yPosition)
        {
            TextBlock axisLabel = new TextBlock { Text = text, Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 11 };
            Canvas.SetLeft(axisLabel, xPosition);
            Canvas.SetTop(axisLabel, yPosition);
            PlotCanvas.Children.Add(axisLabel);
        }

        private void ButtonCalculate_Click(object sender, RoutedEventArgs e)
        {
            stepsPoints.Clear();
            TextBoxResult.Text = string.Empty;
            animationTimer.Start();
        }

        private void ButtonPlot_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out var function, out var a, out var b, out _)) return;
            stepsPoints.Clear();
            DrawPlot(function, a, b);
        }

        private bool ValidateInputs(out Func<double, double> function, out double a, out double b, out double accuracy)
        {
            function = null; a = 0; b = 0; accuracy = 0;

            if (!TryParseFunction(TextBoxFunction.Text, out function, out string err)) { MessageBox.Show(err); return false; }
            if (!TryParseDouble(TextBoxA.Text, "a", out a, out err)) { MessageBox.Show(err); return false; }
            if (!TryParseDouble(TextBoxB.Text, "b", out b, out err)) { MessageBox.Show(err); return false; }

            if (!int.TryParse(TextBoxE.Text, out int decimalPlaces)) { MessageBox.Show("Количество знаков должно быть целым числом"); return false; }
            if (decimalPlaces <= 0) { MessageBox.Show("Количество знаков должно быть больше 0"); return false; }

            accuracy = 1.0 / Math.Pow(10, decimalPlaces);

            if (a >= b) { MessageBox.Show("a должно быть меньше b"); return false; }
            if (accuracy >= b - a) { MessageBox.Show("Слишком высокая точность для данного отрезка"); return false; }

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
            stepsPoints.Clear();
            animationTimer.Stop();
        }

        private void ButtonExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}